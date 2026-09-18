using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// La prioridad del proyecto deja de ser un enum guardado como texto y pasa a ser una llave
    /// foránea al catálogo administrable PrioridadesProyecto.
    ///
    /// <para>El orden de los pasos importa y no es el que genera el andamiaje: EF propone borrar
    /// la columna vieja antes de crear la nueva, lo que tiraría la prioridad de todos los
    /// proyectos y dejaría la llave foránea apuntando a un Id 0 que no existe. Acá la columna
    /// vieja se borra <b>al final</b>, cuando su contenido ya se trasladó.</para>
    /// </summary>
    public partial class CatalogoDePrioridadesDeProyecto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrioridadesProyecto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EsPredeterminada = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrioridadesProyecto", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrioridadesProyecto_Nombre",
                table: "PrioridadesProyecto",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrioridadesProyecto_Orden",
                table: "PrioridadesProyecto",
                column: "Orden");

            // Las tres que existían como enum, con el mismo nombre para que el traslado de abajo
            // las reconozca, más la Q3 que motivó el catálogo. Media queda de predeterminada
            // porque era el valor por defecto del enum.
            migrationBuilder.Sql(@"
INSERT INTO PrioridadesProyecto (Nombre, Orden, Color, EsPredeterminada, Activo, CreatedAt, CreatedBy)
VALUES ('Alta',  1, 'Naranja', 0, 1, SYSUTCDATETIME(), 'migracion'),
       ('Media', 2, 'Azul',    1, 1, SYSUTCDATETIME(), 'migracion'),
       ('Baja',  3, 'Gris',    0, 1, SYSUTCDATETIME(), 'migracion'),
       ('Q3',    4, 'Verde',   0, 1, SYSUTCDATETIME(), 'migracion');");

            // Nace aceptando nulos: hay que rellenarla antes de poder exigirla.
            migrationBuilder.AddColumn<int>(
                name: "PrioridadId",
                table: "Proyectos",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE p
SET    p.PrioridadId = c.Id
FROM   Proyectos p
JOIN   PrioridadesProyecto c ON c.Nombre = p.Prioridad;");

            // Lo que no casó con ninguna fila —nulo, vacío, o un texto que nadie reconoce— va a
            // la predeterminada. Es preferible a dejarlo en nulo: la columna tiene que quedar
            // obligatoria y un proyecto sin prioridad no se puede listar.
            migrationBuilder.Sql(@"
UPDATE Proyectos
SET    PrioridadId = (SELECT TOP 1 Id FROM PrioridadesProyecto WHERE EsPredeterminada = 1)
WHERE  PrioridadId IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "PrioridadId",
                table: "Proyectos",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Proyectos_PrioridadId",
                table: "Proyectos",
                column: "PrioridadId");

            migrationBuilder.AddForeignKey(
                name: "FK_Proyectos_PrioridadesProyecto_PrioridadId",
                table: "Proyectos",
                column: "PrioridadId",
                principalTable: "PrioridadesProyecto",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Hasta acá no se toca: si algo de lo anterior falla, la prioridad sigue estando.
            migrationBuilder.DropColumn(
                name: "Prioridad",
                table: "Proyectos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Prioridad",
                table: "Proyectos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Devuelve el nombre al texto. Una prioridad creada después de esta migración —«Q3»,
            // o cualquiera que agreguen— no cabe en el enum de vuelta, así que aterriza en Media:
            // revertir pierde información, y es la razón por la que esto no es un camino de ida
            // y vuelta gratis.
            migrationBuilder.Sql(@"
UPDATE p
SET    p.Prioridad = CASE WHEN c.Nombre IN ('Alta','Media','Baja') THEN c.Nombre ELSE 'Media' END
FROM   Proyectos p
JOIN   PrioridadesProyecto c ON c.Id = p.PrioridadId;");

            migrationBuilder.DropForeignKey(
                name: "FK_Proyectos_PrioridadesProyecto_PrioridadId",
                table: "Proyectos");

            migrationBuilder.DropIndex(
                name: "IX_Proyectos_PrioridadId",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "PrioridadId",
                table: "Proyectos");

            migrationBuilder.DropTable(
                name: "PrioridadesProyecto");
        }
    }
}
