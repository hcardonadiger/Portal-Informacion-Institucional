using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConciliacionSiger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConciliacionesSiger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteTramiteId = table.Column<int>(type: "int", nullable: false),
                    TramiteSigerId = table.Column<int>(type: "int", nullable: true),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Nota = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConciliacionesSiger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConciliacionesSiger_ExpedienteTramites_ExpedienteTramiteId",
                        column: x => x.ExpedienteTramiteId,
                        principalTable: "ExpedienteTramites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConciliacionesSiger_TramitesSiger_TramiteSigerId",
                        column: x => x.TramiteSigerId,
                        principalTable: "TramitesSiger",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConciliacionesSiger_ExpedienteTramiteId",
                table: "ConciliacionesSiger",
                column: "ExpedienteTramiteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConciliacionesSiger_TramiteSigerId",
                table: "ConciliacionesSiger",
                column: "TramiteSigerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConciliacionesSiger");
        }
    }
}
