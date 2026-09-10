using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditoriaEnEntregablesYActividades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ProyectoEntregables",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ProyectoEntregables",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ProyectoEntregables",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "ProyectoEntregables",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ProyectoActividades",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ProyectoActividades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ProyectoActividades",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "ProyectoActividades",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ProyectoEntregables");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ProyectoEntregables");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ProyectoEntregables");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ProyectoEntregables");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ProyectoActividades");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ProyectoActividades");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ProyectoActividades");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ProyectoActividades");
        }
    }
}
