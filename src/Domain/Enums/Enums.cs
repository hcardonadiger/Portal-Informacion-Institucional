namespace Diger.TramitesEstado.Domain.Enums;

// ── Rol del usuario interno DIGER ─────────────────────────────────────────
// Los roles ya NO son este enum: viven en la tabla Roles y se administran desde
// /Accesos/Roles. Este enum se conserva como fuente del seed de los 6 roles base
// (DbSeeder / migración) y de los nameof(...) en los chequeos de Administrador.
public enum RolUsuario
{
    Administrador   = 1, // Gestiona usuarios y todo el portal
    JefeInstitucion = 2, // Gestiona toda su institución
    JefeArea        = 3, // Gestiona toda su área
    JefeUnidad      = 4, // Gestiona toda su unidad
    Empleado        = 5, // Gestiona sus propios datos en su unidad
    Consultor       = 6  // Solo lectura
}

// ── Alcance de datos que otorga un rol (reemplaza las ramas por nombre de rol
// que tenían los filtros RLS de AppDbContext) ─────────────────────────────────
public enum NivelAlcance
{
    Global      = 1, // Ve todo el portal, sin filtro institucional
    Institucion = 2, // Ve todo lo de su institución
    Area        = 3, // Ve lo de su área
    Unidad      = 4  // Ve lo de su unidad
}

// ── Acción sobre un módulo (vocabulario fijo de la matriz de permisos) ────────
public enum AccionModulo
{
    Ver      = 1,
    Crear    = 2,
    Editar   = 3,
    Eliminar = 4
}

// ── Tipo de documento (documentación solicitada) ──────────────────────────
public enum TipoDocumento
{
    Acta          = 1,
    Informe       = 2,
    Instructivo   = 3,
    Presentacion  = 4,
    Memorando     = 5,
    VideoTutorial = 6,
    Resolucion    = 7,
    Formato       = 8,
    Otro          = 9
}

// ── Estado general del expediente ─────────────────────────────────────────
public enum EstadoExpediente
{
    EnExploracion   = 1,
    EnLevantamiento = 2,
    EnModelado      = 3,
    EnValidacion    = 4,
    Cerrado         = 5
}

// ── Estado individual de un trámite dentro de un expediente ───────────────
public enum EstadoTramite
{
    Pendiente    = 1,
    EnProceso    = 2,
    Completado   = 3,
    EnOperacion  = 4,
    Suspendido   = 5
}

// ── Estado del levantamiento de campo ─────────────────────────────────────
public enum EstadoLevantamientoExp
{
    EnProceso          = 1,
    Completo           = 2,
    PendienteDeValidar = 3,
    RequiereRevisita   = 4
}

// ── Acción propuesta sobre un requisito (modelo racionalizado) ────────────
public enum AccionRequisito
{
    Mantener     = 1,
    Simplificar  = 2,
    Digitalizar  = 3,
    Eliminar     = 4
}

// ── Fase del flujo de actividades ─────────────────────────────────────────
public enum FaseFlujo
{
    Actual    = 1,
    Propuesto = 2
}

// ── Tipo de nodo en el constructor de flujos ──────────────────────────────
public enum TipoNodoFlujo
{
    Inicio   = 1,
    Paso     = 2,
    Decision = 3,
    Fin      = 4
}

// ── Estado de avance por sección del expediente ───────────────────────────
public enum EstadoSeccion
{
    Pendiente  = 1,
    EnProgreso = 2,
    Completo   = 3,
    Validado   = 4
}

// ── Origen de un contacto del directorio ──────────────────────────────────
public enum OrigenContacto
{
    Manual  = 1, // capturado en el directorio
    Reunion = 2  // derivado de una reunión / asistencia
}

// ── Cumplimiento de un requerimiento de infraestructura ───────────────────
public enum InfraStatus
{
    Pendiente = 1,
    Cumple    = 2,
    NoCumple  = 3,
    Parcial   = 4,
    NoAplica  = 5
}

// ── Tickets de soporte de la plataforma SOL ───────────────────────────────
public enum EstadoTicket
{
    Abierto    = 1,
    EnProgreso = 2,
    Resuelto   = 3,
    Cerrado    = 4
}

public enum PrioridadTicket
{
    Baja    = 1,
    Media   = 2,
    Alta    = 3,
    Critica = 4
}

// La categoría/tema del ticket dejó de ser un enum fijo: ahora es el catálogo
// administrable TemaTicket (con SLA en horas), asignable a especialistas.

public enum TipoComentarioTicket
{
    Comentario   = 1,
    CambioEstado = 2,
    Asignacion   = 3
}

