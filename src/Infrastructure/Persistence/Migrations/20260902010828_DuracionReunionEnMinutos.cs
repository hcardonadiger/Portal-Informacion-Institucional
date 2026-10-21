using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Convierte la duración de la reunión de texto libre («2 horas») a minutos.
    ///
    /// <para>Motivo: una reunión sin hora de fin no se puede publicar en ningún calendario. El campo
    /// viejo, además, estaba lleno en 1 de 45 filas y con el valor «1», que no dice si es una hora o
    /// un minuto.</para>
    ///
    /// <para>El orden importa: se agrega la columna nueva, se traduce lo que había y recién entonces
    /// se borra la vieja. La migración que generó <c>dotnet ef</c> hacía el DROP primero, lo que
    /// habría tirado el dato.</para>
    /// </summary>
    public partial class DuracionReunionEnMinutos : Migration
    {
        /// <summary>
        /// Extrae el número que encabeza el texto. Devuelve NULL si no empieza con dígitos, que es
        /// la señal de «no se pudo interpretar» y deja la duración sin declarar en vez de inventarla.
        /// </summary>
        private const string NumeroInicial = @"
            TRY_CONVERT(decimal(9,2),
                NULLIF(LEFT(t, PATINDEX('%[^0-9.]%', t + '|') - 1), ''))";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DuracionMinutos",
                table: "Reuniones",
                type: "int",
                nullable: true);

            // Regla de interpretación, la misma que aplica el editor al pegar un JSON de Cowork
            // (ver duracionAMinutos en Reuniones/Editor.cshtml): si el texto menciona minutos, el
            // número son minutos; si menciona horas, son horas; y un número suelto de 12 o menos se
            // lee como horas —«2» es «2 horas», que es lo que sugería el placeholder del campo—.
            migrationBuilder.Sql($@"
                WITH origen AS (
                    SELECT  Id,
                            REPLACE(LOWER(LTRIM(RTRIM(Duracion))), ',', '.') AS t
                    FROM    Reuniones
                    WHERE   Duracion IS NOT NULL AND LTRIM(RTRIM(Duracion)) <> ''
                ),
                interpretado AS (
                    SELECT  Id, t, {NumeroInicial} AS num
                    FROM    origen
                )
                UPDATE  r
                SET     r.DuracionMinutos =
                            CASE
                                WHEN i.num IS NULL           THEN NULL
                                WHEN i.t LIKE '%min%'        THEN CAST(ROUND(i.num, 0) AS int)
                                WHEN i.t LIKE '%h%'          THEN CAST(ROUND(i.num * 60, 0) AS int)
                                WHEN i.num <= 12             THEN CAST(ROUND(i.num * 60, 0) AS int)
                                ELSE                              CAST(ROUND(i.num, 0) AS int)
                            END
                FROM    Reuniones r
                JOIN    interpretado i ON i.Id = r.Id;");

            // Una duración de cero o negativa no es un dato, es ruido.
            migrationBuilder.Sql(
                "UPDATE Reuniones SET DuracionMinutos = NULL WHERE DuracionMinutos <= 0;");

            migrationBuilder.DropColumn(
                name: "Duracion",
                table: "Reuniones");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Duracion",
                table: "Reuniones",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            // Se repuebla para que la vuelta atrás no pierda el dato. El texto queda normalizado
            // («90 min», «2 h»), no idéntico al original: el original era justamente el problema.
            migrationBuilder.Sql(@"
                UPDATE  Reuniones
                SET     Duracion =
                            CASE
                                WHEN DuracionMinutos % 60 = 0
                                    THEN CAST(DuracionMinutos / 60 AS varchar(10)) + ' h'
                                WHEN DuracionMinutos < 60
                                    THEN CAST(DuracionMinutos AS varchar(10)) + ' min'
                                ELSE CAST(DuracionMinutos / 60 AS varchar(10)) + ' h '
                                   + CAST(DuracionMinutos % 60 AS varchar(10)) + ' min'
                            END
                WHERE   DuracionMinutos IS NOT NULL AND DuracionMinutos > 0;");

            migrationBuilder.DropColumn(
                name: "DuracionMinutos",
                table: "Reuniones");
        }
    }
}
