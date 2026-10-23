using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReunionesAOutlook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OutlookConTeams",
                table: "Reuniones",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "OutlookEnlaceTeams",
                table: "Reuniones",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutlookError",
                table: "Reuniones",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutlookEventId",
                table: "Reuniones",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutlookHuella",
                table: "Reuniones",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OutlookOrganizadorId",
                table: "Reuniones",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OutlookPendiente",
                table: "Reuniones",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OutlookSincronizadaEl",
                table: "Reuniones",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OutlookTransaccionId",
                table: "Reuniones",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConexionesMicrosoft",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Upn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    HomeAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CacheTokens = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ConectadaEl = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequiereReconexion = table.Column<bool>(type: "bit", nullable: false),
                    UltimoError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConexionesMicrosoft", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConexionesMicrosoft_Usuarios_Id",
                        column: x => x.Id,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConexionesMicrosoft");

            migrationBuilder.DropColumn(
                name: "OutlookConTeams",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "OutlookEnlaceTeams",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "OutlookError",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "OutlookEventId",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "OutlookHuella",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "OutlookOrganizadorId",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "OutlookPendiente",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "OutlookSincronizadaEl",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "OutlookTransaccionId",
                table: "Reuniones");
        }
    }
}
