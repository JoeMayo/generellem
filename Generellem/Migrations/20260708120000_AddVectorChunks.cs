using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Generellem.Migrations;

/// <inheritdoc />
public partial class AddVectorChunks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VectorChunks",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                DocumentReference = table.Column<string>(type: "TEXT", nullable: true),
                SourceReference = table.Column<string>(type: "TEXT", nullable: true),
                Content = table.Column<string>(type: "TEXT", nullable: true),
                Embedding = table.Column<byte[]>(type: "BLOB", nullable: true),
                Order = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VectorChunks", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_VectorChunks_DocumentReference",
            table: "VectorChunks",
            column: "DocumentReference");

        migrationBuilder.CreateIndex(
            name: "IX_VectorChunks_SourceReference",
            table: "VectorChunks",
            column: "SourceReference");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "VectorChunks");
    }
}
