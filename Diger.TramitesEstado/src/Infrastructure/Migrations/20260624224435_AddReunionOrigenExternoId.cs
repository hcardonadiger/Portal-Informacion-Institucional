using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReunionOrigenExternoId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrigenExternoId",
                table: "Reuniones",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_OrigenExternoId",
                table: "Reuniones",
                column: "OrigenExternoId",
                unique: true,
                filter: "[OrigenExternoId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reuniones_OrigenExternoId",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "OrigenExternoId",
                table: "Reuniones");
        }
    }
}
