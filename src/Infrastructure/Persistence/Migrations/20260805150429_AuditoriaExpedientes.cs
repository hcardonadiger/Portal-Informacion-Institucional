using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditoriaExpedientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaHoraValidacionDiger",
                table: "Expedientes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaHoraValidacionInst",
                table: "Expedientes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ValidadoDigerUsuarioId",
                table: "Expedientes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ValidadoInstUsuarioId",
                table: "Expedientes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BitacoraExpediente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BitacoraExpediente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BitacoraExpediente_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BitacoraExpediente_ExpedienteId_Fecha",
                table: "BitacoraExpediente",
                columns: new[] { "ExpedienteId", "Fecha" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BitacoraExpediente");

            migrationBuilder.DropColumn(
                name: "FechaHoraValidacionDiger",
                table: "Expedientes");

            migrationBuilder.DropColumn(
                name: "FechaHoraValidacionInst",
                table: "Expedientes");

            migrationBuilder.DropColumn(
                name: "ValidadoDigerUsuarioId",
                table: "Expedientes");

            migrationBuilder.DropColumn(
                name: "ValidadoInstUsuarioId",
                table: "Expedientes");
        }
    }
}
