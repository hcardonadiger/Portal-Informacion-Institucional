using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// La prioridad del ticket deja de ser un enum guardado como texto y pasa a ser una llave
    /// foránea al catálogo administrable PrioridadesTicket. Mismo criterio que la migración de
    /// prioridades de proyecto: la columna vieja se borra al final, cuando su contenido ya viajó.
    ///
    /// <para>La diferencia es la marca <c>EsCritica</c>. Los tableros cuentan «tickets críticos»
    /// y esa cuenta estaba atada al miembro <c>Critica</c> del enum; con el nombre editable, la
    /// condición no puede depender de cómo se llame la fila. La semilla marca «Crítica», y quien
    /// administre puede marcar otras.</para>
    /// </summary>
    public partial class CatalogoDePrioridadesDeTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrioridadesTicket",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EsCritica = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EsPredeterminada = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrioridadesTicket", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrioridadesTicket_Nombre",
                table: "PrioridadesTicket",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrioridadesTicket_Orden",
                table: "PrioridadesTicket",
                column: "Orden");

            // El orden se invierte respecto del enum (Baja=1 … Critica=4): acá el 1 es el que va
            // primero en pantalla, y lo urgente va primero. El nombre se conserva —tildes
            // incluidas, «Critica» era el identificador de C#, no el rótulo— para que el
            // traslado de abajo reconozca los tickets que ya existen.
            // Dentro de EXEC por lo mismo que la migración de prioridades de proyecto: el script
            // de producción es un solo lote y se compila entero antes de ejecutarse, así que ni
            // la tabla ni la columna nuevas existen todavía. Ver la nota extensa allá.
            migrationBuilder.Sql(@"
EXEC(N'
INSERT INTO PrioridadesTicket (Nombre, Orden, Color, EsCritica, EsPredeterminada, Activo, CreatedAt, CreatedBy)
VALUES (''Critica'', 1, ''Rojo'',    1, 0, 1, SYSUTCDATETIME(), ''migracion''),
       (''Alta'',    2, ''Naranja'', 0, 0, 1, SYSUTCDATETIME(), ''migracion''),
       (''Media'',   3, ''Azul'',    0, 1, 1, SYSUTCDATETIME(), ''migracion''),
       (''Baja'',    4, ''Gris'',    0, 0, 1, SYSUTCDATETIME(), ''migracion'');
');");

            migrationBuilder.AddColumn<int>(
                name: "PrioridadId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
EXEC(N'
UPDATE t
SET    t.PrioridadId = c.Id
FROM   Tickets t
JOIN   PrioridadesTicket c ON c.Nombre = t.Prioridad;
');");

            migrationBuilder.Sql(@"
EXEC(N'
UPDATE Tickets
SET    PrioridadId = (SELECT TOP 1 Id FROM PrioridadesTicket WHERE EsPredeterminada = 1)
WHERE  PrioridadId IS NULL;
');");

            migrationBuilder.AlterColumn<int>(
                name: "PrioridadId",
                table: "Tickets",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_PrioridadId",
                table: "Tickets",
                column: "PrioridadId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_PrioridadesTicket_PrioridadId",
                table: "Tickets",
                column: "PrioridadId",
                principalTable: "PrioridadesTicket",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Hasta acá no se toca: si algo de lo anterior falla, la prioridad sigue estando.
            migrationBuilder.DropColumn(
                name: "Prioridad",
                table: "Tickets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Prioridad",
                table: "Tickets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Una prioridad creada después de esta migración no cabe en el enum de vuelta y
            // aterriza en Media: revertir pierde información.
            migrationBuilder.Sql(@"
EXEC(N'
UPDATE t
SET    t.Prioridad = CASE WHEN c.Nombre IN (''Baja'',''Media'',''Alta'',''Critica'') THEN c.Nombre ELSE ''Media'' END
FROM   Tickets t
JOIN   PrioridadesTicket c ON c.Id = t.PrioridadId;
');");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_PrioridadesTicket_PrioridadId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_PrioridadId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "PrioridadId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "PrioridadesTicket");
        }
    }
}
