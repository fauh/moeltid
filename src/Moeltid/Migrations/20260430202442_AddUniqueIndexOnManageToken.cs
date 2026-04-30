using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moeltid.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnManageToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Events_ManageToken",
                table: "Events",
                column: "ManageToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_ManageToken",
                table: "Events");
        }
    }
}
