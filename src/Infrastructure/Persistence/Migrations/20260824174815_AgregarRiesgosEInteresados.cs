using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRiesgosEInteresados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProyectoInteresados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProyectoId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Institucion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Cargo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Rol = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Influencia = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RegistradoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RegistradoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectoInteresados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProyectoInteresados_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProyectoRiesgos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProyectoId = table.Column<int>(type: "int", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Probabilidad = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Impacto = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Estrategia = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Mitigacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResponsableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Responsable = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FechaDeteccion = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaRevision = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaCierre = table.Column<DateOnly>(type: "date", nullable: true),
                    RegistradoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RegistradoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectoRiesgos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProyectoRiesgos_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoInteresados_ProyectoId_Rol",
                table: "ProyectoInteresados",
                columns: new[] { "ProyectoId", "Rol" });

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoRiesgos_ProyectoId_Estado",
                table: "ProyectoRiesgos",
                columns: new[] { "ProyectoId", "Estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProyectoInteresados");

            migrationBuilder.DropTable(
                name: "ProyectoRiesgos");
        }
    }
}
