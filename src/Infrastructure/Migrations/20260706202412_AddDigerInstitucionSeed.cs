using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDigerInstitucionSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Instituciones",
                columns: new[] { "Id", "Activo", "CreatedAt", "CreatedBy", "Descripcion", "InfoExtra", "LogoUrl", "Nombre", "NombreCorto", "UpdatedAt", "UpdatedBy" },
                values: new object[] { "DIGER", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "DIGER (Sistema)", null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Instituciones",
                keyColumn: "Id",
                keyValue: "DIGER");
        }
    }
}
