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
    public async Task UneAlaMismaPersonaConDosCorreosDistintos()
    {
        // Caso real: correo institucional en una capacitación y personal en otra.
        await CapacitacionAsync("Primera Capacitación", Persona("Hector Enrique Diaz Merlo", "hdiaz@ihadfa.gob.hn"));
        await CapacitacionAsync("Tercera Capacitación", Persona("Hector Enrique Diaz Merlo", "merloenrique466@gmail.com"));

        var personas = await ListarAsync();

        personas.Should().ContainSingle();
        personas[0].Veces.Should().Be(2);
    }

    [Fact]
    public async Task UneNombreCortoSinCorreoConNombreCompletoConCorreo()
    {
        // Caso real: "Byron Duarte" (sin correo) y "Byron Andres Wilfredo Duarte Serrano".
        await CapacitacionAsync("Segunda Capacitación", Persona("Byron Duarte", correo: null));
        await CapacitacionAsync("Tercera Capacitación", Persona("Byron Andres Wilfredo Duarte Serrano", "bayronandres04@hotmail.com"));

        var personas = await ListarAsync();

        personas.Should().ContainSingle();
        personas[0].Veces.Should().Be(2);
        // Se conserva el nombre más completo.
        personas[0].Nombre.Should().Be("Byron Andres Wilfredo Duarte Serrano");
    }

    [Fact]
    public async Task UneNombreCortoConNombreLargoAunqueLosCorreosDifieran()
    {
        // Caso real: "Ricardo Mendez" y "Ricardo Stiven Mendez Bonilla".
        await CapacitacionAsync("Primera Capacitación", Persona("Ricardo Mendez", "rimendez@ihadfa.gob.hn"));
        await CapacitacionAsync("Segunda Capacitación", Persona("Ricardo Stiven Mendez Bonilla", "ricardomendez730@gmail.com"));

        var personas = await ListarAsync();

        personas.Should().ContainSingle();
        personas[0].Veces.Should().Be(2);
    }

    [Fact]
    public async Task NoUneAPersonasDistintasQueCompartenApellido()
    {
        await CapacitacionAsync("Capacitación",
            Persona("Caterin Mejía", "cmejia@consucoop.hn", "CONSUCOOP"),
            Persona("Isai Mejia Soto", "isai.mejia@iht.hn", "IHT"));

        var personas = await ListarAsync();

        personas.Should().HaveCount(2);
    }

    [Fact]
    public async Task NoUneAPersonasDistintasQueCompartenNombreDePila()
    {
        // "Hector Soto" y "Hector Enrique Diaz Merlo" comparten el nombre de pila pero
        // ninguno es forma corta del otro.
        await CapacitacionAsync("Capacitación",
            Persona("Hector Soto", "hector@senacit.gob.hn"),
            Persona("Hector Enrique Diaz Merlo", "hdiaz@ihadfa.gob.hn"));

        var personas = await ListarAsync();

        personas.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReportaAsistenciasAparteDePersonasUnicas()
    {
        // 3 filas de asistencia, pero 2 personas únicas.
        await CapacitacionAsync("Primera Capacitación",
            Persona("Ana Lopez", "ana@x.hn"),
            Persona("Beto Cruz", "beto@x.hn"));
        await CapacitacionAsync("Segunda Capacitación", Persona("Ana Lopez", "ana@x.hn"));

        var handler = new GetReunionesDashboardQueryHandler(_ctx);
        var dto = await handler.Handle(new GetReunionesDashboardQuery(), CancellationToken.None);

        dto.PersonasCapacitadas.Should().HaveCount(2);
        dto.AsistenciasEnCapacitaciones.Should().Be(3);
        dto.AsistenciasTotales.Should().Be(3);
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
