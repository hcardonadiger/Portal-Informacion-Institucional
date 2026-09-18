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
