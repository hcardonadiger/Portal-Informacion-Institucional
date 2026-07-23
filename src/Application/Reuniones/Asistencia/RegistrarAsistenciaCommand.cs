using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Reuniones.Asistencia;

/// <summary>Auto-registro anónimo de un participante mediante el token público de la reunión.</summary>
public sealed record RegistrarAsistenciaCommand(Guid Token, AsistenteAutoInput Datos) : IRequest<string>;

public sealed class RegistrarAsistenciaCommandHandler(
    IReunionRepository repo, 
    IContactoRepository contactoRepo,
    IInstitucionRepository institucionRepo,
    IUnitOfWork uow)
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
        var tel = FormatearTelefono(d.CodigoPais, d.Telefono);

        // Vincular la institución con el catálogo (null cuando es texto libre / "Otra")
        Institucion? inst = null;
        if (!string.IsNullOrWhiteSpace(d.Institucion))
            inst = await institucionRepo.GetByNombreAsync(d.Institucion, ct);

        // Si hay un pre-registro pendiente para este correo, confirmarlo en lugar de crear un duplicado.
        var preregistrado = !string.IsNullOrWhiteSpace(correo)
            ? r.Asistentes.FirstOrDefault(a => a.EsPreregistro && a.Correo == correo)
            : null;

        if (preregistrado is not null)
        {
            preregistrado.Confirmado   = true;
            preregistrado.AutoRegistro = true;
            preregistrado.RegistradoEl = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(d.Nombre))       preregistrado.Nombre      = d.Nombre.Trim();
            if (!string.IsNullOrWhiteSpace(d.Cargo))        preregistrado.Cargo       = d.Cargo.Trim();
            if (!string.IsNullOrWhiteSpace(d.Institucion))
            {
                preregistrado.InstitucionId = inst?.Id;
                preregistrado.Institucion   = inst?.Nombre ?? d.Institucion.Trim();
            }
            if (!string.IsNullOrWhiteSpace(d.Departamento)) preregistrado.Departamento = d.Departamento.Trim();
            if (!string.IsNullOrWhiteSpace(tel))            preregistrado.Telefono    = tel;
        }
        else
        {
            // Evita el doble registro por correo.
            if (!string.IsNullOrWhiteSpace(correo) &&
                r.Asistentes.Any(a => a.Correo != null && a.Correo == correo))
                throw new DomainException("Ya existe un registro con ese correo para esta reunión.");

            r.RegistrarAsistente(d.Nombre, d.Cargo, inst?.Nombre ?? d.Institucion, d.Departamento, correo, tel,
                institucionId: inst?.Id);
        }

        repo.Update(r);

        if (!string.IsNullOrWhiteSpace(correo))
        {
            var contacto = await contactoRepo.GetByCorreoAsync(correo, ct);
            Institucion? inst = null;
            if (!string.IsNullOrWhiteSpace(d.Institucion))
            {
                inst = await institucionRepo.GetByNombreAsync(d.Institucion, ct);
            }
            var areaToUse = !string.IsNullOrWhiteSpace(d.Departamento) ? d.Departamento.Trim() : contacto?.Area;

            if (contacto is not null)
            {
                // Actualizar contacto existente.
                var instIdToUse = inst?.Id ?? contacto.InstitucionId;
                var instNameToUse = inst?.Nombre ?? contacto.Institucion;

                contacto.Actualizar(
                    d.Nombre, instIdToUse, instNameToUse, contacto.AreaId, contacto.UnidadId,
                    d.Cargo, correo, tel, contacto.Notas, area: areaToUse
                );
                contactoRepo.Update(contacto);
            }
            else if (inst is not null)
            {
                // Crear nuevo contacto
                var nuevoContacto = Contacto.Crear(
                    d.Nombre, inst.Id, inst.Nombre, r.AreaId, r.UnidadId, d.Cargo, correo, tel, null, OrigenContacto.Reunion, area: areaToUse
                );
                await contactoRepo.AddAsync(nuevoContacto, ct);
            }
        }

        await uow.SaveChangesAsync(ct);
        return r.Titulo;
    }

    public static string? FormatearTelefono(string? codigoPais, string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
            return null;

        var clean = telefono.Trim();
        var code = codigoPais?.Trim();

        if (!string.IsNullOrWhiteSpace(code))
        {
            while (clean.StartsWith(code + " ", StringComparison.OrdinalIgnoreCase) ||
                   clean.StartsWith(code, StringComparison.OrdinalIgnoreCase))
            {
                clean = clean[code.Length..].Trim();
            }

            if (clean.StartsWith("+"))
            {
                var parts = clean.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && parts[0].StartsWith("+"))
                {
                    clean = parts[1].Trim();
                }
            }

            return string.IsNullOrWhiteSpace(clean) ? null : $"{code} {clean}";
        }

        return clean;
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
