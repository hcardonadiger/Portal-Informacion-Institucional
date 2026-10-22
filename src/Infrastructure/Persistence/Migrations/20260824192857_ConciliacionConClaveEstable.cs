using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Le da a cada trámite de expediente una identidad estable, y reapunta las decisiones de
    /// conciliación a esa identidad en vez de al Id de la fila.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Lo que arregla.</b> Guardar un expediente borra y reinserta todos sus trámites, y
    /// ConciliacionesSiger colgaba del Id de esa fila con borrado en cascada. Resultado: cada
    /// guardado se llevaba por delante las decisiones «descartado» y «proponer ficha nueva», y el
    /// trámite volvía a aparecer en la bandeja como si nadie lo hubiera revisado.
    /// </para>
    /// <para>
    /// <b>Por qué no se renombra la columna.</b> EF propuso renombrar <c>ExpedienteTramiteId</c> a
    /// <c>ExpedienteId</c>, que habría dejado ids de trámite haciéndose pasar por ids de
    /// expediente y apuntando la nueva llave foránea a expedientes equivocados. Se crean columnas
    /// nuevas y se rellenan cruzando contra ExpedienteTramites mientras la vieja todavía existe.
    /// </para>
    /// <para>
    /// <b>Por qué NEWID() como valor por defecto.</b> SQL Server evalúa el DEFAULT por fila al
    /// agregar la columna, así que cada trámite ya existente recibe una clave distinta. Con el
    /// <c>Guid.Empty</c> que ponía EF, las 240 filas habrían quedado idénticas y el índice único
    /// habría fallado.
    /// </para>
    /// </remarks>
    public partial class ConciliacionConClaveEstable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConciliacionesSiger_ExpedienteTramites_ExpedienteTramiteId",
                table: "ConciliacionesSiger");

            migrationBuilder.DropIndex(
                name: "IX_ConciliacionesSiger_ExpedienteTramiteId",
                table: "ConciliacionesSiger");

            // Cada fila existente recibe su propia clave: el DEFAULT se evalúa por fila.
            migrationBuilder.AddColumn<Guid>(
                name: "ClaveEstable",
                table: "ExpedienteTramites",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            // Nacen aceptando nulos para poder rellenarlas antes de exigirlas.
            migrationBuilder.AddColumn<Guid>(
                name: "ClaveTramite",
                table: "ConciliacionesSiger",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpedienteId",
                table: "ConciliacionesSiger",
                type: "int",
                nullable: true);

            // El cruce se hace mientras ExpedienteTramiteId sigue existiendo. Es el único momento
            // en que se puede saber a qué trámite pertenecía cada decisión.
            migrationBuilder.Sql(@"
                UPDATE c
                   SET c.ClaveTramite = t.ClaveEstable,
                       c.ExpedienteId = t.ExpedienteId
                  FROM ConciliacionesSiger c
                 INNER JOIN ExpedienteTramites t ON t.Id = c.ExpedienteTramiteId;");

            // Decisiones que ya apuntaban a un trámite inexistente. No se pueden reasignar a nada,
            // y dejarlas obligaría a mantener nulos en una columna que es la identidad de la fila.
            migrationBuilder.Sql(
                "DELETE FROM ConciliacionesSiger WHERE ClaveTramite IS NULL OR ExpedienteId IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClaveTramite",
                table: "ConciliacionesSiger",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ExpedienteId",
                table: "ConciliacionesSiger",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "ExpedienteTramiteId",
                table: "ConciliacionesSiger");

            migrationBuilder.CreateIndex(
                name: "IX_ExpedienteTramites_ClaveEstable",
                table: "ExpedienteTramites",
                column: "ClaveEstable",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConciliacionesSiger_ClaveTramite",
                table: "ConciliacionesSiger",
                column: "ClaveTramite",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConciliacionesSiger_ExpedienteId",
                table: "ConciliacionesSiger",
                column: "ExpedienteId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConciliacionesSiger_Expedientes_ExpedienteId",
                table: "ConciliacionesSiger",
                column: "ExpedienteId",
                principalTable: "Expedientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <summary>Revierte la identidad estable.</summary>
        /// <remarks>
        /// Reconstruye <c>ExpedienteTramiteId</c> cruzando por la clave, así que las decisiones
        /// sobreviven al viaje de vuelta. Lo que <b>no</b> sobrevive es el arreglo: al volver a
        /// colgar de la fila del trámite con borrado en cascada, el primer guardado de cada
        /// expediente vuelve a borrar sus decisiones.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConciliacionesSiger_Expedientes_ExpedienteId",
                table: "ConciliacionesSiger");

            migrationBuilder.DropIndex(
                name: "IX_ConciliacionesSiger_ClaveTramite",
                table: "ConciliacionesSiger");

            migrationBuilder.DropIndex(
                name: "IX_ConciliacionesSiger_ExpedienteId",
                table: "ConciliacionesSiger");

            migrationBuilder.AddColumn<int>(
                name: "ExpedienteTramiteId",
                table: "ConciliacionesSiger",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                UPDATE c
                   SET c.ExpedienteTramiteId = t.Id
                  FROM ConciliacionesSiger c
                 INNER JOIN ExpedienteTramites t ON t.ClaveEstable = c.ClaveTramite;");

            migrationBuilder.Sql("DELETE FROM ConciliacionesSiger WHERE ExpedienteTramiteId = 0;");

            migrationBuilder.DropColumn(
                name: "ClaveTramite",
                table: "ConciliacionesSiger");

            migrationBuilder.DropColumn(
                name: "ExpedienteId",
                table: "ConciliacionesSiger");

            migrationBuilder.DropIndex(
                name: "IX_ExpedienteTramites_ClaveEstable",
                table: "ExpedienteTramites");

            migrationBuilder.DropColumn(
                name: "ClaveEstable",
                table: "ExpedienteTramites");

            migrationBuilder.CreateIndex(
                name: "IX_ConciliacionesSiger_ExpedienteTramiteId",
                table: "ConciliacionesSiger",
                column: "ExpedienteTramiteId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ConciliacionesSiger_ExpedienteTramites_ExpedienteTramiteId",
                table: "ConciliacionesSiger",
                column: "ExpedienteTramiteId",
                principalTable: "ExpedienteTramites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
