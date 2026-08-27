using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepositorioDocumentalProyectos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoriasDocumento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasDocumento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProyectoDocumentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ProyectoId = table.Column<int>(type: "int", nullable: false),
                    CategoriaId = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectoDocumentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProyectoDocumentos_CategoriasDocumento_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "CategoriasDocumento",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProyectoDocumentos_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProyectoDocumentoVersiones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentoId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    ArchivoNombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ArchivoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ArchivoTamano = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubidoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SubidoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectoDocumentoVersiones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProyectoDocumentoVersiones_ProyectoDocumentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "ProyectoDocumentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasDocumento_Nombre",
                table: "CategoriasDocumento",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasDocumento_Orden",
                table: "CategoriasDocumento",
                column: "Orden");

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoDocumentos_CategoriaId",
                table: "ProyectoDocumentos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoDocumentos_ProyectoId_CategoriaId",
                table: "ProyectoDocumentos",
                columns: new[] { "ProyectoId", "CategoriaId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoDocumentoVersiones_DocumentoId_Numero",
                table: "ProyectoDocumentoVersiones",
                columns: new[] { "DocumentoId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoDocumentoVersiones_Sha256",
                table: "ProyectoDocumentoVersiones",
                column: "Sha256");

            // Semilla del catálogo. Va en la migración y no en un script aparte porque una base
            // nueva tiene que arrancar con categorías: sin ninguna, la pantalla de subir documento
            // no ofrecería dónde clasificarlo y el módulo nacería inutilizable.
            //
            // Fecha fija a propósito: DateTime.UtcNow acá haría que dos entornos migrados el mismo
            // día no coincidieran al diffear el esquema.
            var sembradoEn = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);
            migrationBuilder.InsertData(
                table: "CategoriasDocumento",
                columns: ["Nombre", "Descripcion", "Orden", "Activa", "CreatedAt", "CreatedBy"],
                values: new object[,]
                {
                    { "Acta",         "Actas de reunión, de entrega y ayudas memoria",          1, true, sembradoEn, "sistema" },
                    { "Convenio",     "Convenios, adendas y cartas de entendimiento",           2, true, sembradoEn, "sistema" },
                    { "Contrato",     "Contratos y órdenes de compra",                          3, true, sembradoEn, "sistema" },
                    { "Informe",      "Informes de avance, técnicos y de cierre",               4, true, sembradoEn, "sistema" },
                    { "Plan",         "Planes de trabajo, cronogramas y presupuestos",          5, true, sembradoEn, "sistema" },
                    { "Normativa",    "Leyes, reglamentos, dictámenes y lineamientos",          6, true, sembradoEn, "sistema" },
                    { "Presentación", "Presentaciones y material de difusión",                  7, true, sembradoEn, "sistema" },
                    { "Otro",         "Lo que no encaja en las anteriores",                    99, true, sembradoEn, "sistema" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProyectoDocumentoVersiones");

            migrationBuilder.DropTable(
                name: "ProyectoDocumentos");

            migrationBuilder.DropTable(
                name: "CategoriasDocumento");
        }
    }
}
