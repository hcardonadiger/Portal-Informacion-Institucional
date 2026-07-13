using System.Text.Json.Serialization;
using Diger.TramitesEstado.Application.Common.Catalogs;

namespace Diger.TramitesEstado.Web.Pages.Expedientes;

// ── DTO que refleja exactamente la forma JSON que produce/consume el editor
//    (expediente.js · recolectar/poblarFormulario). Se adapta a ExpedienteInputDto. ──
public sealed class OriginalExpedienteDto
{
    [JsonPropertyName("_ts")]                public string? Ts { get; set; } // marca de tiempo origen (importación)
    [JsonPropertyName("inst")]               public string? Inst { get; set; }
    [JsonPropertyName("fecha_apertura")]     public string? FechaApertura { get; set; }
    [JsonPropertyName("analista")]           public string? Analista { get; set; }
    [JsonPropertyName("dir_sede")]           public string? DirSede { get; set; }
    [JsonPropertyName("contacto_nombre")]    public string? ContactoNombre { get; set; }
    [JsonPropertyName("contacto_cargo")]     public string? ContactoCargo { get; set; }
    [JsonPropertyName("contacto_correo")]    public string? ContactoCorreo { get; set; }
    [JsonPropertyName("contacto_tel")]       public string? ContactoTel { get; set; }
    [JsonPropertyName("num_tramites")]       public int NumTramites { get; set; } = 1;
    [JsonPropertyName("num_tramites_prod")]  public int NumTramitesProd { get; set; }
    [JsonPropertyName("tramite_nombres")]    public List<string?> TramiteNombres { get; set; } = [];
    [JsonPropertyName("tramite_areas")]      public List<string?> TramiteAreas { get; set; } = [];
    [JsonPropertyName("tramites")]           public List<Dictionary<string, string?>> Tramites { get; set; } = [];
    [JsonPropertyName("reqs_tram")]          public List<List<ReqOrig>> ReqsTram { get; set; } = [];
    [JsonPropertyName("acciones_tram")]      public List<List<AccionOrig>> AccionesTram { get; set; } = [];
    [JsonPropertyName("infra")]              public InfraOrig? Infra { get; set; }
    [JsonPropertyName("legal")]              public List<LegalOrig> Legal { get; set; } = [];
    [JsonPropertyName("docs")]               public List<DocOrig> Docs { get; set; } = [];
    [JsonPropertyName("obs_legal")]          public string? ObsLegal { get; set; }
    [JsonPropertyName("num_func")]           public string? NumFunc { get; set; }
    [JsonPropertyName("num_anio")]           public string? NumAnio { get; set; }
    [JsonPropertyName("tiempo_obs")]         public string? TiempoObs { get; set; }
    [JsonPropertyName("t_norm")]             public string? TNorm { get; set; }
    [JsonPropertyName("desc_proceso")]       public string? DescProceso { get; set; }
    [JsonPropertyName("docs_internos")]      public List<DocIntOrig> DocsInternos { get; set; } = [];
    [JsonPropertyName("docs_add")]           public string? DocsAdd { get; set; }
    [JsonPropertyName("obs_flujo")]          public string? ObsFlujo { get; set; }
    [JsonPropertyName("func_dig")]           public string? FuncDig { get; set; }
    [JsonPropertyName("tiempo_dig")]         public string? TiempoDig { get; set; }
    [JsonPropertyName("obs_modelo")]         public string? ObsModelo { get; set; }
    [JsonPropertyName("flujos_actual")]      public List<List<NodoOrig>> FlujosActual { get; set; } = [];
    [JsonPropertyName("flujos_propuesto")]   public List<List<NodoOrig>> FlujosPropuesto { get; set; } = [];
    [JsonPropertyName("estados")]            public Dictionary<string, string?> Estados { get; set; } = [];
    [JsonPropertyName("notas")]              public Dictionary<string, string?> Notas { get; set; } = [];
    [JsonPropertyName("estado_exp")]         public string? EstadoExp { get; set; }
    [JsonPropertyName("estado_lev")]         public string? EstadoLev { get; set; }
    [JsonPropertyName("obs_levantamiento")]  public string? ObsLevantamiento { get; set; }
    [JsonPropertyName("obs_expediente")]     public string? ObsExpediente { get; set; }
    [JsonPropertyName("validado_diger")]     public string? ValidadoDiger { get; set; }
    [JsonPropertyName("validado_inst")]      public string? ValidadoInst { get; set; }
    [JsonPropertyName("fecha_validacion")]   public string? FechaValidacion { get; set; }
    [JsonPropertyName("num_acta")]           public string? NumActa { get; set; }

