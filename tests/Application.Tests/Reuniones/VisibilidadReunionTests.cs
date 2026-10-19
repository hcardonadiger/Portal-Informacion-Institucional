using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

public class VisibilidadReunionTests
{
    // Usuario configurable para probar el filtro global por alcance + privacidad.
    private sealed class Usuario(Guid? id, bool global, string[] inst) : ICurrentUserService
    {
        public Guid?       UserId               => id;
        public string?     Nombre               => "u" + id;
        public string?     Correo               => $"u{id}@x.com";
        public string?     Rol                  => "Empleado";
        public bool        IsAuthenticated       => true;
        public bool        EsGlobal             => global;
        /// <summary>Nivel del rol. Por defecto Unidad —el más restrictivo, al que se cae un rol que
        /// no resuelve— y configurable porque el respaldo del filtro de reuniones solo vale desde
        /// nivel Institución, así que la diferencia dejó de ser decorativa.</summary>
        public NivelAlcance? Nivel                { get; init; }
        public NivelAlcance NivelAlcance         => global ? NivelAlcance.Global : (Nivel ?? NivelAlcance.Unidad);
        public bool        EsSoloLectura         => false;
        public bool        EsSupervisor          => false;
        public bool        EsTecnicoSoporte      => true;
        public bool        EsJefeDeArea          => false;
        public bool        EsPmo                 => false;
        public string?     ActiveInstitucionId   => inst.Length > 0 ? inst[0] : null;
        /// <summary>Área y unidad del contexto activo. Casi nadie las tiene asignadas —5 de las 51
        /// asignaciones reales— y son justo el ancla de dueño del filtro de reuniones, así que se
        /// dejan configurables en vez de fijas en null.</summary>
        public string?     Area                  { get; init; }
        public string?     Unidad                { get; init; }
        public string?     ActiveAreaId          => Area;
        public string?     ActiveUnidadId        => Unidad;
        public IReadOnlyCollection<string> InstitucionesAsignadas => inst;
        public bool        PuedeAccederInstitucion(string? institucionId) => global || (institucionId is string i && inst.Contains(i));
    }

    private static DbContextOptions<AppDbContext> Opts(string db) =>
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(db).Options;

    [Fact]
    public async Task Privada_SoloVisibleParaSuCreador()
    {
        var db = Guid.NewGuid().ToString();

        // El dueño (usuario 1, alcance a institución 7) crea una privada y una pública.
        var uid1 = Guid.NewGuid();
        await using (var ctx = new AppDbContext(Opts(db), new Usuario(uid1, false, ["7"]), NSubstitute.Substitute.For<MediatR.IPublisher>()))
        {
            var priv = Reunion.Crear("Privada"); priv.InstitucionId = "7"; priv.Institucion = "SENASA";
            priv.Visibilidad = VisibilidadReunion.Privada; priv.CreadoPorId = uid1;
            // La pública también la convoca el dueño: el comentario de arriba siempre lo dijo, pero
            // el fixture no lo estampaba y la reunión quedaba sin creador. Pasaba inadvertido
            // mientras el respaldo del filtro valía en todos los niveles.
            var pub = Reunion.Crear("Pública"); pub.InstitucionId = "7"; pub.Institucion = "SENASA";
            pub.CreadoPorId = uid1;
            await ctx.Reuniones.AddRangeAsync(priv, pub);
            await ctx.SaveChangesAsync();
        }

        // El dueño ve ambas.
        await using (var ctx = new AppDbContext(Opts(db), new Usuario(uid1, false, ["7"]), NSubstitute.Substitute.For<MediatR.IPublisher>()))
            (await ctx.Reuniones.Select(r => r.Titulo).ToListAsync())
                .Should().BeEquivalentTo(["Privada", "Pública"]);

        // Otro usuario con alcance a la misma institución NO ve la privada. Va con nivel
        // Institución porque la pública no tiene área ni unidad dueña, y el respaldo que recoge
        // esos casos ya no se extiende a los niveles de Área y Unidad.
        await using (var ctx = new AppDbContext(Opts(db), new Usuario(Guid.NewGuid(), false, ["7"]) { Nivel = NivelAlcance.Institucion }, NSubstitute.Substitute.For<MediatR.IPublisher>()))
            (await ctx.Reuniones.Select(r => r.Titulo).ToListAsync())
                .Should().BeEquivalentTo(["Pública"]);

        // Un administrador global tampoco ve la privada de otro.
        await using (var ctx = new AppDbContext(Opts(db), new Usuario(Guid.NewGuid(), true, []), NSubstitute.Substitute.For<MediatR.IPublisher>()))
            (await ctx.Reuniones.Select(r => r.Titulo).ToListAsync())
                .Should().BeEquivalentTo(["Pública"]);
    }

