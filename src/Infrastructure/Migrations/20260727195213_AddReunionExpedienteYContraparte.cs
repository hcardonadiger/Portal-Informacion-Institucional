using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReunionExpedienteYContraparte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpedienteCodigo",
                table: "Reuniones",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpedienteId",
                table: "Reuniones",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContraparteUsuarioId",
                table: "Expedientes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContraparteUsuarioNombre",
                table: "Expedientes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaLimiteEntrega",
                table: "Expedientes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpedienteId",
                table: "AcuerdosReunion",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TramiteIndex",
                table: "AcuerdosReunion",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TramiteNombre",
                table: "AcuerdosReunion",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpedienteCodigo",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "ExpedienteId",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "ContraparteUsuarioId",
                table: "Expedientes");

            migrationBuilder.DropColumn(
                name: "ContraparteUsuarioNombre",
                table: "Expedientes");

            migrationBuilder.DropColumn(
                name: "FechaLimiteEntrega",
                table: "Expedientes");

            migrationBuilder.DropColumn(
                name: "ExpedienteId",
                table: "AcuerdosReunion");

            migrationBuilder.DropColumn(
                name: "TramiteIndex",
                table: "AcuerdosReunion");

            migrationBuilder.DropColumn(
                name: "TramiteNombre",
                table: "AcuerdosReunion");
        }
    }
}
