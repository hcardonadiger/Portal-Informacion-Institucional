using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Reuniones.Asistencia;

/// <summary>Auto-registro anónimo de un participante mediante el token público de la reunión.</summary>
public sealed record RegistrarAsistenciaCommand(Guid Token, AsistenteAutoInput Datos) : IRequest<string>;

public sealed class RegistrarAsistenciaCommandHandler(IReunionRepository repo, IUnitOfWork uow)
    : IRequestHandler<RegistrarAsistenciaCommand, string>
{
    public async Task<string> Handle(RegistrarAsistenciaCommand cmd, CancellationToken ct)
    {
        var r = await repo.GetByTokenWithAsistentesAsync(cmd.Token, ct)
            ?? throw new NotFoundException(nameof(Reunion), cmd.Token);

        if (!r.RegistroAbierto)
            throw new DomainException("El registro de esta reunión está cerrado.");

        var d = cmd.Datos;
        var correo = d.Correo?.Trim().ToLowerInvariant();

        // Evita el doble registro por correo.
        if (!string.IsNullOrWhiteSpace(correo) &&
            r.Asistentes.Any(a => a.Correo != null && a.Correo == correo))
            throw new DomainException("Ya existe un registro con ese correo para esta reunión.");

        var tel = string.IsNullOrWhiteSpace(d.Telefono)
            ? null
            : $"{d.CodigoPais} {d.Telefono.Trim()}".Trim();

        r.RegistrarAsistente(d.Nombre, d.Cargo, d.Institucion, d.Departamento, correo, tel);
        repo.Update(r);
        await uow.SaveChangesAsync(ct);
        return r.Titulo;
    }
}

public sealed class RegistrarAsistenciaCommandValidator : AbstractValidator<RegistrarAsistenciaCommand>
{
    public RegistrarAsistenciaCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Datos.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Datos.Correo).MaximumLength(200);
        RuleFor(x => x.Datos.Departamento).MaximumLength(150);
        RuleFor(x => x.Datos.Cargo).MaximumLength(150);
    }
}
