using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Common;

namespace Diger.TramitesEstado.Application.Proyectos.Queries.GetInformeProyecto;

/// <summary>
/// Reúne todo lo que necesita el Informe de Estado del proyecto (<see cref="InformeProyectoDto"/>).
///
/// <para>No re-consulta la base: <b>compone las consultas que ya existen</b> —ficha, tablero,
/// riesgos, interesados y avances— para no reescribir la aritmética del cronograma ni el filtro de
/// alcance. Si el proyecto no existe o cae fuera del alcance del usuario, la ficha o el tablero
/// vuelven <c>null</c> y aquí se traduce en <c>null</c> (la página lo convierte en 404).</para>
/// </summary>
public sealed record GetInformeProyectoQuery(int Id) : IRequest<InformeProyectoDto?>;

public sealed class GetInformeProyectoQueryHandler(ISender sender, ICurrentUserService currentUser)
    : IRequestHandler<GetInformeProyectoQuery, InformeProyectoDto?>
{
    public async Task<InformeProyectoDto?> Handle(GetInformeProyectoQuery q, CancellationToken ct)
    {
        var ficha = await sender.Send(new GetProyectoQuery(q.Id), ct);
        if (ficha is null) return null;

        var tablero = await sender.Send(new GetTableroProyectoQuery(q.Id), ct);
        if (tablero is null) return null;

        var riesgos     = await sender.Send(new GetRiesgosProyectoQuery(q.Id), ct);
        var interesados = await sender.Send(new GetInteresadosProyectoQuery(q.Id), ct);
        var avances     = await sender.Send(new GetAvancesProyectoQuery(q.Id), ct);

        var quien = currentUser.Nombre ?? currentUser.Correo ?? "—";
        return new InformeProyectoDto(ficha, tablero, riesgos, interesados, avances, quien, DateTime.UtcNow);
    }
}
