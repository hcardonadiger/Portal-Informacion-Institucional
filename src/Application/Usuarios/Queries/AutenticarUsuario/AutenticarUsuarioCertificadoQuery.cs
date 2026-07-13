using MediatR;
using Microsoft.EntityFrameworkCore;
using Diger.TramitesEstado.Application.Common.Interfaces;

namespace Diger.TramitesEstado.Application.Usuarios.Queries.AutenticarUsuario;

public sealed record AutenticarUsuarioCertificadoQuery(string Thumbprint)
    : IRequest<UsuarioAuthDto?>;

public sealed class AutenticarUsuarioCertificadoQueryHandler(
    IUsuarioRepository repo,
    IApplicationDbContext ctx)
    : IRequestHandler<AutenticarUsuarioCertificadoQuery, UsuarioAuthDto?>
{
    public async Task<UsuarioAuthDto?> Handle(AutenticarUsuarioCertificadoQuery q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.Thumbprint))
            return null;

        var thumbprint = q.Thumbprint.Trim().ToUpperInvariant();
        var usuario = await repo.GetByCertificadoThumbprintAsync(thumbprint, ct);
        
        if (usuario is null || !usuario.Activo)
            return null;

        var asignacionesEntity = await ctx.AsignacionesUsuario
            .Where(a => a.UsuarioId == usuario.Id)
            .ToListAsync(ct);

        var asignaciones = asignacionesEntity
            .Select(a => new AsignacionAuthDto(a.InstitucionId, a.AreaId, a.UnidadId, a.Rol))
            .ToList();

        var rolGlobal = asignaciones.FirstOrDefault()?.Rol ?? "Empleado";

        return new UsuarioAuthDto(usuario.Id, usuario.Nombre, usuario.Correo, rolGlobal, asignaciones);
    }
}
