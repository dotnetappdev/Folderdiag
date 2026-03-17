using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FolderDiag.Data.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ScanHistory",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                RootPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                ScanTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                TotalSize = table.Column<long>(type: "INTEGER", nullable: false),
                TotalFiles = table.Column<int>(type: "INTEGER", nullable: false),
                TotalFolders = table.Column<int>(type: "INTEGER", nullable: false),
                ScanDurationSeconds = table.Column<double>(type: "REAL", nullable: false),
                IsComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScanHistory", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Settings",
            columns: table => new
            {
                Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Value = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Settings", x => x.Key);
            });

        migrationBuilder.CreateTable(
            name: "FileIndex",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                FullPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                Extension = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                IsDirectory = table.Column<bool>(type: "INTEGER", nullable: false),
                Size = table.Column<long>(type: "INTEGER", nullable: false),
                TotalSize = table.Column<long>(type: "INTEGER", nullable: false),
                LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                ScanHistoryId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FileIndex", x => x.Id);
                table.ForeignKey(
                    name: "FK_FileIndex_ScanHistory_ScanHistoryId",
                    column: x => x.ScanHistoryId,
                    principalTable: "ScanHistory",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ScanHistory_RootPath",
            table: "ScanHistory",
            column: "RootPath");

        migrationBuilder.CreateIndex(
            name: "IX_ScanHistory_ScanTime",
            table: "ScanHistory",
            column: "ScanTime");

        migrationBuilder.CreateIndex(
            name: "IX_FileIndex_Name",
            table: "FileIndex",
            column: "Name");

        migrationBuilder.CreateIndex(
            name: "IX_FileIndex_Extension",
            table: "FileIndex",
            column: "Extension");

        migrationBuilder.CreateIndex(
            name: "IX_FileIndex_LastModified",
            table: "FileIndex",
            column: "LastModified");

        migrationBuilder.CreateIndex(
            name: "IX_FileIndex_Size",
            table: "FileIndex",
            column: "Size");

        migrationBuilder.CreateIndex(
            name: "IX_FileIndex_ScanHistoryId_IsDirectory",
            table: "FileIndex",
            columns: ["ScanHistoryId", "IsDirectory"]);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FileIndex");
        migrationBuilder.DropTable(name: "ScanHistory");
        migrationBuilder.DropTable(name: "Settings");
    }
}
