namespace Diger.TramitesEstado.Api.Lectura;

// ─────────────────────────────────────────────────────────────────────────────
//  EL CONTRATO DE DATOS CON PORTALDIGITAL
//
//  Estas ocho clases no son «las entidades de PortalDigital copiadas». Son la lista
//  exacta de lo que esta API lee: ocho tablas y, dentro de ellas, solo las columnas
//  que acaban en una respuesta.
//
//  Las columnas que NO están acá son deliberadas, no olvidos. TramitesSiger tiene
//  más de treinta; esta API lee veintitantas. Lo que no aparece —el Id de SIGER, el
//  estado interno, las observaciones de DIGER, la bitácora de auditoría— es asunto
//  de PortalDigital y no tiene por qué salir por una API pública.
//
//  QUÉ PASA CUANDO PORTALDIGITAL CAMBIA EL ESQUEMA:
//
//    · Agrega una tabla o una columna    → acá no pasa nada. No hay que tocar ni desplegar.
//    · Cambia una columna que sí se lee  → esta API rompe. Y ES LO CORRECTO: esa columna
//                                          ya no es un detalle interno, viaja en el contrato
//                                          público. Que rompa al compilar es infinitamente
//                                          mejor que enterarse por HondurasÁgil.
//
//  Nada de esto tiene comportamiento a propósito: no hay métodos, ni validaciones, ni
//  reglas. Las reglas viven en PortalDigital. Acá solo se leen columnas.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Una ficha del inventario — tabla <c>TramitesSiger</c>.</summary>
public sealed class FichaSiger
{
    public int Id { get; set; }

    public string Codigo { get; set; } = default!;
    public string Nombre { get; set; } = default!;
    public string Institucion { get; set; } = default!;
    public string? InstitucionId { get; set; }

    /// <summary>La única condición que hace pública a una ficha. Sin excepción.</summary>
    public bool Publicado { get; set; }

    public int? CategoriaId { get; set; }
    public string? Modalidad { get; set; }
    public bool EsPopular { get; set; }
    public bool? CostoEsGratuito { get; set; }
    public string? CostoTexto { get; set; }
    public string? TiempoTexto { get; set; }

    public bool EstaEnSol { get; set; }
    /// <summary>La dirección completa heredada, de fichas anteriores a que se compusiera.</summary>
    public string? SolUrl { get; set; }
    /// <summary>El tramo final; lo que va delante lo pone la institución.</summary>
    public string? SolTramo { get; set; }
    public DateTime? SolVerificadoEl { get; set; }

    public string? Descripcion { get; set; }
    public string? Objetivo { get; set; }
    public string? DirigidoA { get; set; }
    public string? VigenciaDocumento { get; set; }
    public string? Temporalidad { get; set; }
    public string? EnlacePrincipal { get; set; }

    /// <summary>
    /// Si la ficha tiene capturado todo lo que hace falta para servirle de algo al ciudadano.
    ///
    /// <b>Esta API no la calcula: la lee.</b> La decide PortalDigital, en una columna calculada
    /// por la base (migración <c>ColumnaFichaCompleta</c>). Antes se evaluaba acá, con una copia
    /// de la regla de PortalDigital, y eso significaba que agregar un campo obligatorio allá
    /// obligaba a modificar y desplegar esta API. Ahora no: la regla cambia en un solo lado y
    /// acá se sirve lo que diga.
    /// </summary>
    public bool FichaCompleta { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    /// <summary>Campo editable del formulario. Ojo: no es la fecha que se publica —esa es
    /// <see cref="UpdatedAt"/>— sino un dato que captura quien edita.</summary>
    public DateTime? UltimaModificacion { get; set; }
}

/// <summary>Tabla <c>PasosSiger</c>.</summary>
public sealed class PasoSiger
{
    public int Id { get; set; }
    public int TramiteSigerId { get; set; }
    public int NumeroPaso { get; set; }
    public string Descripcion { get; set; } = default!;
    public string? Titulo { get; set; }
    public string? Modalidad { get; set; }
    public string? LugarDependencia { get; set; }
    public string? SalidaResultado { get; set; }
    public string? TiempoRegistrado { get; set; }
}

/// <summary>Tabla <c>RequisitosSiger</c>.</summary>
public sealed class RequisitoSiger
{
    public int Id { get; set; }
    public int TramiteSigerId { get; set; }
    public int Numero { get; set; }
    public string Requisito { get; set; } = default!;
    public string? Tipo { get; set; }
    public string? DocumentoSoporte { get; set; }
    public string? Formato { get; set; }
}

/// <summary>Tabla <c>EntregablesSiger</c>.</summary>
public sealed class EntregableSiger
{
    public int Id { get; set; }
    public int TramiteSigerId { get; set; }
    public int Numero { get; set; }
    public string Entregable { get; set; } = default!;
    public string? Formato { get; set; }
    public string? Presentacion { get; set; }
}

/// <summary>Tabla <c>LugaresAtencionSiger</c>.</summary>
public sealed class LugarAtencionSiger
{
    public int Id { get; set; }
    public int TramiteSigerId { get; set; }
    public int Numero { get; set; }
    public string Lugar { get; set; } = default!;
    public string? Ciudad { get; set; }
    public string? Direccion { get; set; }
    public string? Telefonos { get; set; }
}

/// <summary>Tabla <c>EnlacesSiger</c>.</summary>
public sealed class EnlaceSiger
{
    public int Id { get; set; }
    public int TramiteSigerId { get; set; }
    public int Numero { get; set; }
    public string Url { get; set; } = default!;
    public string? Tipo { get; set; }
}

/// <summary>Tabla <c>CategoriasTramite</c>.</summary>
public sealed class CategoriaTramite
{
    public int Id { get; set; }
    public string Nombre { get; set; } = default!;
    public string? Icono { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
}

/// <summary>Tabla <c>Instituciones</c>. La llave es la sigla (INPREMA, IDP…), no un número.</summary>
public sealed class Institucion
{
    public string Id { get; set; } = default!;
    public string Nombre { get; set; } = default!;
    public string? NombreCorto { get; set; }
    public string? LogoUrl { get; set; }
    public string? Telefono { get; set; }
    public string? SitioWeb { get; set; }
    public string? Direccion { get; set; }
    public string? Horario { get; set; }
    public string? Tipo { get; set; }
    public bool Activo { get; set; }

    /// <summary>Su ruta dentro de SOL cuando la real difiere de la sigla. Nula significa
    /// «nadie la corrigió», y entonces vale la sigla.</summary>
    public string? RutaSol { get; set; }
}