    /// <summary>
    /// Quien convoca una reunión tiene que poder volver a abrirla. Suena obvio y no lo era:
    /// <c>InstitucionId</c> guarda la CONTRAPARTE —"Reunión técnica SRECI-DIGER" queda con
    /// InstitucionId = SRECI— y el ancla de dueño son <c>AreaId</c>/<c>UnidadId</c>, que
    /// <c>CrearReunionCommand</c> copia del creador y son nulas para quien no tiene área ni unidad
    /// asignada. Sin esas dos, el respaldo del filtro cae en la contraparte, que por definición no
    /// es la institución de quien convocó: la reunión desaparecía de su lista en cuanto la guardaba
    /// y solo la veía un administrador global.
    /// </summary>
    [Fact]
    public async Task Publica_con_contraparte_de_otra_institucion_sigue_visible_para_quien_la_creo()
    {
        var db   = Guid.NewGuid().ToString();
        var uid  = Guid.NewGuid();
        var yo   = () => new Usuario(uid, false, ["DIGER"]);

        await using (var ctx = new AppDbContext(Opts(db), yo(), NSubstitute.Substitute.For<MediatR.IPublisher>()))
        {
            var r = Reunion.Crear("Reunión técnica SRECI-DIGER");
            r.InstitucionId = "SRECI"; r.Institucion = "SRECI";
            r.CreadoPorId   = uid;     // lo único que delata al dueño cuando no hay área ni unidad
            await ctx.Reuniones.AddAsync(r);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new AppDbContext(Opts(db), yo(), NSubstitute.Substitute.For<MediatR.IPublisher>()))
            (await ctx.Reuniones.Select(r => r.Titulo).ToListAsync())
                .Should().BeEquivalentTo(["Reunión técnica SRECI-DIGER"],
                    "la convocó él; que la contraparte sea de otra institución no la vuelve ajena");
    }

    /// <summary>
    /// El mismo caso para la otra forma en que queda una reunión sin ancla utilizable: un rol de
    /// nivel Unidad cuya asignación trae área pero no unidad. La rama de Unidad exige
    /// <c>UnidadId != null</c>, la de Área exige nivel Área, y el respaldo exige las dos nulas —
    /// ninguna se cumple, así que la reunión no la veía nadie, ni su creador.
    /// </summary>
    [Fact]
    public async Task Publica_de_un_rol_de_unidad_sin_unidad_sigue_visible_para_quien_la_creo()
    {
        var db  = Guid.NewGuid().ToString();
        var uid = Guid.NewGuid();
        var yo  = () => new Usuario(uid, false, ["DIGER"]) { Area = "GOBDIG" };

        await using (var ctx = new AppDbContext(Opts(db), yo(), NSubstitute.Substitute.For<MediatR.IPublisher>()))
        {
            var r = Reunion.Crear("Mesa de trabajo");
            r.InstitucionId = "SRECI"; r.Institucion = "SRECI";
            r.AreaId        = "GOBDIG";   // CrearReunionCommand estampa el área del creador…
            r.UnidadId      = null;       // …y no hay unidad que estampar
            r.CreadoPorId   = uid;
            await ctx.Reuniones.AddAsync(r);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new AppDbContext(Opts(db), yo(), NSubstitute.Substitute.For<MediatR.IPublisher>()))
            (await ctx.Reuniones.Select(r => r.Titulo).ToListAsync())
                .Should().BeEquivalentTo(["Mesa de trabajo"],
                    "un rol de Unidad sin unidad asignada no puede perder lo que acaba de convocar");
    }

    /// <summary>
    /// La fuga que motivó <c>InstitucionDuenaId</c>. El respaldo del filtro —el que recoge a las
    /// reuniones sin área ni unidad, 80 de 81 en la base real— comparaba contra
    /// <c>InstitucionId</c>, que es la CONTRAPARTE invitada. Resultado: ser nombrado en una
    /// reunión equivalía a poder leerla. Los 3 usuarios de CONSUCOOP veían así 17 reuniones
    /// internas de DIGER, con sus acuerdos y la lista completa de asistentes.
    /// </summary>
    [Fact]
    public async Task La_contraparte_invitada_no_ve_la_reunion_de_la_casa_que_la_convoco()
    {
        var db       = Guid.NewGuid().ToString();
        var deDiger  = Guid.NewGuid();
        var deOtra   = Guid.NewGuid();

        await using (var ctx = new AppDbContext(Opts(db), new Usuario(deDiger, false, ["DIGER"]), NSubstitute.Substitute.For<MediatR.IPublisher>()))
        {
            var r = Reunion.Crear("Mesa técnica DIGER-CONSUCOOP");
            r.InstitucionId = "CONSUCOOP";   // la invitada
            r.Institucion   = "CONSUCOOP";
            r.CreadoPorId   = deDiger;
            // InstitucionDuenaId no se pone a mano: lo estampa el relleno de SaveChanges con la
            // institución activa de quien guarda, que es lo que hace CrearReunionCommand.
            await ctx.Reuniones.AddAsync(r);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new AppDbContext(Opts(db), new Usuario(deOtra, false, ["CONSUCOOP"]), NSubstitute.Substitute.For<MediatR.IPublisher>()))
            (await ctx.Reuniones.ToListAsync())
                .Should().BeEmpty("estar invitado a una reunión no es ser dueño de ella");
    }

