using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moeltid.Migrations
{
    /// <inheritdoc />
    public partial class DropMyEventsAccessToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MyEventsAccessTokens");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MyEventsAccessTokens",
                columns: table => new
                {
                    Token = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyEventsAccessTokens", x => x.Token);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MyEventsAccessTokens_ExpiresAt",
                table: "MyEventsAccessTokens",
                column: "ExpiresAt");
        }
    }
}