// ── Tipo de evento que genera una notificación ────────────────────────────
public enum TipoNotificacion
{
    TicketAsignado          = 1,
    TicketResuelto          = 2,
    CompromisoVencido       = 3,
    CompromisoProximo       = 4,
    ReuniónMañana           = 5,
    ChatRecibido            = 6,
    EtapaCronogramaVencida  = 7,
    EtapaCronogramaProxima  = 8,
    RecordatorioManualTicket      = 9,
    RecordatorioManualExpediente  = 10,
    RecordatorioManualReunion     = 11,
    RecordatorioManualCompromiso  = 12,
    ProyectoCambioEstado          = 13,
}

// ── Estado de una sesión de chat de soporte ───────────────────────────────
public enum ChatEstado
{
    EnCola     = 1,
    Activo     = 2,
    Resuelto   = 3,
    Abandonado = 4,
    Expirado   = 5,
}

// ── Visibilidad de una reunión ────────────────────────────────────────────
public enum VisibilidadReunion
{
    Publica = 1, // visible para las instituciones dentro del alcance
    Privada = 2  // visible solo para quien la creó
}

// ── Plan de trabajo anual por institución ─────────────────────────────────
public enum EstadoPlanTrabajo
{
    Borrador = 1,
    Activo   = 2,
    Cerrado  = 3
}

public enum EstadoMeta
{
    Pendiente  = 1,
    EnProgreso = 2,
    Cumplida   = 3,
    Postergada = 4,
    Cancelada  = 5
}

// ── Estado de seguimiento de un compromiso/acuerdo de reunión ──────────────
// "Vencido" no es un estado almacenado: se calcula cuando el plazo pasó y el
// compromiso sigue abierto (Pendiente / EnProgreso / Reprogramado).
public enum EstadoCompromiso
{
    Pendiente    = 1,
    EnProgreso   = 2,
    Cumplido     = 3,
    Reprogramado = 4,
    Cancelado    = 5,
    EnRevision   = 6
}

// ── Decisión tomada en la bandeja de conciliación Expedientes ↔ SIGER ──────
// Se guarda para que lo ya revisado no vuelva a proponerse en cada pasada.
public enum DecisionConciliacion
{
    Enlazado           = 1, // el trámite quedó vinculado a una ficha SIGER
    Descartado         = 2, // se revisó y no corresponde enlazarlo
    ProponerFichaNueva = 3  // no existe en SIGER; queda en cola para darlo de alta
}

// ── Seguimiento de proyectos internos de DIGER ────────────────────────────
// La secuencia no es lineal como EstadoExpediente: Suspendido va y vuelve, y
// Cancelado se alcanza desde cualquier estado abierto. Las transiciones válidas
// las declara Proyecto.CambiarEstado, no el orden de este enum.
public enum EstadoProyecto
{
    Planificado = 1,
    EnEjecucion = 2,
    Suspendido  = 3,
    Cerrado     = 4,
    Cancelado   = 5
}

public enum PrioridadProyecto
{
    Alta  = 1,
    Media = 2,
    Baja  = 3
}

// ── Estado de un entregable dentro de un proyecto ──────────────────────────
// Los miembros conservan sus nombres aunque el tipo se llamara antes EstadoHito: se guardan como
// texto (HasConversion<string>), así que renombrarlos obligaría a reescribir la columna entera.
public enum EstadoEntregable
{
    Pendiente  = 1,
    EnProceso  = 2,
    Completado = 3,
    Cancelado  = 4
}

// ── Estado de una actividad dentro de un entregable ────────────────────────
// No reusa EstadoEntregable por concordancia: leer «Actividad: Completado» en una tabla parece
// un error de tipeo, no un enum compartido.
//
// El estado no se teclea: lo deriva el porcentaje reportado —0 → Pendiente, 1-99 → EnProceso,
// 100 → Completada—. La excepción es Cancelada, que sí es una decisión, y por eso además saca a
// la actividad del promedio del entregable en vez de arrastrarlo contando como cero.
public enum EstadoActividad
{
    Pendiente  = 1,
    EnProceso  = 2,
    Completada = 3,
    Cancelada  = 4
}

// ── Tipo de dependencia entre actividades ──────────────────────────────────
// Hoy el portal solo modela Fin → Comienzo, que es la dependencia del PMI que cubre casi todos
// los casos: la sucesora no arranca hasta que la predecesora termina. Los otros tres tipos
// (Comienzo-Comienzo, Fin-Fin y Comienzo-Fin) obligarían a explicar cuatro conceptos en la ficha
// para ganar poco, así que quedaron fuera.
//
// La columna existe igual —y guarda el tipo— para que agregarlos después sea un miembro más del
// enum y no una migración de datos. Se guarda como texto (HasConversion<string>): renombrar un
// miembro obliga a reescribir la columna, como ya pasó con TipoEventoProyecto.
public enum TipoDependencia
{
    /// <summary>La sucesora no puede arrancar hasta que la predecesora esté completada.</summary>
    FinComienzo = 1
}
