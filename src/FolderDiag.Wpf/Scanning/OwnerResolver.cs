using System.Collections.Concurrent;
using System.Security.AccessControl;
using System.Security.Principal;

namespace FolderDiag.Wpf.Scanning
{
    /// <summary>
    /// Fast file/folder owner lookup for scans.
    /// - Reads only the Owner part of the security descriptor (not the full DACL).
    /// - Caches SID → "DOMAIN\user" translation, which is the expensive LSA call and is the
    ///   same for almost every file on a drive.
    /// Thread-safe.
    /// </summary>
    public sealed class OwnerResolver
    {
        public static OwnerResolver Shared { get; } = new();

        private readonly ConcurrentDictionary<string, string> _names = new(StringComparer.Ordinal);

        public string? Resolve(string path, bool isDirectory)
        {
            try
            {
                FileSystemSecurity sec = isDirectory
                    ? new DirectorySecurity(path, AccessControlSections.Owner)
                    : new FileSecurity(path, AccessControlSections.Owner);
                if (sec.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier sid) return null;

                return _names.GetOrAdd(sid.Value, _ =>
                {
                    try { return sid.Translate(typeof(NTAccount)).Value; }
                    catch { return sid.Value; } // deleted/unknown account: show the SID
                });
            }
            catch { return null; } // access denied etc.
        }
    }
}
