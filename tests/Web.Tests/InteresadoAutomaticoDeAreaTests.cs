using System.Net;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Services;
using Diger.TramitesEstado.Application.Roles;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La costura completa, servida de verdad: un proyecto del área de alguien, <b>sin que nadie
/// siembre una sola fila de <c>ProyectoInteresados</c></b>, tiene que aparecer en su
/// <c>/Tableros/ProyectosArea</c> — y desaparecer cuando se le retira la capacidad al rol.
///
/// <para>El resto de las suites de tablero siembran las filas de interesado a mano: prueban la
/// página, no el mecanismo que la llena. Acá corre el <see cref="IInteresadosAutomaticosSync"/>
/// real, resuelto del contenedor de la aplicación, y la revocación entra por el comando real
/// (<c>ActualizarRolCommand</c>) a través de MediatR. Es la única prueba que falla si alguien
/// vuelve a quitar el disparador de <c>ActualizarRolCommandHandler</c>.</para>
/// </summary>
public sealed class InteresadoAutomaticoDeAreaTests : IAsyncLifetime
{
    private const string Ruta    = "/Tableros/ProyectosArea";
    private const string AreaId  = "AREA-DIGER";
    private const string Codigo  = "PRY-AUT-01";

    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Proyectos.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // La capacidad se enciende por fixture y nunca en PortalFactory: que el rol se llame
        // "JefeArea" no lo vuelve jefe de área para el portal.
        var rol = await db.Roles.SingleAsync(r => r.Id == "JefeArea");
        rol.Actualizar(rol.Nombre, rol.NivelAlcance, rol.Descripcion, rol.Color,
            esAdministrador: false, esSoloLectura: false, esSupervisor: true, esTecnicoSoporte: true,
            esJefeDeArea: true, esPmo: false);

        db.Areas.Add(Area.Crear(AreaId, "DIGER", "Area de Tecnologia"));

        var jefe = await db.Usuarios.SingleAsync(u => u.Correo == "jefearea@pruebas.gob.hn");
        db.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefe.Id, "DIGER", AreaId, null, "JefeArea"));

        var proyecto = Proyecto.Crear(Codigo, "Sistema de expedientes");
        proyecto.InstitucionId = "DIGER";   // el filtro de alcance se ancla acá
        proyecto.AreaId        = AreaId;
        db.Proyectos.Add(proyecto);

        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IRolCatalogo>().RecargarAsync();

        // Lo único que se hace a mano es disparar la sincronización, igual que la haría el
        // guardado del proyecto. Las filas de interesado las crea el servicio real.
        await scope.ServiceProvider.GetRequiredService<IInteresadosAutomaticosSync>()
            .SincronizarProyectoAsync(proyecto.Id);
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> TableroAsync()
    {
        var respuesta = await _portal.ClienteComo("JefeArea").GetAsync(Ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await respuesta.Content.ReadAsStringAsync();
    }

    private async Task ActualizarRolAsync(bool esJefeDeArea)
    {
        using var scope = _portal.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new ActualizarRolCommand(
                "JefeArea", "Jefe de Area", NivelAlcance.Area, null, null,
                EsAdministrador: false, EsSoloLectura: false, EsSupervisor: true, EsTecnicoSoporte: true,
                Activo: true, EsJefeDeArea: esJefeDeArea, EsPmo: false));
    }

    [Fact]
    public async Task El_jefe_de_area_ve_el_proyecto_sin_que_nadie_siembre_la_fila_de_interesado()
    {
        (await TableroAsync()).Should().Contain(Codigo,
            "la sincronizacion automatica lo dejo como interesado del proyecto de su area");
    }

    [Fact]
    public async Task Destildar_la_capacidad_del_rol_le_quita_el_proyecto_del_tablero()
    {
        (await TableroAsync()).Should().Contain(Codigo);

        await ActualizarRolAsync(esJefeDeArea: false);

        (await TableroAsync()).Should().NotContain(Codigo,
            "quitar la capacidad tiene que revocar el acceso que esa casilla concedia");
    }
}
