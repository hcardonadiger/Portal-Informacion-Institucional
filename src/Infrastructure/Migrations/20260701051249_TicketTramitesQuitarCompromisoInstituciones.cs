using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TicketTramitesQuitarCompromisoInstituciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcuerdoInstituciones");

            migrationBuilder.CreateTable(
                name: "TicketTramites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    TramiteDefinicionId = table.Column<int>(type: "int", nullable: true),
                    Tramite = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTramites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketTramites_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketTramites_TicketId",
                table: "TicketTramites",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketTramites");

            migrationBuilder.CreateTable(
                name: "AcuerdoInstituciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AcuerdoReunionId = table.Column<int>(type: "int", nullable: false),
                    InstitucionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcuerdoInstituciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcuerdoInstituciones_AcuerdosReunion_AcuerdoReunionId",
                        column: x => x.AcuerdoReunionId,
                        principalTable: "AcuerdosReunion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AcuerdoInstituciones_Instituciones_InstitucionId",
                        column: x => x.InstitucionId,
                        principalTable: "Instituciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdoInstituciones_AcuerdoReunionId_InstitucionId",
                table: "AcuerdoInstituciones",
                columns: new[] { "AcuerdoReunionId", "InstitucionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdoInstituciones_InstitucionId",
                table: "AcuerdoInstituciones",
                column: "InstitucionId");
        }
    }
}
