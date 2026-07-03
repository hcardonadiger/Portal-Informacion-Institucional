namespace Diger.TramitesEstado.Application.Expedientes.Seguimiento;

public sealed record SubDef(string Id, string Desc, double Peso);
public sealed record EtapaDef(string Num, string Label, double Peso, bool Toggle, IReadOnlyList<SubDef> Subs);

/// <summary>
/// Metodología estándar de digitalización/simplificación de un trámite (11 etapas).
/// Definición fija (no en BD); el avance por expediente se guarda aparte en ExpedienteEtapaAvance.
/// </summary>
public static class MetodologiaDigitalizacion
{
    public static readonly IReadOnlyList<EtapaDef> Etapas =
    [
        new("I", "Diagnóstico y línea de base", 0.08, false,
        [
            new("1.1", "Reunión de apertura para estructura actual", 0.15),
            new("1.2", "Mapeo de actores, roles y designación de contraparte técnica", 0.10),
            new("1.3", "Revisión normativa: leyes, reglamentos, acuerdos y disposiciones que regulan el servicio", 0.15),
            new("1.4", "Levantamiento del proceso actual (AS-IS): flujos, tiempos y pasos del trámite vigente", 0.20),
            new("1.5", "Medición de la demanda: volúmenes históricos de solicitudes y proyecciones de uso", 0.10),
            new("1.6", "Inventario de requisitos exigidos al ciudadano, formularios y gestión interna", 0.10),
            new("1.7", "Diagnóstico de infraestructura tecnológica y capacidad de interoperabilidad", 0.10),
            new("1.8", "Informe de línea de base consolidado como insumo para las siguientes fases", 0.10),
        ]),
        new("II", "Talleres de simplificación y modelación", 0.15, false,
        [
            new("2.1", "Diseño de talleres: metodología, materiales de facilitación y convocatoria de participantes", 0.10),
            new("2.2", "Taller AS-IS: revisión participativa, identificación de cuellos de botella y duplicidades", 0.20),
            new("2.3", "Taller de simplificación normativa: análisis de requisitos eliminables o sustituibles", 0.20),
            new("2.4", "Diseño del proceso simplificado (TO-BE) con roles, responsables y tiempos estimados", 0.25),
            new("2.5", "Validación interna del TO-BE con todas las unidades involucradas", 0.15),
            new("2.6", "Memoria de talleres: sistematización de acuerdos, compromisos y decisiones", 0.10),
        ]),
        new("III", "Modelación en sistema (SOL)", 0.12, false,
        [
            new("3.1", "Especificación técnica del servicio: requerimientos funcionales, casos de uso", 0.10),
            new("3.2", "Diseño de formularios en línea con criterios de usabilidad, accesibilidad y credibilidad", 0.15),
            new("3.3", "Desarrollo de procedimientos almacenados y lógica de validación en base de datos", 0.20),
            new("3.4", "Desarrollo e integración de web services para interoperabilidad con otras instituciones (Opcional)", 0.20),
            new("3.5", "Configuración de flujos de trabajo y gestión de estados del trámite", 0.15),
            new("3.6", "Integración de firma electrónica y pasarelas de pago (cuando aplique)", 0.10),
            new("3.7", "Presentaciones de seguimiento al desarrollo con el equipo institucional", 0.10),
        ]),
        new("IV", "Migración e instalación del ambiente SOL en infraestructura institucional", 0.12, true,
        [
            new("4.1", "Hacer el levantamiento de requisitos mínimos", 1.00),
        ]),
        new("V", "Pruebas en ambiente de testing", 0.08, false,
        [
            new("5.1", "Instalación del sistema en ambiente de testing aislado del entorno productivo", 0.10),
            new("5.2", "Pruebas funcionales internas por el equipo técnico", 0.20),
            new("5.3", "Sesiones de prueba con usuarios institucionales usando casos reales o simulados", 0.25),
            new("5.4", "Registro y priorización de hallazgos por nivel de criticidad", 0.15),
            new("5.5", "Corrección e implementación de ajustes identificados en las pruebas", 0.20),
            new("5.6", "Pruebas de regresión y firma de acta de conformidad de testing", 0.10),
        ]),
        new("VI", "Instructivos y videotutoriales ciudadanos", 0.04, false,
        [
            new("6.1", "Definición de guiones y estructura de contenido por cada servicio digitalizado", 0.15),
            new("6.2", "Producción de videotutoriales: grabación, edición y locución", 0.25),
            new("6.3", "Elaboración de instructivos paso a paso en formato PDF o en línea", 0.20),
            new("6.4", "Diseño de sección de preguntas frecuentes (FAQ) en lenguaje ciudadano", 0.15),
            new("6.5", "Revisión y aprobación institucional de todos los materiales producidos", 0.15),
            new("6.6", "Publicación en portal institucional, YouTube y otros canales definidos", 0.10),
        ]),
        new("VII", "Modelado y lanzamiento en producción", 0.05, false,
        [
            new("7.1", "Despliegue del sistema validado en el entorno productivo real", 0.30),
            new("7.2", "Apoyo en la definición de la estrategia comunicacional de lanzamiento", 0.20),
            new("7.3", "Definición de mensajes, canales, públicos objetivo y cronograma de comunicación", 0.20),
            new("7.4", "Lanzamiento oficial del servicio digital para la ciudadanía", 0.30),
        ]),
        new("VIII", "Gestión de incidencias y soporte continuo", 0.08, true,
        [
            new("8.1", "Implementación de un sistema de tickets con trazabilidad y prioridad, en sustitución de reportes por correo", 0.20),
            new("8.2", "Definición del protocolo de escalamiento de incidencias (institución → DIGER → SDE → PNUD)", 0.15),
            new("8.3", "Diagnóstico estándar con el Visor de eventos antes de aplicar cualquier parche", 0.20),
            new("8.4", "Catálogo de incidencias conocidas y sus soluciones", 0.20),
            new("8.5", "Buenas prácticas de modelado posteriores a la entrega: validar cambios con usuario de prueba", 0.15),
            new("8.6", "Publicación de avisos de mantenimiento cuando una incidencia afecte el servicio público", 0.10),
        ]),
        new("IX", "Seguimiento y asistencia técnica", 0.04, false,
        [
            new("9.1", "Mesa de soporte técnico activa durante las primeras semanas de operación", 0.20),
            new("9.2", "Monitoreo de indicadores: solicitudes en línea, tasas de abandono y tiempos de resolución", 0.25),
            new("9.3", "Verificación del cumplimiento del nuevo proceso por parte de los funcionarios", 0.25),
            new("9.4", "Informe de funcionamiento y recomendaciones de mejora de corto plazo", 0.15),
            new("9.5", "Sesiones de retroalimentación con el equipo institucional operador del sistema", 0.15),
        ]),
        new("X", "Socialización y comunicación pública", 0.04, false,
        [
            new("10.1", "Consultar del estado de socialización", 1.00),
        ]),
        new("XI", "Gestión de calidad institucional", 0.06, false,
        [
            new("11.1", "Implementación de encuestas de satisfacción ciudadana", 0.34),
            new("11.2", "Establecimiento del ciclo de revisión periódica: mensual o trimestral", 0.33),
            new("11.3", "Entrega formal, transferencia de responsabilidad y firma de acta de cierre", 0.33),
        ]),
    ];

