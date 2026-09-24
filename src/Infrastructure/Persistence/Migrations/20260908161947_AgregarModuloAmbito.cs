using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarModuloAmbito : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModuloAmbitos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Modulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    InstitucionId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AreaId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UnidadId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuloAmbitos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModuloAmbitos_Modulo",
                table: "ModuloAmbitos",
                column: "Modulo");

            migrationBuilder.CreateIndex(
                name: "IX_ModuloAmbitos_Modulo_InstitucionId_AreaId_UnidadId",
                table: "ModuloAmbitos",
                columns: new[] { "Modulo", "InstitucionId", "AreaId", "UnidadId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModuloAmbitos");
        }
    }
}
