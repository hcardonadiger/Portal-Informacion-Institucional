using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Convierte a catálogo cerrado las modalidades de texto libre que ya existen, y recién
    /// entonces pone el CHECK.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El orden es el punto de esta migración.</b> La anterior agregó las columnas pero no la
    /// restricción, a propósito: en Ensayo hay 202 trámites con la modalidad escrita a mano y
    /// ninguno de esos valores cumple el catálogo. Puesta antes, la migración habría fallado al
    /// aplicarse sobre cualquier base con datos.
    /// </para>
    /// <para>
    /// Medido en Ensayo el 25-08-2026, son ocho variantes sobre 240 trámites:
    /// «En línea» (166), «En línea, Presencial» (14), «En línea (total)» (12), «Presencial» (3),
    /// «Trámite en línea» (3), «En línea / Presencial» (2), «En línea Tipo de solicitud» (1) y
    /// «En linea» (1) —esta última sin tilde—. Todas caen en el catálogo sin ambigüedad.
    /// </para>
    /// </remarks>
    public partial class ConvertirModalidadesExistentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Primero se guarda el texto íntegro, y solo después se normaliza. «En línea (total)»
            // y «En línea Tipo de solicitud» llevan matiz que el catálogo cerrado pierde, y una
            // vez convertido no hay forma de recuperarlo.
            //
            // No se pisa lo que ya tenga ModalidadDetalle: la columna es nueva y hoy está vacía
            // en todas las filas, pero si esta migración se reaplicara sobre una base donde
            // alguien ya capturó el detalle, sobrescribirlo sería perder trabajo humano.
            // Va dentro de EXEC a propósito, y no es cosmética.
            //
            // Al desplegar, esta migración y la que agrega ModalidadDetalle viajan en el MISMO
            // lote del script de producción. SQL Server analiza el lote entero antes de ejecutar
            // nada, y una columna que aún no existe no supera ese análisis: el script falla con
            // «Msg 207, Invalid column name 'ModalidadDetalle'» aunque la columna se cree tres
            // instrucciones más arriba. Dentro de EXEC el texto se compila al ejecutarse, cuando
            // la columna ya está.
            //
            // No lo atrapa «dotnet ef database update», que manda cada migración por separado:
            // solo aparece al correr el .sql de despliegue. Por eso ese script se prueba.
            migrationBuilder.Sql(@"
                EXEC(N'
                    UPDATE ExpedienteTramites
                    SET    ModalidadDetalle = Modalidad
                    WHERE  Modalidad IS NOT NULL
                      AND  LTRIM(RTRIM(Modalidad)) <> ''''
                      AND  ModalidadDetalle IS NULL;');");

            // Mismo criterio que ModalidadNormalizador, que es quien decide de acá en adelante:
            // se compara sin tildes y sin distinguir mayúsculas, porque en la base conviven
            // «En línea» y «En linea» y tratarlas distinto dejaría una ficha sin modalidad por
            // una tilde. Lo que no se reconoce queda en NULL en vez de adivinarse: una ficha sin
            // modalidad se declara incompleta y alguien la revisa, que es mejor que publicar una
            // modalidad equivocada.
            migrationBuilder.Sql(@"
                UPDATE ExpedienteTramites
                SET    Modalidad = CASE
                         WHEN Modalidad COLLATE Latin1_General_CI_AI IN ('Virtual', 'Presencial', 'Hibrido')
                              THEN Modalidad
                         WHEN (Modalidad COLLATE Latin1_General_CI_AI LIKE '%linea%'
                            OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%virtual%'
                            OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%online%')
                          AND  Modalidad COLLATE Latin1_General_CI_AI LIKE '%presencial%'
                              THEN 'Hibrido'
                         WHEN  Modalidad COLLATE Latin1_General_CI_AI LIKE '%linea%'
                            OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%virtual%'
                            OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%online%'
                              THEN 'Virtual'
                         WHEN  Modalidad COLLATE Latin1_General_CI_AI LIKE '%presencial%'
                              THEN 'Presencial'
                         ELSE NULL
                       END
                WHERE  Modalidad IS NOT NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExpedienteTramites_Modalidad",
                table: "ExpedienteTramites",
                sql: "[Modalidad] IS NULL OR [Modalidad] IN ('Virtual', 'Presencial', 'Hibrido')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ExpedienteTramites_Modalidad",
                table: "ExpedienteTramites");

            // Se devuelve el texto original. Hay que quitar el CHECK primero, porque lo que
            // vuelve no cumple el catálogo —es justo el motivo por el que se guardó aparte—.
            migrationBuilder.Sql(@"
                UPDATE ExpedienteTramites
                SET    Modalidad = ModalidadDetalle
                WHERE  ModalidadDetalle IS NOT NULL;");
        }
    }
}