    /// <summary>
    /// La reunión es de la unidad de quien la convocó, y de nadie más. Era el reclamo concreto:
    /// a un Jefe de Unidad de Desarrollo de Proyectos le aparecían las 78 reuniones migradas, que
    /// son de Digitalización de Trámites, porque el respaldo del filtro —el que recoge lo que no
    /// tiene área ni unidad— valía en todos los niveles y repartía por institución entera.
    /// </summary>
    [Fact]
    public async Task Solo_la_unidad_que_convoco_ve_la_reunion()
    {
        var db      = Guid.NewGuid().ToString();
        var creador = Guid.NewGuid();
        var deDitra = () => new Usuario(Guid.NewGuid(), false, ["DIGER"]) { Area = "GOBDIG", Unidad = "DITRA" };
        var deDpd   = () => new Usuario(Guid.NewGuid(), false, ["DIGER"]) { Area = "GOBDIG", Unidad = "DPD" };

        await using (var ctx = new AppDbContext(Opts(db), new Usuario(creador, false, ["DIGER"]) { Area = "GOBDIG", Unidad = "DITRA" }, NSubstitute.Substitute.For<MediatR.IPublisher>()))
        {
            var r = Reunion.Crear("Capacitación de plataforma SOL");
            r.InstitucionId = "CONSUCOOP";   // la invitada
            r.Institucion   = "CONSUCOOP";
            r.CreadoPorId   = creador;
            // Área y unidad dueñas las estampa el relleno de SaveChanges con el contexto activo,
            // igual que hace CrearReunionCommand.
            await ctx.Reuniones.AddAsync(r);
            await ctx.SaveChangesAsync();
        }

        // Un colega de la misma unidad sí la ve, aunque no la haya convocado él.
        await using (var ctx = new AppDbContext(Opts(db), deDitra(), NSubstitute.Substitute.For<MediatR.IPublisher>()))
            (await ctx.Reuniones.Select(r => r.Titulo).ToListAsync())
                .Should().BeEquivalentTo(["Capacitación de plataforma SOL"],
                    "la reunión es de la unidad, no solo de quien apretó el botón");

        // Una unidad hermana, no.
        await using (var ctx = new AppDbContext(Opts(db), deDpd(), NSubstitute.Substitute.For<MediatR.IPublisher>()))
            (await ctx.Reuniones.ToListAsync())
                .Should().BeEmpty("compartir institución no es compartir el trabajo de otra unidad");
    }

    /// <summary>
    /// El respaldo que queda: una reunión sin área ni unidad dueña —la convoca alguien sin unidad
    /// asignada— no desaparece, pero sube hasta la jefatura de institución en vez de repartirse
    /// a todas las unidades.
    /// </summary>
    [Fact]
    public async Task La_reunion_sin_unidad_duena_la_ve_la_jefatura_de_institucion_y_no_las_unidades()
    {
        var db      = Guid.NewGuid().ToString();
        var creador = Guid.NewGuid();

        await using (var ctx = new AppDbContext(Opts(db), new Usuario(creador, false, ["DIGER"]), NSubstitute.Substitute.For<MediatR.IPublisher>()))
        {
            var r = Reunion.Crear("Mesa sin unidad asignada");
            r.CreadoPorId = creador;
            await ctx.Reuniones.AddAsync(r);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new AppDbContext(Opts(db), new Usuario(Guid.NewGuid(), false, ["DIGER"]) { Nivel = NivelAlcance.Institucion }, NSubstitute.Substitute.For<MediatR.IPublisher>()))
            (await ctx.Reuniones.Select(r => r.Titulo).ToListAsync())
                .Should().BeEquivalentTo(["Mesa sin unidad asignada"],
                    "sin dueño de unidad, responde la institución");

        await using (var ctx = new AppDbContext(Opts(db), new Usuario(Guid.NewGuid(), false, ["DIGER"]) { Area = "GOBDIG", Unidad = "DPD" }, NSubstitute.Substitute.For<MediatR.IPublisher>()))
            (await ctx.Reuniones.ToListAsync())
                .Should().BeEmpty("una unidad no hereda lo que no tiene dueño");
    }
}
