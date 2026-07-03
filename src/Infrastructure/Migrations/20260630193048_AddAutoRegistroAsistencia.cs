using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoRegistroAsistencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RegistroAbierto",
                table: "Reuniones",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegistroToken",
                table: "Reuniones",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<bool>(
                name: "AutoRegistro",
                table: "Asistentes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Departamento",
                table: "Asistentes",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistradoEl",
                table: "Asistentes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_RegistroToken",
                table: "Reuniones",
                column: "RegistroToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reuniones_RegistroToken",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "RegistroAbierto",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "RegistroToken",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "AutoRegistro",
                table: "Asistentes");

            migrationBuilder.DropColumn(
                name: "Departamento",
                table: "Asistentes");

            migrationBuilder.DropColumn(
                name: "RegistradoEl",
                table: "Asistentes");
        }
    }
}
