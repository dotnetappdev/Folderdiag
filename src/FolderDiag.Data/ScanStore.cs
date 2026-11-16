using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq.Expressions;
using FolderDiag.Core.Models;
using FolderDiag.Core.Scanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace FolderDiag.Data
{
    /// <summary>
    /// Fast persistence for scan results.
    /// - One transaction per scan: a cancelled or failed scan rolls back and keeps the previous results.
    /// - Old rows removed with a single DELETE (not loaded into memory first).
    /// - Rows written with a prepared INSERT (no EF change tracking), streamed from the scanner
    ///   through a bounded queue so memory stays flat on drives with millions of files.
    /// - Parent folders' totals are adjusted so a sub-folder rescan doesn't leave them stale.
    /// </summary>
    public static class ScanStore
    {
        /// <summary>Create the database plus the indexes the folder views query on.</summary>
        public static void Initialize(FolderDiagDbContext db)
        {
            db.Database.EnsureCreated();
            // Table names come from the EF model (not user input); validate anyway before using them in DDL
            string folders = Identifier(Table<FolderItem>(db)), files = Identifier(Table<FileItem>(db));
            string[] ddl =
            {
                "PRAGMA journal_mode=WAL;",
                $"CREATE INDEX IF NOT EXISTS IX_{folders}_ParentPath ON \"{folders}\" (\"ParentPath\" COLLATE NOCASE);",
                $"CREATE INDEX IF NOT EXISTS IX_{folders}_Path ON \"{folders}\" (\"Path\" COLLATE NOCASE);",
                $"CREATE INDEX IF NOT EXISTS IX_{files}_FolderPath ON \"{files}\" (\"FolderPath\" COLLATE NOCASE);",
                $"CREATE INDEX IF NOT EXISTS IX_{files}_SizeBytes ON \"{files}\" (\"SizeBytes\" DESC);",
            };
            foreach (string sql in ddl) db.Database.ExecuteSqlRaw(sql);

            RemoveDuplicateRows(db, folders, files);
        }

        /// <summary>
        /// Clean up results left by older versions: keep only the newest row per path
        /// (case-insensitive), and drop drive-relative paths such as "C:Users" that an old bug
        /// recorded instead of "C:\\Users". Cheap when there's nothing to remove.
        /// </summary>
        private static void RemoveDuplicateRows(FolderDiagDbContext db, string folders, string files)
        {
            foreach (var (table, col) in new[] { (folders, "Path"), (files, "Path") })
            {
                string dedupe =
                    $"DELETE FROM \"{table}\" WHERE \"Id\" IN (" +
                    $"  SELECT \"Id\" FROM (SELECT \"Id\", ROW_NUMBER() OVER (" +
                    $"    PARTITION BY \"{col}\" COLLATE NOCASE ORDER BY \"ScannedAt\" DESC, \"Id\" DESC) AS rn" +
                    $"  FROM \"{table}\") WHERE rn > 1);";
                // "X:" followed by anything other than a backslash = drive-relative (wrong) path
                string driveRelative =
                    $"DELETE FROM \"{table}\" WHERE \"{col}\" GLOB '[A-Za-z]:*' AND \"{col}\" NOT GLOB '[A-Za-z]:\\*';";
                db.Database.ExecuteSqlRaw(dedupe);
                db.Database.ExecuteSqlRaw(driveRelative);
            }
        }

        public static ScanTotals ScanAndStore(
            DbContextOptions<FolderDiagDbContext> dbOptions,
            string rootPath,
            ScanOptions options,
            IProgress<ScanProgress>? progress,
            CancellationToken ct)
        {
            using var db = new FolderDiagDbContext(dbOptions);
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            db.Database.OpenConnection();
            db.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");   // safe with WAL, much faster commits
            db.Database.ExecuteSqlRaw("PRAGMA temp_store=MEMORY;");

            using var tx = db.Database.BeginTransaction();

            // Previous size of this folder, to correct its ancestors afterwards
            var previous = db.FolderItems.AsNoTracking()
                .Where(f => EF.Functions.Collate(f.Path, "NOCASE") == rootPath)
                .Select(f => new { f.SizeBytes, f.AllocatedBytes, f.FileCount, f.FolderCount })
                .FirstOrDefault();

            // Remove this folder and everything below it (but not sibling "C:\Users\me2" when rescanning "C:\Users\me")
            string prefix = LikeUnder(rootPath);
            db.FolderItems.Where(f => EF.Functions.Collate(f.Path, "NOCASE") == rootPath
                                   || EF.Functions.Like(f.Path, prefix, "\\")).ExecuteDelete();
            db.FileItems.Where(f => EF.Functions.Collate(f.FolderPath, "NOCASE") == rootPath
                                 || EF.Functions.Like(f.FolderPath, prefix, "\\")).ExecuteDelete();

            var dbTx = tx.GetDbTransaction();
            using var folderWriter = new BulkInserter<FolderItem>(db, dbTx);
            using var fileWriter   = new BulkInserter<FileItem>(db, dbTx);

            // Scanner threads → bounded queue → single writer (SQLite allows one writer)
            using var queue = new BlockingCollection<object>(boundedCapacity: 50_000);
            Exception? writeError = null;
            var writer = Task.Run(() =>
            {
                try
                {
                    foreach (var item in queue.GetConsumingEnumerable())
                        if (item is FileItem f) fileWriter.Insert(f); else folderWriter.Insert((FolderItem)item);
                }
                catch (Exception ex) { writeError = ex; queue.CompleteAdding(); }
            });

            ScanTotals totals;
            try
            {
                totals = FolderScanner.Scan(rootPath, options,
                    onFolder: f => queue.Add(f, ct),
                    onFile:   f => queue.Add(f, ct),
                    progress, ct);
            }
            catch (Exception) when (writeError != null)
            {
                throw new IOException("Saving scan results failed: " + writeError.Message, writeError);
            }
            catch (AggregateException) when (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct); // parallel workers wrap cancellation
            }
            finally
            {
                if (!queue.IsAddingCompleted) queue.CompleteAdding();
                writer.Wait();
            }
            if (writeError != null) throw new IOException("Saving scan results failed: " + writeError.Message, writeError);

            UpdateAncestors(db, rootPath, totals,
                previous == null ? default : new ScanTotals(previous.SizeBytes, previous.AllocatedBytes, previous.FileCount, previous.FolderCount),
                isNew: previous == null);

            tx.Commit();
            return totals;
        }

        /// <summary>Apply the size change of a rescanned folder to every scanned ancestor.</summary>
        private static void UpdateAncestors(FolderDiagDbContext db, string root, ScanTotals now, ScanTotals before, bool isNew)
        {
            long dSize = now.Size - before.Size, dAlloc = now.Allocated - before.Allocated;
            int dFiles = now.Files - before.Files;
            int dFolders = now.Folders - before.Folders + (isNew ? 1 : 0);
            if (dSize == 0 && dAlloc == 0 && dFiles == 0 && dFolders == 0) return;

            for (string? p = Path.GetDirectoryName(root); !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
            {
                string ancestor = p;
                db.FolderItems.Where(f => EF.Functions.Collate(f.Path, "NOCASE") == ancestor)
                    .ExecuteUpdate(u => u
                        .SetProperty(f => f.SizeBytes, f => f.SizeBytes + dSize)
                        .SetProperty(f => f.AllocatedBytes, f => f.AllocatedBytes + dAlloc)
                        .SetProperty(f => f.FileCount, f => f.FileCount + dFiles)
                        .SetProperty(f => f.FolderCount, f => f.FolderCount + dFolders));
            }
        }

        private static string Identifier(string name)
            => System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$")
                ? name : throw new InvalidOperationException($"Unexpected table name '{name}'");

        /// <summary>LIKE pattern matching everything strictly inside <paramref name="folder"/> (use with escape '\\').</summary>
        public static string LikeUnder(string folder)
            => EscapeLike(folder.EndsWith('\\') || folder.EndsWith('/') ? folder
                          : folder + (folder.Contains('\\') ? '\\' : Path.DirectorySeparatorChar)) + "%";

        /// <summary>Files in <paramref name="folder"/> or any sub-folder (case-insensitive, no "C:\\Users\\me2" false matches).</summary>
        public static IQueryable<FileItem> FilesUnder(IQueryable<FileItem> q, string folder)
        {
            string like = LikeUnder(folder);
            return q.Where(f => EF.Functions.Collate(f.FolderPath, "NOCASE") == folder || EF.Functions.Like(f.FolderPath, like, "\\"));
        }

        /// <summary>Folders strictly below <paramref name="folder"/>.</summary>
        public static IQueryable<FolderItem> FoldersBelow(IQueryable<FolderItem> q, string folder)
        {
            string like = LikeUnder(folder);
            return q.Where(f => EF.Functions.Like(f.Path, like, "\\"));
        }

        private static string EscapeLike(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

        internal static string Table<T>(DbContext db) => db.Model.FindEntityType(typeof(T))!.GetTableName()!;
    }

    /// <summary>
    /// Prepared-statement INSERT for an EF entity, using EF's own model metadata for the
    /// table/column names and value conversions (so it stays in sync with the DbContext).
    /// One row per statement: Microsoft.Data.Sqlite binds parameters by name, so multi-row
    /// VALUES lists get slower, not faster (measured). Use from one writer thread.
    /// </summary>
    internal sealed class BulkInserter<T> : IDisposable where T : class
    {
        private readonly DbCommand _cmd;
        private readonly (DbParameter Param, Func<T, object?> Get, Func<object?, object?>? Convert)[] _columns;

        public BulkInserter(DbContext db, DbTransaction tx)
        {
            var entity = db.Model.FindEntityType(typeof(T))!;
            string table = entity.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());

            var props = entity.GetProperties()
                .Where(p => !(p.IsPrimaryKey() && p.ValueGenerated != ValueGenerated.Never) && p.PropertyInfo != null)
                .ToArray();

            _cmd = db.Database.GetDbConnection().CreateCommand();
            _cmd.Transaction = tx;
            _cmd.CommandText =
                $"INSERT INTO \"{table}\" ({string.Join(", ", props.Select(p => $"\"{p.GetColumnName(store)}\""))}) " +
                $"VALUES ({string.Join(", ", props.Select((_, i) => $"@p{i}"))})";

            _columns = props.Select((p, i) =>
            {
                var prm = _cmd.CreateParameter();
                prm.ParameterName = $"@p{i}";
                _cmd.Parameters.Add(prm);

                // Compiled getter: item => (object)item.Prop  (much faster than reflection per row)
                var arg = Expression.Parameter(typeof(T));
                var get = Expression.Lambda<Func<T, object?>>(
                    Expression.Convert(Expression.Property(arg, p.PropertyInfo!), typeof(object)), arg).Compile();

                var conv = p.GetTypeMapping().Converter;
                return (prm, get, conv == null ? null : (Func<object?, object?>)conv.ConvertToProvider);
            }).ToArray();

            _cmd.Prepare();
        }

        public void Insert(T item)
        {
            foreach (var (param, get, convert) in _columns)
            {
                object? v = get(item);
                if (convert != null && v != null) v = convert(v);
                param.Value = v ?? DBNull.Value;
            }
            _cmd.ExecuteNonQuery();
        }

        public void Dispose() => _cmd.Dispose();
    }
}
