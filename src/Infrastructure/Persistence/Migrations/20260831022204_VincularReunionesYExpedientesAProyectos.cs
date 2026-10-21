using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VincularReunionesYExpedientesAProyectos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProyectoExpedientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProyectoId = table.Column<int>(type: "int", nullable: false),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    Nota = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    VinculadoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VinculadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectoExpedientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProyectoExpedientes_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProyectoExpedientes_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProyectoReuniones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProyectoId = table.Column<int>(type: "int", nullable: false),
                    ReunionId = table.Column<int>(type: "int", nullable: false),
                    Nota = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    VinculadoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VinculadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectoReuniones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProyectoReuniones_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProyectoReuniones_Reuniones_ReunionId",
                        column: x => x.ReunionId,
                        principalTable: "Reuniones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoExpedientes_ExpedienteId",
                table: "ProyectoExpedientes",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoExpedientes_ProyectoId_ExpedienteId",
                table: "ProyectoExpedientes",
                columns: new[] { "ProyectoId", "ExpedienteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoReuniones_ProyectoId_ReunionId",
                table: "ProyectoReuniones",
                columns: new[] { "ProyectoId", "ReunionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoReuniones_ReunionId",
                table: "ProyectoReuniones",
                column: "ReunionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProyectoExpedientes");

            migrationBuilder.DropTable(
                name: "ProyectoReuniones");
        }
    }
}
