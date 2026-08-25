using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Publica «ficha completa» como una columna de la base, para que la API pública pueda
    /// servirla sin conocer la regla que la decide.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué existe esta columna.</b> Hasta hoy la API calculaba la completitud por su
    /// cuenta, con una copia en C# de la regla y otra en SQL dentro de su consulta. Eso ataba los
    /// dos sistemas: agregar un campo obligatorio en PortalDigital obligaba a tocar la API y a
    /// desplegarla. La columna invierte la relación — <b>PortalDigital decide, la API solo
    /// lee</b> — y con ella la API deja de referenciar el código de PortalDigital.
    /// </para>
    /// <para>
    /// <b>Por qué la calcula la base y no la aplicación.</b> Una columna que alguien tiene que
    /// acordarse de recalcular al guardar es una columna que tarde o temprano miente: basta un
    /// camino de escritura que la olvide —una carga masiva, un UPDATE directo, una importación—
    /// para que quede desfasada, y nadie lo nota hasta que el ciudadano ve una ficha a medias.
    /// Calculada por SQL Server no existe ese camino: no hay forma de escribirla mal porque no
    /// hay forma de escribirla.
    /// </para>
    /// <para>
    /// <b>PERSISTED y no virtual</b> porque el filtro <c>?soloFichasCompletas=true</c> la usa en
    /// un WHERE. Sin persistir, SQL Server reevaluaría la expresión fila por fila en cada
    /// consulta del catálogo.
    /// </para>
    /// <para>
    /// <b>Su gemela en C# es <c>FichaPublicaCompletitud.CamposFaltantes</c></b>, y las dos tienen
    /// que decir lo mismo. La de C# sigue viva porque responde algo que un booleano no puede:
    /// <i>qué</i> falta, que es lo que el editor le enseña al técnico. Las dos viven ahora en
    /// PortalDigital —antes una de ellas vivía en la API— y <c>16-verificar-ficha-completa.sql</c>
    /// las contrasta contra la misma tabla de verdad.
    /// </para>
    /// <para>
    /// <b>La columna no se mapea en <c>AppDbContext</c>, y es deliberado.</b> Un proveedor
    /// distinto al de SQL Server —los de las pruebas— no sabe crear columnas calculadas con esta
    /// expresión. Como PortalDigital no necesita leerla (para eso tiene la regla en C#), dejarla
    /// fuera del modelo evita romper las pruebas sin perder nada. EF no la conoce, así que
    /// tampoco la va a borrar en una migración futura.
    /// </para>
    /// </remarks>
    public partial class ColumnaFichaCompleta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La comparación es contra NULL y no contra cadena vacía, igual que la gemela en C#:
            // un texto en blanco cuenta como capturado. Apretar el criterio solo de un lado haría
            // que la alerta del editor y el catálogo público discreparan en silencio.
            //
            // El costo se decide por CostoEsGratuito y no por CostoTexto: «es gratuito» ya es una
            // respuesta completa aunque no haya monto que escribir.
            //
            // El enlace a SOL solo se exige cuando EstaEnSol; y vale cualquiera de los dos —el
            // tramo nuevo o la URL heredada de antes de la Fase 7—.
            migrationBuilder.Sql(@"
                ALTER TABLE TramitesSiger ADD FichaCompleta AS (
                    CASE WHEN CategoriaId      IS NOT NULL
                          AND Modalidad        IS NOT NULL
                          AND TiempoTexto      IS NOT NULL
                          AND CostoEsGratuito  IS NOT NULL
                          AND (EstaEnSol = 0 OR SolUrl IS NOT NULL OR SolTramo IS NOT NULL)
                         THEN CAST(1 AS bit)
                         ELSE CAST(0 AS bit)
                    END
                ) PERSISTED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE TramitesSiger DROP COLUMN FichaCompleta;");
        }
    }
}
