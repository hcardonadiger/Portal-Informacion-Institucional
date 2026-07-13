using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlantillaTramite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsPersonalizado",
                table: "TramiteRequisitos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PlantillaOrigenId",
                table: "TramiteRequisitos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EsPersonalizado",
                table: "FundamentosLegales",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PlantillaOrigenId",
                table: "FundamentosLegales",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlantillasTramite",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantillasTramite", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlantillaFundamentosLegales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlantillaId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Instrumento = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Articulos = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Obs = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantillaFundamentosLegales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlantillaFundamentosLegales_PlantillasTramite_PlantillaId",
                        column: x => x.PlantillaId,
                        principalTable: "PlantillasTramite",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlantillaRequisitos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlantillaId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Requisito = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Obs = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantillaRequisitos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlantillaRequisitos_PlantillasTramite_PlantillaId",
                        column: x => x.PlantillaId,
                        principalTable: "PlantillasTramite",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlantillaFundamentosLegales_PlantillaId",
                table: "PlantillaFundamentosLegales",
                column: "PlantillaId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantillaRequisitos_PlantillaId",
                table: "PlantillaRequisitos",
                column: "PlantillaId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasTramite_Nombre",
                table: "PlantillasTramite",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlantillaFundamentosLegales");

            migrationBuilder.DropTable(
                name: "PlantillaRequisitos");

            migrationBuilder.DropTable(
                name: "PlantillasTramite");

            migrationBuilder.DropColumn(
                name: "EsPersonalizado",
                table: "TramiteRequisitos");

            migrationBuilder.DropColumn(
                name: "PlantillaOrigenId",
                table: "TramiteRequisitos");

            migrationBuilder.DropColumn(
                name: "EsPersonalizado",
                table: "FundamentosLegales");

            migrationBuilder.DropColumn(
                name: "PlantillaOrigenId",
                table: "FundamentosLegales");
        }
    }
}