    public sealed class ReqOrig    { [JsonPropertyName("requisito")] public string? Requisito { get; set; } [JsonPropertyName("obs")] public string? Obs { get; set; } [JsonPropertyName("plantilla_origen_id")] public int? PlantillaOrigenId { get; set; } [JsonPropertyName("es_personalizado")] public bool EsPersonalizado { get; set; } }
    public sealed class AccionOrig { [JsonPropertyName("accion")] public string? Accion { get; set; } [JsonPropertyName("justificacion")] public string? Justificacion { get; set; } }
    public sealed class LegalOrig  { [JsonPropertyName("instrumento")] public string? Instrumento { get; set; } [JsonPropertyName("articulos")] public string? Articulos { get; set; } [JsonPropertyName("obs")] public string? Obs { get; set; } [JsonPropertyName("plantilla_origen_id")] public int? PlantillaOrigenId { get; set; } [JsonPropertyName("es_personalizado")] public bool EsPersonalizado { get; set; } }
    public sealed class DocOrig    { [JsonPropertyName("nombre")] public string? Nombre { get; set; } [JsonPropertyName("tipo")] public string? Tipo { get; set; } [JsonPropertyName("recibido")] public string? Recibido { get; set; } [JsonPropertyName("fecha")] public string? Fecha { get; set; } [JsonPropertyName("url")] public string? Url { get; set; } }
    public sealed class DocIntOrig { [JsonPropertyName("documento")] public string? Documento { get; set; } [JsonPropertyName("area")] public string? Area { get; set; } [JsonPropertyName("obs")] public string? Obs { get; set; } }
    public sealed class NodoOrig   { [JsonPropertyName("tipo")] public string? Tipo { get; set; } [JsonPropertyName("titulo")] public string? Titulo { get; set; } [JsonPropertyName("area")] public string? Area { get; set; } [JsonPropertyName("tiempo")] public string? Tiempo { get; set; } [JsonPropertyName("doc_emitido")] public string? DocEmitido { get; set; } [JsonPropertyName("obs")] public string? Obs { get; set; } [JsonPropertyName("retorno_a")] public int? RetornoA { get; set; } }
    public sealed class InfraOrig
    {
        [JsonPropertyName("personal")] public string? Personal { get; set; }
        [JsonPropertyName("perfiles")] public List<PerfilOrig> Perfiles { get; set; } = [];
        [JsonPropertyName("personal_ti")] public string? PersonalTI { get; set; }
        [JsonPropertyName("resp_sol")] public string? RespSol { get; set; }
        [JsonPropertyName("acomp")] public string? Acomp { get; set; }
        [JsonPropertyName("dc_modalidad")] public string? DcModalidad { get; set; }
        [JsonPropertyName("dc_virt")] public string? DcVirt { get; set; }
        [JsonPropertyName("dc_virt_otro")] public string? DcVirtOtro { get; set; }
        [JsonPropertyName("dc_disp")] public string? DcDisp { get; set; }
        [JsonPropertyName("dc_cond")] public List<string> DcCond { get; set; } = [];
        [JsonPropertyName("dc_obs")] public string? DcObs { get; set; }
        [JsonPropertyName("checklist")] public List<ChkOrig> Checklist { get; set; } = [];
        [JsonPropertyName("plan")] public string? Plan { get; set; }
    }
    public sealed class PerfilOrig { [JsonPropertyName("perfil")] public string? Perfil { get; set; } [JsonPropertyName("nombre")] public string? Nombre { get; set; } [JsonPropertyName("correo")] public string? Correo { get; set; } }
    public sealed class ChkOrig    { [JsonPropertyName("req")] public string? Req { get; set; } [JsonPropertyName("status")] public string? Status { get; set; } [JsonPropertyName("obs")] public string? Obs { get; set; } }
}
