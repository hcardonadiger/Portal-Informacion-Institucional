using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpedienteOrigenExternoId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrigenExternoId",
                table: "Expedientes",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expedientes_OrigenExternoId",
                table: "Expedientes",
                column: "OrigenExternoId",
                unique: true,
                filter: "[OrigenExternoId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Expedientes_OrigenExternoId",
                table: "Expedientes");

            migrationBuilder.DropColumn(
                name: "OrigenExternoId",
                table: "Expedientes");
        }
    }
}
