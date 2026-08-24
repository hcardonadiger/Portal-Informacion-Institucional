using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Crea el archivo del SIGER original: una copia congelada de cada ficha y de sus seis
    /// colecciones hijas, guardada como documento JSON.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sin llave foránea a TramitesSiger, a propósito.</b> Un archivo tiene que sobrevivir a
    /// su propio sujeto: con cascada, borrar una ficha destruiría la única copia de su información
    /// original, y con restricción el archivo impediría borrar fichas. La fila lleva el código y
    /// el identificador de SIGER copiados para poder reconocerla aunque la ficha ya no exista.
    /// </para>
    /// <para>
    /// El índice único sobre (TramiteSigerId, Version) es lo que hace idempotente la captura del
    /// original: sin él, dos corridas simultáneas guardarían la versión 0 dos veces y el archivo
    /// dejaría de tener una respuesta única a «cómo era esto al principio».
    /// </para>
    /// </remarks>
    public partial class ArchivoSigerOriginal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FotosTramiteSiger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TramiteSigerId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdSiger = table.Column<int>(type: "int", nullable: true),
                    CapturadaEl = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotosTramiteSiger", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FotosTramiteSiger_Codigo",
                table: "FotosTramiteSiger",
                column: "Codigo");

            migrationBuilder.CreateIndex(
                name: "IX_FotosTramiteSiger_TramiteSigerId_Version",
                table: "FotosTramiteSiger",
                columns: new[] { "TramiteSigerId", "Version" },
                unique: true);
        }

        /// <summary>Revierte la creación del archivo.</summary>
        /// <remarks>
        /// <b>PELIGRO — esto no se puede deshacer.</b> Esta tabla es la única copia de cómo era el
        /// inventario SIGER antes de que el portal empezara a escribir encima. Si ya se capturó,
        /// revertir esta migración borra ese original de forma definitiva: no está duplicado en
        /// ninguna otra parte y no se puede volver a tomar, porque las fichas vivas ya cambiaron.
        /// Antes de correr esto, respalde la tabla FotosTramiteSiger.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FotosTramiteSiger");
        }
    }
}
