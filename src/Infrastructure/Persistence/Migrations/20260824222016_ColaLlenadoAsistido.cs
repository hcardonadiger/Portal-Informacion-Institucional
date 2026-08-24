using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ColaLlenadoAsistido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropuestasLlenado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TramiteSigerId = table.Column<int>(type: "int", nullable: false),
                    Campo = table.Column<int>(type: "int", nullable: false),
                    ValorPropuesto = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Certeza = table.Column<int>(type: "int", nullable: false),
                    Justificacion = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    DecididaEl = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecididaPor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropuestasLlenado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropuestasLlenado_TramitesSiger_TramiteSigerId",
                        column: x => x.TramiteSigerId,
                        principalTable: "TramitesSiger",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropuestasLlenado_Estado_Certeza",
                table: "PropuestasLlenado",
                columns: new[] { "Estado", "Certeza" });

            migrationBuilder.CreateIndex(
                name: "IX_PropuestasLlenado_TramiteSigerId_Campo",
                table: "PropuestasLlenado",
                columns: new[] { "TramiteSigerId", "Campo" },
                unique: true,
                filter: "[Estado] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropuestasLlenado");
        }
    }
}
