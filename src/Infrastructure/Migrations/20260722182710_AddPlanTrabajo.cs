using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanTrabajo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanTrabajo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    InstitucionId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Institucion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Anio = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AprobadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FechaAprobacion = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanTrabajo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanTrabajoMetas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanTrabajoId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    NombreTramite = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FechaEstimadaInicio = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaEstimadaFin = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaRealFin = table.Column<DateOnly>(type: "date", nullable: true),
                    Responsable = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExpedienteId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanTrabajoMetas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanTrabajoMetas_PlanTrabajo_PlanTrabajoId",
                        column: x => x.PlanTrabajoId,
                        principalTable: "PlanTrabajo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanTrabajo_InstitucionId_Anio",
                table: "PlanTrabajo",
                columns: new[] { "InstitucionId", "Anio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanTrabajoMetas_PlanTrabajoId_Orden",
                table: "PlanTrabajoMetas",
                columns: new[] { "PlanTrabajoId", "Orden" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanTrabajoMetas");

            migrationBuilder.DropTable(
                name: "PlanTrabajo");
        }
    }
}
