using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Application.Tests;

/// <summary>Los tres identificadores que deja sembrados <see cref="PrioridadesDePrueba"/>.</summary>
internal sealed record CatalogoPrioridades(int Alta, int Media, int Baja);

/// <summary>
/// Siembra el catálogo mínimo de prioridades en una base en memoria. Hace falta porque un proyecto
/// apunta a una fila de ese catálogo: antes Alta/Media/Baja eran un enum y venían dadas por el
/// lenguaje, ahora son filas y alguien las tiene que poner. Un proyecto cuya prioridad no exista
/// desaparece de los listados —la proyección une contra el catálogo— en vez de fallar ruidosamente,
/// que es la forma incómoda en que esto se nota si se olvida.
///
/// <para>Reproduce las mismas tres que la migración deja en las bases reales, para que una prueba
/// que afirme sobre «Media» hable de lo mismo que ve el usuario.</para>
///
/// <para>Es idempotente: se puede llamar desde el constructor de la suite o desde cada siembra,
/// según de qué contexto disponga cada una.</para>
/// </summary>
internal static class PrioridadesDePrueba
{
    public static CatalogoPrioridades Sembrar(AppDbContext ctx)
    {
        if (!ctx.PrioridadesProyecto.Any())
        {
            var alta  = PrioridadProyecto.Crear("Alta",  1, ColorEtiqueta.Naranja);
            var media = PrioridadProyecto.Crear("Media", 2, ColorEtiqueta.Azul);
            var baja  = PrioridadProyecto.Crear("Baja",  3, ColorEtiqueta.Gris);
            media.FijarPredeterminada(true);

            ctx.PrioridadesProyecto.AddRange(alta, media, baja);
            ctx.SaveChanges();
        }

        var filas = ctx.PrioridadesProyecto.AsNoTracking().ToDictionary(p => p.Nombre, p => p.Id);
        return new CatalogoPrioridades(filas["Alta"], filas["Media"], filas["Baja"]);
    }

    /// <summary>Atajo para el caso común: «denme una prioridad válida para este proyecto».</summary>
    public static int Media(AppDbContext ctx) => Sembrar(ctx).Media;
}

/// <summary>Los cuatro identificadores del catálogo de prioridades de ticket.</summary>
internal sealed record CatalogoPrioridadesTicket(int Critica, int Alta, int Media, int Baja);

/// <summary>
/// El equivalente de <see cref="PrioridadesDePrueba"/> para los tickets. Va aparte porque los dos
/// catálogos son independientes: el de tickets tiene «Crítica» y la marca que alimenta el
/// indicador de los tableros.
/// </summary>
internal static class PrioridadesTicketDePrueba
{
    public static CatalogoPrioridadesTicket Sembrar(AppDbContext ctx)
    {
        if (!ctx.PrioridadesTicket.Any())
        {
            var critica = PrioridadTicket.Crear("Critica", 1, ColorEtiqueta.Rojo, esCritica: true);
            var alta    = PrioridadTicket.Crear("Alta",    2, ColorEtiqueta.Naranja);
            var media   = PrioridadTicket.Crear("Media",   3, ColorEtiqueta.Azul);
            var baja    = PrioridadTicket.Crear("Baja",    4, ColorEtiqueta.Gris);
            media.FijarPredeterminada(true);

            ctx.PrioridadesTicket.AddRange(critica, alta, media, baja);
            ctx.SaveChanges();
        }

        var filas = ctx.PrioridadesTicket.AsNoTracking().ToDictionary(p => p.Nombre, p => p.Id);
        return new CatalogoPrioridadesTicket(filas["Critica"], filas["Alta"], filas["Media"], filas["Baja"]);
    }

    /// <summary>Atajo: «denme una prioridad válida para este ticket».</summary>
    public static int Media(AppDbContext ctx) => Sembrar(ctx).Media;
}
