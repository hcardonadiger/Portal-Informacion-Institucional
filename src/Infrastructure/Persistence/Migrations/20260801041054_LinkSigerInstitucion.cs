using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkSigerInstitucion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstitucionId",
                table: "TramitesSiger",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            // Create missing institutions from SIGER siglas
            migrationBuilder.Sql(@"
                INSERT INTO [Instituciones] ([Id], [Nombre], [Activo], [CreatedAt])
                SELECT
                    norm.NormSigla,
                    norm.Institucion,
                    1,
                    GETUTCDATE()
                FROM (
                    SELECT DISTINCT
                        UPPER(TRANSLATE(ts.[Sigla], N'áéíóúÁÉÍÓÚñÑ', N'aeiouAEIOUnN')) AS NormSigla,
                        ts.[Institucion]
                    FROM [TramitesSiger] ts
                    WHERE ts.[Sigla] IS NOT NULL AND ts.[Sigla] <> ''
                ) norm
                WHERE norm.NormSigla NOT IN (SELECT [Id] FROM [Instituciones])
                  AND norm.NormSigla IS NOT NULL;
            ");

            // Populate InstitucionId from normalized Sigla
            migrationBuilder.Sql(@"
                UPDATE [TramitesSiger]
                SET [InstitucionId] = UPPER(TRANSLATE([Sigla], N'áéíóúÁÉÍÓÚñÑ', N'aeiouAEIOUnN'))
                WHERE [Sigla] IS NOT NULL AND [Sigla] <> ''
                  AND UPPER(TRANSLATE([Sigla], N'áéíóúÁÉÍÓÚñÑ', N'aeiouAEIOUnN'))
                      IN (SELECT [Id] FROM [Instituciones]);
            ");

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_InstitucionId",
                table: "TramitesSiger",
                column: "InstitucionId");

            migrationBuilder.AddForeignKey(
                name: "FK_TramitesSiger_Instituciones_InstitucionId",
                table: "TramitesSiger",
                column: "InstitucionId",
                principalTable: "Instituciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TramitesSiger_Instituciones_InstitucionId",
                table: "TramitesSiger");

            migrationBuilder.DropIndex(
                name: "IX_TramitesSiger_InstitucionId",
                table: "TramitesSiger");

            migrationBuilder.DropColumn(
                name: "InstitucionId",
                table: "TramitesSiger");
        }
    }
}
