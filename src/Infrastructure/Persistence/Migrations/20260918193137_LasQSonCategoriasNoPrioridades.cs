using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Mueve las «Q» de donde no iban. La primera migración de prioridades sembró «Q3» como una
    /// prioridad más, junto a Alta, Media y Baja, porque así se planteó el pedido al inicio. Con el
    /// catálogo de categorías ya en pie quedó claro que Q1, Q2 y Q3 son <b>categorías</b> —tomadas
    /// de las tres rondas de clasificación de la Fórmula 1— y no niveles de urgencia.
    ///
    /// <para>El orden sigue al de la F1: Q3 es la ronda final, la de los más rápidos, así que va
    /// primero. Los colores no usan el rojo a propósito: en el listado cada proyecto ya muestra su
    /// insignia de prioridad, y una categoría en rojo al lado haría que toda la fila se leyera como
    /// una alarma. Verde, azul y gris separan las dos escalas de un vistazo. Se cambian en dos
    /// clics desde Catálogos › Categorías de proyectos.</para>
    ///
    /// <para>Todo va con guardas: la baja de la prioridad solo ocurre si nadie la usa —y si alguien
    /// la usara, la llave foránea lo impediría de todos modos— y las altas comprueban que el nombre
    /// no exista. Así esta migración es inofensiva en una base donde ya se haya hecho a mano.</para>
    /// </summary>
    public partial class LasQSonCategoriasNoPrioridades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Q3 sale de prioridades. El NOT EXISTS mira también los proyectos borrados
            // lógicamente: siguen apuntando a la fila y la llave foránea no distingue.
            migrationBuilder.Sql(@"
DELETE FROM PrioridadesProyecto
WHERE  Nombre = 'Q3'
  AND  NOT EXISTS (SELECT 1 FROM Proyectos p WHERE p.PrioridadId = PrioridadesProyecto.Id);");

            migrationBuilder.Sql(@"
INSERT INTO CategoriasProyecto (Nombre, Orden, Color, Activo, CreatedAt, CreatedBy)
SELECT v.Nombre, v.Orden, v.Color, 1, SYSUTCDATETIME(), 'migracion'
FROM   (VALUES
           ('Q3', 1, 'Verde'),
           ('Q2', 2, 'Azul'),
           ('Q1', 3, 'Gris')
       ) AS v(Nombre, Orden, Color)
WHERE  NOT EXISTS (SELECT 1 FROM CategoriasProyecto c WHERE c.Nombre = v.Nombre);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revertir devuelve Q3 a prioridades y retira las tres categorías, salvo las que
            // algún proyecto ya esté usando: esas se dejan, porque borrarlas perdería la
            // clasificación de quien sí alcanzó a hacerla.
            migrationBuilder.Sql(@"
DELETE FROM CategoriasProyecto
WHERE  Nombre IN ('Q1', 'Q2', 'Q3')
  AND  NOT EXISTS (SELECT 1 FROM Proyectos p WHERE p.CategoriaId = CategoriasProyecto.Id);");

            migrationBuilder.Sql(@"
INSERT INTO PrioridadesProyecto (Nombre, Orden, Color, EsPredeterminada, Activo, CreatedAt, CreatedBy)
SELECT 'Q3', 4, 'Verde', 0, 1, SYSUTCDATETIME(), 'migracion'
WHERE  NOT EXISTS (SELECT 1 FROM PrioridadesProyecto WHERE Nombre = 'Q3');");
        }
    }
}
