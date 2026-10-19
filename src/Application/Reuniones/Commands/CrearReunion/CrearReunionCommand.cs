using FluentValidation;
using Diger.TramitesEstado.Application.Reuniones.Common;

namespace Diger.TramitesEstado.Application.Reuniones.Commands.CrearReunion;

public sealed record CrearReunionCommand(
    ReunionFormDto Datos, List<AsistenteInput> Asistentes, List<AcuerdoInput> Acuerdos) : IRequest<int>;

public sealed class CrearReunionCommandHandler(
    IReunionRepository repo,
    IInstitucionRepository institucionRepo,
    IContactoRepository contactoRepo,
    ICurrentUserService currentUser,
    IApplicationDbContext ctx,
    IUnitOfWork uow)
    : IRequestHandler<CrearReunionCommand, int>
{
    public async Task<int> Handle(CrearReunionCommand cmd, CancellationToken ct)
    {
        // Acá había un rechazo por alcance: un usuario no global solo podía convocar a su propia
        // institución (PuedeAccederInstitucion es `EsGlobal || ActiveInstitucionId == id`). Era
        // justo al revés del caso de uso: DIGER convoca a la institución cuyos trámites está
        // racionalizando, así que la contraparte casi nunca es la propia. El lado de lectura ya se
        // había corregido —la pantalla pide GetActivasParaConvocarAsync— pero este rechazo seguía
        // vivo y botaba el formulario al guardar. Convocar no es ver: ver sigue decidiéndolo el
        // filtro RLS de Reunion. ActualizarReunionCommand nunca tuvo esta restricción.
        //
        // Se va también la exigencia de «al menos una institución»: existía para que la reunión
        // quedara anclada en el alcance del usuario, y ese ancla ya no es la contraparte —el filtro
        // reconoce a quien la convocó—. Los usuarios globales nunca la tuvieron.

        // "Es capacitación de plataforma" ya no es un checkbox manual: se deriva del Tipo elegido.
        cmd.Datos.EsCapacitacionPlataforma = cmd.Datos.Tipo == "Capacitación";

        var r = Reunion.Crear(cmd.Datos.Titulo);
        ReunionMapper.Aplicar(r, cmd.Datos, cmd.Asistentes, cmd.Acuerdos);
        r.CreadoPorId = currentUser.UserId;   // dueño (relevante para reuniones privadas)
        r.InstitucionDuenaId = currentUser.ActiveInstitucionId;  // la casa que convoca, no la convocada
        r.AreaId      = currentUser.ActiveAreaId;
        r.UnidadId    = currentUser.ActiveUnidadId;

        var principalId = cmd.Datos.InstitucionesIds.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(principalId))
        {
            var inst = await institucionRepo.GetParaConvocarByIdAsync(principalId, ct);
            r.InstitucionId = inst?.Id;
            r.Institucion   = inst?.Nombre;
        }
        else
        {
            r.InstitucionId = null;
            r.Institucion   = null;
        }

        if (cmd.Datos.ExpedienteId.HasValue)
        {
            var exp = await ctx.Expedientes.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == cmd.Datos.ExpedienteId.Value, ct);
            r.ExpedienteId = exp?.Id;
            r.ExpedienteCodigo = exp?.Codigo;
        }
        else
        {
            r.ExpedienteId = null;
            r.ExpedienteCodigo = null;
        }

        await repo.AddAsync(r, ct);
        await ContactoFeeder.FeedAsync(r, contactoRepo, institucionRepo, ctx, uow, ct);
        await uow.SaveChangesAsync(ct);
        return r.Id;
    }
}

public sealed class CrearReunionCommandValidator : AbstractValidator<CrearReunionCommand>
{
    public CrearReunionCommandValidator()
    {
        RuleFor(x => x.Datos.Titulo).NotEmpty().MaximumLength(250);
    }
}
