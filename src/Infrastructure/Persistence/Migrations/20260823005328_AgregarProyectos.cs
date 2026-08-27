using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarProyectos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Proyectos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Objetivo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AreaId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ResponsableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Responsable = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Prioridad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FechaInicioPlan = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaFinPlan = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaInicioReal = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaFinReal = table.Column<DateOnly>(type: "date", nullable: true),
                    AvancePct = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proyectos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProyectoHitos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProyectoId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FechaPlan = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaReal = table.Column<DateOnly>(type: "date", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ResponsableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Responsable = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectoHitos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProyectoHitos_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProyectoAvances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProyectoId = table.Column<int>(type: "int", nullable: false),
                    HitoId = table.Column<int>(type: "int", nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Autor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PorcentajeReportado = table.Column<int>(type: "int", nullable: false),
                    Bloqueo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ArchivoNombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ArchivoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ArchivoTamano = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectoAvances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProyectoAvances_ProyectoHitos_HitoId",
                        column: x => x.HitoId,
                        principalTable: "ProyectoHitos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProyectoAvances_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoAvances_HitoId",
                table: "ProyectoAvances",
                column: "HitoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoAvances_ProyectoId_Fecha",
                table: "ProyectoAvances",
                columns: new[] { "ProyectoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoHitos_ProyectoId_Orden",
                table: "ProyectoHitos",
                columns: new[] { "ProyectoId", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_Proyectos_Codigo",
                table: "Proyectos",
                column: "Codigo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Proyectos_Estado_FechaFinPlan",
                table: "Proyectos",
                columns: new[] { "Estado", "FechaFinPlan" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProyectoAvances");

            migrationBuilder.DropTable(
                name: "ProyectoHitos");

            migrationBuilder.DropTable(
                name: "Proyectos");
        }
    }
}
