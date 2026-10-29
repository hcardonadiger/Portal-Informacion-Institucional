using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CalendarioOutlookAlPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CanceladaEnOutlookEl",
                table: "Reuniones",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutlookICalUId",
                table: "Reuniones",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OutlookImportadaPorId",
                table: "Reuniones",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutlookImportadoEventId",
                table: "Reuniones",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OutlookModificadaEl",
                table: "Reuniones",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeOutlook",
                table: "Asistentes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_OutlookICalUId",
                table: "Reuniones",
                column: "OutlookICalUId",
                filter: "[OutlookICalUId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_OutlookImportadaPorId",
                table: "Reuniones",
                column: "OutlookImportadaPorId",
                filter: "[OutlookImportadaPorId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reuniones_OutlookICalUId",
                table: "Reuniones");

            migrationBuilder.DropIndex(
                name: "IX_Reuniones_OutlookImportadaPorId",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "CanceladaEnOutlookEl",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "OutlookICalUId",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "OutlookImportadaPorId",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "OutlookImportadoEventId",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "OutlookModificadaEl",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "DeOutlook",
                table: "Asistentes");
        }
    }
}
