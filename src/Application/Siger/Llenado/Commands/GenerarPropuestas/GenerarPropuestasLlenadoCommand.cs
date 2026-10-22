using Diger.TramitesEstado.Application.Siger.Bloqueo;
using Diger.TramitesEstado.Application.Siger.Llenado;

namespace Diger.TramitesEstado.Application.Siger.Llenado.Commands.GenerarPropuestas;

/// <summary>Recorre el inventario y deja en cola un valor propuesto para cada hueco que las
/// reglas sepan llenar.</summary>
public sealed record GenerarPropuestasLlenadoCommand : IRequest<ResultadoGeneracion>;

/// <param name="FichasRevisadas">Fichas que tenían al menos un hueco.</param>
/// <param name="Creadas">Propuestas nuevas puestas en cola.</param>
/// <param name="YaEstaban">Huecos que ya tenían una propuesta pendiente y se dejaron intactos.</param>
/// <param name="RespetadasPorRechazo">Huecos que no se volvieron a proponer porque alguien ya
/// había rechazado exactamente ese valor.</param>
/// <param name="SinPropuesta">Huecos para los que ninguna regla tuvo nada que decir. No es un
/// fallo: es la parte del inventario que necesita a una persona.</param>
public sealed record ResultadoGeneracion(
    int FichasRevisadas,
    int Creadas,
    int YaEstaban,
    int RespetadasPorRechazo,
    int SinPropuesta,
    IReadOnlyDictionary<string, int> PorCampo,
    IReadOnlyDictionary<string, int> PorCerteza);

/// <remarks>
/// <para>
/// <b>Es idempotente y está pensado para correrse muchas veces</b>, porque las reglas se van a
/// afinar: se corre, se mira la cola, se ajusta una regla, se vuelve a correr. Un hueco que ya
/// tiene propuesta pendiente no genera otra —lo garantiza además un índice único filtrado, para
/// que no dependa solo de que este código se acuerde—.
/// </para>
/// <para>
/// <b>No insiste con lo que ya rechazaron.</b> Si alguien miró una propuesta y dijo que no, la
/// siguiente corrida no vuelve a ponerle el mismo valor enfrente; sí le pone uno distinto, que es
/// justo lo que se quiere cuando la regla mejoró. Sin esta distinción, cada corrida devolvería a
/// la cola todo lo que la persona acababa de descartar, y la cola dejaría de ser un lugar donde
/// se avanza.
/// </para>
/// <para>
/// <b>Va por lotes</b> y cada lote guarda por su cuenta: son más de mil fichas con sus pasos y
/// requisitos, y una corrida interrumpida no debe perder lo ya propuesto.
/// </para>
/// </remarks>
public sealed class GenerarPropuestasLlenadoCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<GenerarPropuestasLlenadoCommand, ResultadoGeneracion>
{
    private const int TamanoLote = 100;

    private static readonly CampoFicha[] Campos =
        [CampoFicha.Categoria, CampoFicha.Modalidad, CampoFicha.Tiempo, CampoFicha.Costo];

    public async Task<ResultadoGeneracion> Handle(GenerarPropuestasLlenadoCommand _, CancellationToken ct)
    {
        var categorias = await CatalogoDeCategoriasAsync(ct);

        // Solo interesan las fichas a las que les falta algo. El resto ni se carga.
        //
        // Y solo las que este portal todavía manda: una ficha enlazada a un expediente tiene sus
        // campos de contenido de solo lectura acá (D-17), así que proponerle valores produciría
        // una cola de sugerencias que nadie puede aprobar.
        var pendientes = await ctx.TramitesSiger.AsNoTracking().SinBloqueadas(ctx.Tramites.AsNoTracking())
            .Where(t => t.CategoriaId == null || t.Modalidad == null
                     || t.TiempoTexto == null || t.CostoEsGratuito == null)
            .Select(t => t.Id).OrderBy(id => id).ToListAsync(ct);

        var acumulado = new Acumulado();

        foreach (var lote in EnLotes(pendientes, TamanoLote))
            await ProcesarLoteAsync(lote, categorias, acumulado, ct);

        return acumulado.AResultado(pendientes.Count);
    }

    /// <summary>
    /// El catálogo de categorías indexado por nombre normalizado. Las reglas hablan de «medio
    /// ambiente», no del número 8, precisamente para no depender de en qué orden se sembró esta
    /// tabla en cada base.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, int>> CatalogoDeCategoriasAsync(CancellationToken ct)
    {
        var filas = await ctx.CategoriasTramite.AsNoTracking()
            .Select(c => new { c.Id, c.Nombre }).ToListAsync(ct);

        var mapa = new Dictionary<string, int>();
        foreach (var f in filas)
            mapa[TextoNormalizado.De(f.Nombre)] = f.Id;

        return mapa;
    }

