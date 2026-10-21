using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Script F del plan: colación insensible a tildes en las columnas sobre las que busca
    /// ?busqueda= (decisión P-07 — Nombre, Descripcion, Objetivo de TramitesSiger — más
    /// Institucion, y Instituciones.Nombre por consistencia con la conciliación de SIGER).
    /// Nombre e Institucion participan en índices (uno de ellos único), así que hay que
    /// soltarlos antes del ALTER COLUMN y recrearlos después — EF no modela esa danza sola
    /// (el migrations add generado a mano solo trae los AlterColumn; esto se completó a mano).
    /// </summary>
    /// <inheritdoc />
    public partial class CorregirColacionBusqueda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Soltar los índices dependientes ANTES de tocar las columnas ────────────
            migrationBuilder.DropIndex(
                name: "IX_TramitesSiger_Catalogo",
                table: "TramitesSiger");

            migrationBuilder.DropIndex(
                name: "IX_TramitesSiger_Institucion",
                table: "TramitesSiger");

            migrationBuilder.DropIndex(
                name: "IX_Instituciones_Nombre",
                table: "Instituciones");

            // ── Cambiar la colación ─────────────────────────────────────────────────────
            migrationBuilder.AlterColumn<string>(
                name: "Objetivo",
                table: "TramitesSiger",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                collation: "Modern_Spanish_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "TramitesSiger",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: false,
                collation: "Modern_Spanish_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(600)",
                oldMaxLength: 600);

            migrationBuilder.AlterColumn<string>(
                name: "Institucion",
                table: "TramitesSiger",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                collation: "Modern_Spanish_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "TramitesSiger",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                collation: "Modern_Spanish_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Instituciones",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                collation: "Modern_Spanish_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            // ── Recrear los índices ──────────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_Catalogo",
                table: "TramitesSiger",
                columns: new[] { "Publicado", "CategoriaId", "InstitucionId" })
                .Annotation("SqlServer:Include", new[] { "Codigo", "Nombre", "Modalidad", "EsPopular", "CostoEsGratuito" });

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_Institucion",
                table: "TramitesSiger",
                column: "Institucion");

            migrationBuilder.CreateIndex(
                name: "IX_Instituciones_Nombre",
                table: "Instituciones",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TramitesSiger_Catalogo",
                table: "TramitesSiger");

            migrationBuilder.DropIndex(
                name: "IX_TramitesSiger_Institucion",
                table: "TramitesSiger");

            migrationBuilder.DropIndex(
                name: "IX_Instituciones_Nombre",
                table: "Instituciones");

            migrationBuilder.AlterColumn<string>(
                name: "Objetivo",
                table: "TramitesSiger",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true,
                oldCollation: "Modern_Spanish_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "TramitesSiger",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(600)",
                oldMaxLength: 600,
                oldCollation: "Modern_Spanish_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "Institucion",
                table: "TramitesSiger",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldCollation: "Modern_Spanish_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "TramitesSiger",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true,
                oldCollation: "Modern_Spanish_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Instituciones",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldCollation: "Modern_Spanish_CI_AI");

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_Catalogo",
                table: "TramitesSiger",
                columns: new[] { "Publicado", "CategoriaId", "InstitucionId" })
                .Annotation("SqlServer:Include", new[] { "Codigo", "Nombre", "Modalidad", "EsPopular", "CostoEsGratuito" });

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_Institucion",
                table: "TramitesSiger",
                column: "Institucion");

            migrationBuilder.CreateIndex(
                name: "IX_Instituciones_Nombre",
                table: "Instituciones",
                column: "Nombre",
                unique: true);
        }
    }
}
