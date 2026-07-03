using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeguimientoCompromisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "AcuerdosReunion",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pendiente");

            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaCumplimiento",
                table: "AcuerdosReunion",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotaSeguimiento",
                table: "AcuerdosReunion",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SeguimientoActualizadoEl",
                table: "AcuerdosReunion",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeguimientoActualizadoPor",
                table: "AcuerdosReunion",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdosReunion_Estado",
                table: "AcuerdosReunion",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdosReunion_Plazo",
                table: "AcuerdosReunion",
                column: "Plazo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AcuerdosReunion_Estado",
                table: "AcuerdosReunion");

            migrationBuilder.DropIndex(
                name: "IX_AcuerdosReunion_Plazo",
                table: "AcuerdosReunion");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "AcuerdosReunion");

            migrationBuilder.DropColumn(
                name: "FechaCumplimiento",
                table: "AcuerdosReunion");

            migrationBuilder.DropColumn(
                name: "NotaSeguimiento",
                table: "AcuerdosReunion");

            migrationBuilder.DropColumn(
                name: "SeguimientoActualizadoEl",
                table: "AcuerdosReunion");

            migrationBuilder.DropColumn(
                name: "SeguimientoActualizadoPor",
                table: "AcuerdosReunion");
        }
    }
}
