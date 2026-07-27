using System.Security.Cryptography;
using Diger.TramitesEstado.Application.Common.Interfaces;
using FluentValidation;

namespace Diger.TramitesEstado.Application.Usuarios.Commands.SolicitarRecuperacionPassword;

public sealed record SolicitarRecuperacionPasswordCommand(string Correo, string BaseUrl) : IRequest<Unit>;

public sealed class SolicitarRecuperacionPasswordCommandValidator : AbstractValidator<SolicitarRecuperacionPasswordCommand>
{
    public SolicitarRecuperacionPasswordCommandValidator()
    {
        RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.BaseUrl).NotEmpty();
    }
}

public sealed class SolicitarRecuperacionPasswordCommandHandler(
    IApplicationDbContext ctx,
    IUnitOfWork uow,
    IEmailService emailService) : IRequestHandler<SolicitarRecuperacionPasswordCommand, Unit>
{
    public async Task<Unit> Handle(SolicitarRecuperacionPasswordCommand cmd, CancellationToken ct)
    {
        var correoNormalizado = cmd.Correo.Trim().ToLowerInvariant();
        var usuario = await ctx.Usuarios
            .FirstOrDefaultAsync(u => u.Correo == correoNormalizado && u.Activo, ct);

        // Si el usuario no existe o está inactivo, retornamos sin error para prevenir enumeración
        if (usuario is null)
            return Unit.Value;

        // Generar token seguro URL-safe de alta entropía
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

        // 20 minutos de vigencia acordados
        usuario.GenerarTokenRecuperacion(token, TimeSpan.FromMinutes(20));
        await uow.SaveChangesAsync(ct);

        var resetUrl = $"{cmd.BaseUrl.TrimEnd('/')}/Cuenta/RestablecerPassword?token={Uri.EscapeDataString(token)}&correo={Uri.EscapeDataString(usuario.Correo)}";

        var bodyHtml = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8" />
                <style>
                    body { font-family: system-ui, -apple-system, sans-serif; background-color: #f8fafc; color: #1e293b; padding: 20px; }
                    .container { max-width: 580px; margin: 0 auto; background: #ffffff; border-radius: 12px; padding: 32px; border: 1px solid #e2e8f0; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); }
                    .header { text-align: center; border-bottom: 2px solid #3b82f6; padding-bottom: 16px; margin-bottom: 24px; }
                    .header h1 { color: #1e3a8a; font-size: 22px; margin: 0; font-weight: 800; }
                    .btn { display: inline-block; background-color: #2563eb; color: #ffffff !important; font-weight: 600; padding: 12px 24px; border-radius: 8px; text-decoration: none; margin: 20px 0; font-size: 15px; }
                    .footer { font-size: 12px; color: #64748b; margin-top: 32px; border-top: 1px solid #f1f5f9; padding-top: 16px; text-align: center; }
                    .warning { background-color: #eff6ff; border-left: 4px solid #3b82f6; padding: 12px; margin: 16px 0; font-size: 13px; color: #1e40af; border-radius: 4px; }
                </style>
            </head>
            <body>
                <div class="container">
                    <div class="header">
                        <h1>DIGER — Portal de Trámites del Estado</h1>
                    </div>
                    <p>Hola <strong>{{usuario.Nombre}}</strong>,</p>
                    <p>Hemos recibido una solicitud para restablecer la contraseña de tu cuenta en el Portal de Trámites de la DIGER.</p>
                    <p>Haz clic en el siguiente botón para ingresar tu nueva contraseña:</p>
                    <div style="text-align: center;">
                        <a href="{{resetUrl}}" class="btn" target="_blank">Restablecer mi Contraseña</a>
                    </div>
                    <div class="warning">
                        <strong>Nota importante:</strong> Este enlace es válido únicamente por <strong>20 minutos</strong>. Si tú no solicitaste este cambio, puedes ignorar este correo de forma segura.
                    </div>
                    <p style="font-size: 13px; color: #475569;">Si el botón no funciona, copia y pega el siguiente enlace en tu navegador:<br />
                    <a href="{{resetUrl}}" style="color: #2563eb; word-break: break-all;">{{resetUrl}}</a></p>
                    <div class="footer">
                        Este es un mensaje automático enviado por la Dirección de Gestión de Riesgos (DIGER). Por favor no respondas a este correo.
                    </div>
                </div>
            </body>
            </html>
            """;

        await emailService.SendEmailAsync(usuario.Correo, "Restablecimiento de contraseña — DIGER", bodyHtml, ct);

        return Unit.Value;
    }
}
