using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Llena las dos tablas nuevas con lo que el expediente ya sabía, para que nadie tenga que
    /// volver a teclear 202 entregables y 197 teléfonos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El expediente nunca tuvo listas de entregables ni de sedes, pero sí guardaba las mismas
    /// cosas en campos sueltos: un <c>DocEntregado</c> por trámite, un <c>Telefono</c> por
    /// trámite y una <c>DirSede</c> por expediente. Esta migración los convierte en la primera
    /// fila de cada lista.
    /// </para>
    /// <para>
    /// <b>Va junto con la pantalla que enseña esas listas, y no antes.</b> Guardar un expediente
    /// borra y reinserta todos sus hijos desde el formulario; si lo sembrado no se pintara,
    /// el primer guardado de cada expediente lo borraría sin que nadie se enterara.
    /// </para>
    /// <para>
    /// <b>Lo que no se siembra:</b> el <c>Horario</c> del trámite. Un lugar de atención de SIGER
    /// no tiene dónde guardarlo —sus campos son lugar, ciudad, dirección y teléfonos—, así que
    /// meterlo en la dirección lo corrompería. Se queda donde está, en el trámite del expediente.
    /// </para>
    /// </remarks>
    public partial class SembrarEntregablesYLugares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DocEntregado es un texto suelto por trámite (202 de 240 lo tienen en Ensayo).
            // Pasa a ser el primer entregable de la lista.
            migrationBuilder.Sql(@"
                INSERT INTO ExpedienteTramiteEntregables (ExpedienteId, TramiteIndex, Orden, Entregable)
                SELECT ExpedienteId, TramiteIndex, 0, LTRIM(RTRIM(DocEntregado))
                FROM   ExpedienteTramites
                WHERE  DocEntregado IS NOT NULL AND LTRIM(RTRIM(DocEntregado)) <> '';");

            // Se arma un lugar único por trámite con el teléfono del trámite y la dirección de
            // sede del expediente. El nombre sale de la institución porque no hay ningún campo
            // que nombre la sede, y la columna es obligatoria: dejarla vacía no es opción.
            migrationBuilder.Sql(@"
                INSERT INTO ExpedienteTramiteLugares
                       (ExpedienteId, TramiteIndex, Orden, Lugar, Direccion, Telefonos)
                SELECT t.ExpedienteId, t.TramiteIndex, 0,
                       e.Institucion, NULLIF(LTRIM(RTRIM(e.DirSede)), ''), NULLIF(LTRIM(RTRIM(t.Telefono)), '')
                FROM   ExpedienteTramites t
                JOIN   Expedientes e ON e.Id = t.ExpedienteId
                WHERE  LTRIM(RTRIM(ISNULL(e.Institucion, ''))) <> ''
                  AND  ((t.Telefono IS NOT NULL AND LTRIM(RTRIM(t.Telefono)) <> '')
                    OR  (e.DirSede  IS NOT NULL AND LTRIM(RTRIM(e.DirSede))  <> ''));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo se borra lo que sigue siendo idéntico a lo que esta migración escribió.
            //
            // Borrar por «Orden = 0» —que es lo primero que uno escribiría— se llevaría por
            // delante la primera fila de cualquier lista que alguien haya capturado a mano
            // después, y esa fila no la puso esta migración. Revertir un sembrado nunca debe
            // costar trabajo humano.
            migrationBuilder.Sql(@"
                DELETE g
                FROM   ExpedienteTramiteEntregables g
                JOIN   ExpedienteTramites t
                       ON t.ExpedienteId = g.ExpedienteId AND t.TramiteIndex = g.TramiteIndex
                WHERE  g.Orden = 0
                  AND  g.Formato IS NULL AND g.Presentacion IS NULL
                  AND  g.Entregable = LTRIM(RTRIM(t.DocEntregado));");

            migrationBuilder.Sql(@"
                DELETE l
                FROM   ExpedienteTramiteLugares l
                JOIN   ExpedienteTramites t
                       ON t.ExpedienteId = l.ExpedienteId AND t.TramiteIndex = l.TramiteIndex
                JOIN   Expedientes e ON e.Id = l.ExpedienteId
                WHERE  l.Orden = 0
                  AND  l.Ciudad IS NULL
                  AND  l.Lugar = e.Institucion
                  AND  ISNULL(l.Direccion, '') = ISNULL(NULLIF(LTRIM(RTRIM(e.DirSede)), ''), '')
                  AND  ISNULL(l.Telefonos, '') = ISNULL(NULLIF(LTRIM(RTRIM(t.Telefono)), ''), '');");
        }
    }
}
