using Diger.TramitesEstado.Application.Dashboards.Queries.GetReunionesDashboard;
using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

/// <summary>Listado único de personas capacitadas: filtrado por tipo, exclusión de DIGER,
/// deduplicación por correo / nombre normalizado y conteo de participación.</summary>
public class PersonasCapacitadasTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public PersonasCapacitadasTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser());
    }

    private async Task<Reunion> CapacitacionAsync(string titulo, params Asistente[] asistentes)
        => await ReunionAsync(titulo, "Capacitación", asistentes);

    private async Task<Reunion> ReunionAsync(string titulo, string tipo, params Asistente[] asistentes)
    {
        var r = Reunion.Crear(titulo);
        r.Tipo = tipo;
        foreach (var a in asistentes) r.Agregar(a);
        await _ctx.Reuniones.AddAsync(r);
        await _ctx.SaveChangesAsync();
        return r;
    }

    private static Asistente Persona(string nombre, string? correo = null, string? institucion = "IHADFA")
        => new() { Nombre = nombre, Correo = correo, Institucion = institucion };

    private async Task<IReadOnlyList<Application.Dashboards.Common.PersonaCapacitadaDto>> ListarAsync()
    {
        var handler = new GetReunionesDashboardQueryHandler(_ctx);
        var dto = await handler.Handle(new GetReunionesDashboardQuery(), CancellationToken.None);
        return dto.PersonasCapacitadas;
    }

    [Fact]
    public async Task SoloCuentaReunionesDeCapacitacion()
    {
        await CapacitacionAsync("Primera Capacitación IHADFA", Persona("Ana Lopez", "ana@ihadfa.hn"));
        await ReunionAsync("Reunión de análisis legal", "Reunión técnica", Persona("Beto Cruz", "beto@ihadfa.hn"));

        var personas = await ListarAsync();

        personas.Should().ContainSingle();
        personas[0].Nombre.Should().Be("Ana Lopez");
    }

    [Fact]
    public async Task ExcluyeAlPersonalDeDiger()
    {
        await CapacitacionAsync("Capacitación Plataforma SOL",
            Persona("Facilitador DIGER", "fac@diger.gob.hn", "DIGER"),
            Persona("Carla Mejía", "carla@consucoop.hn", "CONSUCOOP"));

        var personas = await ListarAsync();

        personas.Should().ContainSingle();
        personas[0].Nombre.Should().Be("Carla Mejía");
        personas[0].Institucion.Should().Be("CONSUCOOP");
    }

    [Fact]
    public async Task DeduplicaPorCorreoAunqueElNombreVarie()
    {
        await CapacitacionAsync("Primera Capacitación", Persona("Hector Diaz Merlo", "hector@ihadfa.hn"));
        await CapacitacionAsync("Tercera Capacitación", Persona("HECTOR ENRIQUE DIAZ", "HECTOR@IHADFA.HN"));

        var personas = await ListarAsync();

        personas.Should().ContainSingle();
        personas[0].Veces.Should().Be(2);
        personas[0].EsMultiple.Should().BeTrue();
        personas[0].Capacitaciones.Should().BeEquivalentTo(["Primera Capacitación", "Tercera Capacitación"]);
    }

    [Fact]
    public async Task DeduplicaPorNombreNormalizadoCuandoNoHayCorreo()
    {
        // Mismo nombre con acentos, mayúsculas y espacios distintos, sin correo.
        await CapacitacionAsync("Primera Capacitación", Persona("José  Martínez", correo: null));
        await CapacitacionAsync("Segunda Capacitación", Persona("jose martinez", correo: null));

        var personas = await ListarAsync();

        personas.Should().ContainSingle();
        personas[0].Veces.Should().Be(2);
    }

    [Fact]
    public async Task PersonaConUnaSolaCapacitacionNoEsMultiple()
    {
        await CapacitacionAsync("Primera Capacitación de CONSUCOOP",
            Persona("Fany Melissa López Rivera", "fany@consucoop.hn", "CONSUCOOP"));

        var personas = await ListarAsync();

        personas[0].Veces.Should().Be(1);
        personas[0].EsMultiple.Should().BeFalse();
    }

    [Fact]
    public async Task AsistirDosVecesALaMismaCapacitacionNoDuplicaElConteo()
    {
        var r = Reunion.Crear("Capacitación IHADFA");
        r.Tipo = "Capacitación";
        r.Agregar(Persona("Ever Eguigure", "ever@ihadfa.hn"));
        r.Agregar(Persona("Ever Eguigure", "ever@ihadfa.hn"));
        await _ctx.Reuniones.AddAsync(r);
        await _ctx.SaveChangesAsync();

        var personas = await ListarAsync();

        personas.Should().ContainSingle();
        personas[0].Veces.Should().Be(1);
    }

    [Fact]
    public async Task OrdenaAlfabeticamentePorNombre()
    {
        await CapacitacionAsync("Capacitación",
            Persona("Zoila Rosa", "z@x.hn"),
            Persona("Abel Josué Maldonado", "a@x.hn"),
            Persona("Mario Peña", "m@x.hn"));

        var personas = await ListarAsync();

        personas.Select(p => p.Nombre)
            .Should().ContainInOrder("Abel Josué Maldonado", "Mario Peña", "Zoila Rosa");
    }

    public void Dispose() => _ctx.Dispose();
}
