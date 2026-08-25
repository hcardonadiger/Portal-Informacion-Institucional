using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UrlSolCompuesta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TramitesSiger_EstaEnSol",
                table: "TramitesSiger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TramitesSiger_Sol",
                table: "TramitesSiger");

            migrationBuilder.AddColumn<string>(
                name: "SolTramo",
                table: "TramitesSiger",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RutaSol",
                table: "Instituciones",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_EstaEnSol",
                table: "TramitesSiger",
                column: "EstaEnSol",
                filter: "[EstaEnSol] = 1")
                .Annotation("SqlServer:Include", new[] { "SolUrl", "SolTramo" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TramitesSiger_Sol",
                table: "TramitesSiger",
                sql: "[EstaEnSol] = 0 OR [SolTramo] IS NOT NULL OR ([SolUrl] IS NOT NULL AND ([SolUrl] LIKE 'http://%' OR [SolUrl] LIKE 'https://%'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TramitesSiger_EstaEnSol",
                table: "TramitesSiger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TramitesSiger_Sol",
                table: "TramitesSiger");

            migrationBuilder.DropColumn(
                name: "SolTramo",
                table: "TramitesSiger");

            migrationBuilder.DropColumn(
                name: "RutaSol",
                table: "Instituciones");

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_EstaEnSol",
                table: "TramitesSiger",
                column: "EstaEnSol",
                filter: "[EstaEnSol] = 1")
                .Annotation("SqlServer:Include", new[] { "SolUrl" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TramitesSiger_Sol",
                table: "TramitesSiger",
                sql: "[EstaEnSol] = 0 OR ([SolUrl] IS NOT NULL AND ([SolUrl] LIKE 'http://%' OR [SolUrl] LIKE 'https://%'))");
        }
    }
}
