namespace Diger.TramitesEstado.Domain.Enums;

// ── Rol del usuario interno DIGER ─────────────────────────────────────────
public enum RolUsuario
{
    Administrador = 1, // gestiona usuarios y todo el portal
    Coordinador   = 2, // crea y edita expedientes
    Tecnico       = 3  // consulta
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

// ── Visibilidad de una reunión ────────────────────────────────────────────
public enum VisibilidadReunion
{
    Publica = 1, // visible para las instituciones dentro del alcance
    Privada = 2  // visible solo para quien la creó
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
    Cancelado    = 5
}
