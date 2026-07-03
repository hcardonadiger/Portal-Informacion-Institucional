using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReforzarIntegridadRelaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreadoPor",
                table: "Tickets",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreadoPorId",
                table: "Tickets",
                type: "int",
                nullable: true);

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
                name: "IX_Tickets_CreadoPorId",
                table: "Tickets",
                column: "CreadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_CreadoPorId",
                table: "Reuniones",
                column: "CreadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdoInstituciones_AcuerdoReunionId_InstitucionId",
                table: "AcuerdoInstituciones",
                columns: new[] { "AcuerdoReunionId", "InstitucionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdoInstituciones_InstitucionId",
                table: "AcuerdoInstituciones",
                column: "InstitucionId");

            // Saneo: anula referencias de creador que no correspondan a un usuario existente
            // (la columna se agregó antes sin FK), para poder crear la restricción.
            migrationBuilder.Sql(
                "UPDATE Reuniones SET CreadoPorId = NULL " +
                "WHERE CreadoPorId IS NOT NULL AND CreadoPorId NOT IN (SELECT Id FROM Usuarios);");

            migrationBuilder.AddForeignKey(
                name: "FK_Reuniones_Usuarios_CreadoPorId",
                table: "Reuniones",
                column: "CreadoPorId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Usuarios_CreadoPorId",
                table: "Tickets",
                column: "CreadoPorId",
                principalTable: "Usuarios",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reuniones_Usuarios_CreadoPorId",
                table: "Reuniones");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Usuarios_CreadoPorId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "AcuerdoInstituciones");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_CreadoPorId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Reuniones_CreadoPorId",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "CreadoPor",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CreadoPorId",
                table: "Tickets");
        }
    }
}
