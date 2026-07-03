using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CategoriasDeTemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoriaId",
                table: "TemasTicket",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CategoriasTicket",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasTicket", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TemasTicket_CategoriaId",
                table: "TemasTicket",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasTicket_Nombre",
                table: "CategoriasTicket",
                column: "Nombre",
                unique: true);

            // Siembra de categorías iniciales y mapeo de los temas base (solo si existen esos temas).
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [CategoriasTicket] ON;
INSERT INTO [CategoriasTicket] ([Id],[Nombre],[Activo],[CreatedAt]) VALUES
 (1, N'Plataforma SOL', 1, SYSUTCDATETIME()),
 (2, N'Accesos y permisos', 1, SYSUTCDATETIME()),
 (3, N'Formación y otros', 1, SYSUTCDATETIME());
SET IDENTITY_INSERT [CategoriasTicket] OFF;

UPDATE [TemasTicket] SET [CategoriaId] = 1 WHERE [Nombre] IN (N'Error en plataforma', N'Configuración', N'Datos');
UPDATE [TemasTicket] SET [CategoriaId] = 2 WHERE [Nombre] = N'Acceso';
UPDATE [TemasTicket] SET [CategoriaId] = 3 WHERE [Nombre] IN (N'Capacitación', N'Otro');");

            migrationBuilder.AddForeignKey(
                name: "FK_TemasTicket_CategoriasTicket_CategoriaId",
                table: "TemasTicket",
                column: "CategoriaId",
                principalTable: "CategoriasTicket",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TemasTicket_CategoriasTicket_CategoriaId",
                table: "TemasTicket");

            migrationBuilder.DropTable(
                name: "CategoriasTicket");

            migrationBuilder.DropIndex(
                name: "IX_TemasTicket_CategoriaId",
                table: "TemasTicket");

            migrationBuilder.DropColumn(
                name: "CategoriaId",
                table: "TemasTicket");
        }
    }
}