    private async Task ProcesarLoteAsync(
        List<int> ids, IReadOnlyDictionary<string, int> categorias, Acumulado acumulado, CancellationToken ct)
    {
        var fichas = await ctx.TramitesSiger.AsNoTracking()
            .Where(t => ids.Contains(t.Id)).ToListAsync(ct);

        // Por separado y no con Include: tres Include de colecciones sobre las mismas fichas
        // producen el producto cartesiano entre ellas. Ya mordió en la Fase 2.
        var pasos = (await ctx.PasosSiger.AsNoTracking()
            .Where(x => ids.Contains(x.TramiteSigerId))
            .OrderBy(x => x.NumeroPaso)
            .Select(x => new { x.TramiteSigerId, x.TiempoRegistrado, x.Descripcion })
            .ToListAsync(ct)).ToLookup(x => x.TramiteSigerId);

        var requisitos = (await ctx.RequisitosSiger.AsNoTracking()
            .Where(x => ids.Contains(x.TramiteSigerId))
            .Select(x => new { x.TramiteSigerId, x.Requisito })
            .ToListAsync(ct)).ToLookup(x => x.TramiteSigerId);

        var lugares = (await ctx.LugaresAtencionSiger.AsNoTracking()
            .Where(x => ids.Contains(x.TramiteSigerId))
            .GroupBy(x => x.TramiteSigerId)
            .Select(g => new { Id = g.Key, Cuantos = g.Count() })
            .ToListAsync(ct)).ToDictionary(x => x.Id, x => x.Cuantos);

        // Lo ya decidido sobre estas fichas, para no repetir ni insistir.
        var previas = await ctx.PropuestasLlenado.AsNoTracking()
            .Where(p => ids.Contains(p.TramiteSigerId))
            .Select(p => new { p.TramiteSigerId, p.Campo, p.Estado, p.ValorPropuesto })
            .ToListAsync(ct);

        var yaPendientes = previas.Where(p => p.Estado == EstadoPropuesta.Pendiente)
            .Select(p => (p.TramiteSigerId, p.Campo)).ToHashSet();

        var yaRechazadas = previas.Where(p => p.Estado == EstadoPropuesta.Rechazada)
            .Select(p => (p.TramiteSigerId, p.Campo, p.ValorPropuesto)).ToHashSet();

        var nuevas = new List<PropuestaLlenado>();

        foreach (var ficha in fichas)
        {
            var vacios = new HashSet<CampoFicha>();
            foreach (var campo in Campos)
            {
                if (!ValorLlenado.EstaVacio(ficha, campo)) continue;

                if (yaPendientes.Contains((ficha.Id, campo))) { acumulado.YaEstaban++; continue; }

                vacios.Add(campo);
            }

            if (vacios.Count == 0) continue;

            var datos = new DatosParaLlenado(
                ficha.Nombre,
                ficha.Descripcion,
                ficha.Objetivo,
                pasos[ficha.Id].Select(p => p.TiempoRegistrado).ToList(),
                pasos[ficha.Id].Select(p => p.Descripcion).ToList(),
                requisitos[ficha.Id].Select(r => r.Requisito).ToList(),
                lugares.GetValueOrDefault(ficha.Id));

            var propuestas = ReglasLlenado.Proponer(datos, vacios, categorias);
            var conPropuesta = new HashSet<CampoFicha>();

            foreach (var p in propuestas)
            {
                if (yaRechazadas.Contains((ficha.Id, p.Campo, p.Valor)))
                {
                    acumulado.RespetadasPorRechazo++;
                    conPropuesta.Add(p.Campo);   // se atendió el hueco; no cuenta como sin propuesta
                    continue;
                }

                nuevas.Add(new PropuestaLlenado
                {
                    TramiteSigerId = ficha.Id,
                    Campo          = p.Campo,
                    ValorPropuesto = p.Valor,
                    Certeza        = p.Certeza,
                    Justificacion  = p.Justificacion,
                    Estado         = EstadoPropuesta.Pendiente
                });

                conPropuesta.Add(p.Campo);
                acumulado.Anotar(p.Campo, p.Certeza);
            }

            acumulado.SinPropuesta += vacios.Count(c => !conPropuesta.Contains(c));
        }

        if (nuevas.Count == 0) return;

        ctx.PropuestasLlenado.AddRange(nuevas);
        await ctx.SaveChangesAsync(ct);
        acumulado.Creadas += nuevas.Count;
    }

    private static IEnumerable<List<int>> EnLotes(List<int> todos, int tamano)
    {
        for (var i = 0; i < todos.Count; i += tamano)
            yield return todos.GetRange(i, Math.Min(tamano, todos.Count - i));
    }

    /// <summary>Los conteos de la corrida. Mutable y privado a propósito: es un contador, no un
    /// concepto del dominio.</summary>
    private sealed class Acumulado
    {
        public int Creadas;
        public int YaEstaban;
        public int RespetadasPorRechazo;
        public int SinPropuesta;

        private readonly Dictionary<string, int> _porCampo   = [];
        private readonly Dictionary<string, int> _porCerteza = [];

        public void Anotar(CampoFicha campo, CertezaLlenado certeza)
        {
            var c = ValorLlenado.Etiqueta(campo);
            _porCampo[c] = _porCampo.GetValueOrDefault(c) + 1;
            _porCerteza[certeza.ToString()] = _porCerteza.GetValueOrDefault(certeza.ToString()) + 1;
        }

        public ResultadoGeneracion AResultado(int fichasRevisadas) =>
            new(fichasRevisadas, Creadas, YaEstaban, RespetadasPorRechazo, SinPropuesta,
                _porCampo, _porCerteza);
    }
}