    private static readonly Dictionary<string, SubDef> _subs =
        Etapas.SelectMany(e => e.Subs).ToDictionary(s => s.Id, s => s);

    public static bool SubExiste(string subId) => subId is not null && _subs.ContainsKey(subId);
    public static bool EtapaExiste(string num)  => Etapas.Any(e => e.Num == num);
    public static bool EtapaEsToggle(string num) => Etapas.Any(e => e.Num == num && e.Toggle);

    /// <summary>Avance de una etapa (0..1) según los estados de sus sub-pasos.</summary>
    public static double EtapaPct(EtapaDef e, IReadOnlyDictionary<string, int> estados)
    {
        double t = 0;
        foreach (var s in e.Subs)
        {
            var st = estados.TryGetValue(s.Id, out var v) ? v : 0;
            t += s.Peso * (st == 2 ? 1.0 : st == 1 ? 0.5 : 0.0);
        }
        return t;
    }

    /// <summary>¿La etapa aplica? (siempre para las no-toggle; para las toggle, default true salvo override).</summary>
    public static bool Aplica(EtapaDef e, IReadOnlyDictionary<string, bool> aplica) =>
        !e.Toggle || !aplica.TryGetValue(e.Num, out var b) || b;

    /// <summary>Avance global (0..1), ponderado por peso de etapa y normalizado por las etapas aplicables.</summary>
    public static double Global(IReadOnlyDictionary<string, int> estados, IReadOnlyDictionary<string, bool> aplica)
    {
        double num = 0, den = 0;
        foreach (var e in Etapas)
        {
            if (!Aplica(e, aplica)) continue;
            num += e.Peso * EtapaPct(e, estados);
            den += e.Peso;
        }
        return den <= 0 ? 0 : num / den;
    }

    public static string EstadoGlobal(double pct) =>
        pct <= 0 ? "No iniciado" : pct >= 0.9999 ? "En operación" : "En proceso";
}
