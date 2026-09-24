/* ═══════════════════════════════════════════════════════════════════════════════════════
   Diagnóstico de una base desplegada — qué le falta para correr el código de hoy
   ───────────────────────────────────────────────────────────────────────────────────────
   NO MODIFICA NADA. Solo lee catálogos del sistema y cuenta. Se puede correr en producción
   a cualquier hora y las veces que haga falta.

   Existe porque los scripts de «poner al día» son idempotentes pero no adivinos: antes de
   tocar una base con datos de otras personas hay que saber en qué estado está. Este script
   lo dice, y al final recomienda exactamente qué correr.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 00_diagnostico.sql -o salida.txt
   o pegarlo en SSMS y ejecutar. La salida completa es lo que hay que revisar.

   Lo que compara:
     · las 81 tablas que el modelo de EF exige hoy
     · las 70 migraciones que el código trae
     · las 4 columnas que hay que vigilar aparte, explicadas en la sección 3
   ═══════════════════════════════════════════════════════════════════════════════════════ */

SET NOCOUNT ON;

PRINT '';
PRINT '════════════════ 0. Identidad de la base ════════════════';

SELECT  @@SERVERNAME                      AS Servidor,
        DB_NAME()                         AS Base,
        CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(40)) AS VersionSqlServer,
        CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(80)) AS Colacion,
        SYSDATETIME()                     AS Momento;

PRINT '';
PRINT '════════════════ 1. Tablas del modelo que NO existen ════════════════';
PRINT '(vacío = están todas. Si sale alguna, PARE: falta más que un script de columnas)';

WITH Modelo(Tabla) AS (SELECT * FROM (VALUES
        (N'AcuerdosReunion'), (N'Areas'), (N'AsignacionesUsuario'),
        (N'Asistentes'), (N'BitacoraExpediente'), (N'BitacoraProyecto'),
        (N'CategoriasDocumento'), (N'CategoriasProyecto'), (N'CategoriasTicket'),
        (N'CategoriasTramite'), (N'ChatMensajes'), (N'ChatSesiones'),
        (N'ComentariosCompromisos'), (N'ConciliacionesSiger'), (N'Contactos'),
        (N'DocumentosInternos'), (N'DocumentosSolicitados'), (N'EnlacesSiger'),
        (N'EntregablesSiger'), (N'ExpedienteEtapaAvances'), (N'ExpedienteEtapaCronogramas'),
        (N'ExpedienteSecciones'), (N'ExpedienteTramites'), (N'Expedientes'),
        (N'FlujoNodos'), (N'FundamentosLegales'), (N'InfraChecklist'),
        (N'InfraCondiciones'), (N'InfraPerfiles'), (N'Instituciones'),
        (N'LevantamientoDocumentos'), (N'LevantamientoEquipo'), (N'LevantamientoTramites'),
        (N'Levantamientos'), (N'LugaresAtencionSiger'), (N'Movimientos'),
        (N'NotasSeguimientoExpediente'), (N'Notificaciones'), (N'PasosSiger'),
        (N'Permisos'), (N'PermisosAuditoria'), (N'PlanTrabajo'),
        (N'PlanTrabajoMetas'), (N'PlantillaFundamentosLegales'), (N'PlantillaRequisitos'),
        (N'PlantillasTramite'), (N'Prefijos'), (N'PrioridadesProyecto'),
        (N'PrioridadesTicket'), (N'ProyectoActividades'), (N'ProyectoAvances'),
        (N'ProyectoDependenciasActividad'), (N'ProyectoDocumentoDescargas'), (N'ProyectoDocumentoVersiones'),
        (N'ProyectoDocumentos'), (N'ProyectoEntregables'), (N'ProyectoExpedientes'),
        (N'ProyectoInteresados'), (N'ProyectoReuniones'), (N'ProyectoRiesgos'),
        (N'ProyectoTickets'), (N'Proyectos'), (N'Recursos'),
        (N'RequisitosSiger'), (N'ReunionInstituciones'), (N'Reuniones'),
        (N'RolModuloAccesos'), (N'RolPermisos'), (N'Roles'),
        (N'TareasDigitalizacionSiger'), (N'TemasTicket'), (N'TicketAdjuntos'),
        (N'TicketComentarios'), (N'TicketTramites'), (N'Tickets'),
        (N'TramiteRequisitos'), (N'TramitesDefinicion'), (N'TramitesSiger'),
        (N'Unidades'), (N'UsuarioTemas'), (N'Usuarios')
    ) v(Tabla))
SELECT  m.Tabla AS TablaFaltante
FROM    Modelo m
WHERE   OBJECT_ID(QUOTENAME(m.Tabla), 'U') IS NULL
ORDER BY m.Tabla;

PRINT '';
PRINT '════════════════ 2. Cuántas tablas del modelo faltan ════════════════';

WITH Modelo(Tabla) AS (SELECT * FROM (VALUES
        (N'AcuerdosReunion'), (N'Areas'), (N'AsignacionesUsuario'),
        (N'Asistentes'), (N'BitacoraExpediente'), (N'BitacoraProyecto'),
        (N'CategoriasDocumento'), (N'CategoriasProyecto'), (N'CategoriasTicket'),
        (N'CategoriasTramite'), (N'ChatMensajes'), (N'ChatSesiones'),
        (N'ComentariosCompromisos'), (N'ConciliacionesSiger'), (N'Contactos'),
        (N'DocumentosInternos'), (N'DocumentosSolicitados'), (N'EnlacesSiger'),
        (N'EntregablesSiger'), (N'ExpedienteEtapaAvances'), (N'ExpedienteEtapaCronogramas'),
        (N'ExpedienteSecciones'), (N'ExpedienteTramites'), (N'Expedientes'),
        (N'FlujoNodos'), (N'FundamentosLegales'), (N'InfraChecklist'),
        (N'InfraCondiciones'), (N'InfraPerfiles'), (N'Instituciones'),
        (N'LevantamientoDocumentos'), (N'LevantamientoEquipo'), (N'LevantamientoTramites'),
        (N'Levantamientos'), (N'LugaresAtencionSiger'), (N'Movimientos'),
        (N'NotasSeguimientoExpediente'), (N'Notificaciones'), (N'PasosSiger'),
        (N'Permisos'), (N'PermisosAuditoria'), (N'PlanTrabajo'),
        (N'PlanTrabajoMetas'), (N'PlantillaFundamentosLegales'), (N'PlantillaRequisitos'),
        (N'PlantillasTramite'), (N'Prefijos'), (N'PrioridadesProyecto'),
        (N'PrioridadesTicket'), (N'ProyectoActividades'), (N'ProyectoAvances'),
        (N'ProyectoDependenciasActividad'), (N'ProyectoDocumentoDescargas'), (N'ProyectoDocumentoVersiones'),
        (N'ProyectoDocumentos'), (N'ProyectoEntregables'), (N'ProyectoExpedientes'),
        (N'ProyectoInteresados'), (N'ProyectoReuniones'), (N'ProyectoRiesgos'),
        (N'ProyectoTickets'), (N'Proyectos'), (N'Recursos'),
        (N'RequisitosSiger'), (N'ReunionInstituciones'), (N'Reuniones'),
        (N'RolModuloAccesos'), (N'RolPermisos'), (N'Roles'),
        (N'TareasDigitalizacionSiger'), (N'TemasTicket'), (N'TicketAdjuntos'),
        (N'TicketComentarios'), (N'TicketTramites'), (N'Tickets'),
        (N'TramiteRequisitos'), (N'TramitesDefinicion'), (N'TramitesSiger'),
        (N'Unidades'), (N'UsuarioTemas'), (N'Usuarios')
    ) v(Tabla))
SELECT  COUNT(*) AS TablasDelModelo,
        SUM(CASE WHEN OBJECT_ID(QUOTENAME(Tabla), 'U') IS NULL THEN 1 ELSE 0 END) AS Faltantes
FROM    Modelo;

PRINT '';
PRINT '════════════════ 3. Columnas que hay que vigilar aparte ════════════════';
PRINT 'Las tres primeras no las crea ninguna migración: sus archivos quedaron sin el';
PRINT 'atributo [Migration], así que EF no los ve. El modelo sí las usa. La cuarta es la';
PRINT 'migración más nueva del código.';

SELECT  v.Tabla, v.Columna, v.Origen,
        CASE WHEN OBJECT_ID(QUOTENAME(v.Tabla), 'U') IS NULL THEN 'NO EXISTE LA TABLA'
             WHEN COL_LENGTH(v.Tabla, v.Columna) IS NULL      THEN 'FALTA'
             ELSE 'presente' END AS Estado
FROM (VALUES
        (N'Asistentes',    N'EsPreregistro',  N'fuera de migración (AddPreregistroAsistente)'),
        (N'Asistentes',    N'Confirmado',     N'fuera de migración (AddPreregistroAsistente)'),
        (N'Instituciones', N'Color',          N'fuera de migración (AddInstitucionBranding)'),
        (N'Reuniones',     N'EncuestaActiva', N'migración 20260923223127')
     ) v(Tabla, Columna, Origen);

PRINT '';
PRINT '════════════════ 4. Índice IX_Asistentes_EsPreregistro ════════════════';

SELECT  CASE WHEN OBJECT_ID(N'[Asistentes]', 'U') IS NULL THEN 'NO EXISTE LA TABLA'
             WHEN EXISTS (SELECT 1 FROM sys.indexes
                          WHERE name = N'IX_Asistentes_EsPreregistro'
                            AND object_id = OBJECT_ID(N'[Asistentes]')) THEN 'presente'
             ELSE 'FALTA' END AS IX_Asistentes_EsPreregistro;

PRINT '';
PRINT '════════════════ 5. Historial de migraciones ════════════════';

IF OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NULL
BEGIN
    PRINT '*** No existe __EFMigrationsHistory. Esta base no la creó EF Core, o se perdió';
    PRINT '*** la tabla. No corra ningún script de «poner al día» sin revisarlo antes.';
END
ELSE
BEGIN
    SELECT  COUNT(*)          AS MigracionesRegistradas,
            MIN(MigrationId)  AS Primera,
            MAX(MigrationId)  AS Ultima
    FROM    __EFMigrationsHistory;

    PRINT '── Últimas 15 registradas ──';
    SELECT TOP 15 MigrationId, ProductVersion
    FROM   __EFMigrationsHistory
    ORDER  BY MigrationId DESC;
END;

PRINT '';
PRINT '════════════════ 6. Migraciones del código que la base NO tiene ════════════════';
PRINT '(vacío = la base está al día con EF)';

IF OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NOT NULL
BEGIN
    WITH Codigo(Id) AS (SELECT * FROM (VALUES
            (N'20260702225346_AddContactoActivo'),
            (N'20260706200739_InitialCreate'),
            (N'20260706202412_AddDigerInstitucionSeed'),
            (N'20260707120000_AddTicketTemaOtro'),
            (N'20260707151717_AddReunionInstituciones'),
            (N'20260707154325_AddSoftDelete'),
            (N'20260708165625_AddSpGenerarCodigoMovimiento'),
            (N'20260708204449_FixPendingModelChanges'),
            (N'20260709163652_UpdateInstitucionesSeed'),
            (N'20260709205323_AddCertificadoThumbprint'),
            (N'20260709221010_FixContactoActivoDefault'),
            (N'20260709221250_AddReunionSatisfaccionCalificacion'),
            (N'20260709221700_SwapExpedienteSeccionEstadoOrden'),
            (N'20260709222307_AddPlantillaTramite'),
            (N'20260717202416_AddReunionHilo'),
            (N'20260720161207_AddNotificaciones'),
            (N'20260721151629_AddChatSoporte'),
            (N'20260721152706_AddActivoToAreaAndUnidad'),
            (N'20260721195522_AddComentariosCompromisos'),
            (N'20260722173346_AddLevantamientosAndCronograma'),
            (N'20260722182710_AddPlanTrabajo'),
            (N'20260722194801_AddMetaTramiteIndex'),
            (N'20260722200241_AddMetaResponsableUsuario'),
            (N'20260722201107_AddExpedienteAnalistaUsuario'),
            (N'20260722203223_AddAsistenteInstitucionCatalogo'),
            (N'20260722203942_AddAcuerdoResponsableContacto'),
            (N'20260723161230_AddPasswordResetTokenToUsuario'),
            (N'20260723170218_AddAreaToContacto'),
            (N'20260724153537_AddRecursosTable'),
            (N'20260727195213_AddReunionExpedienteYContraparte'),
            (N'20260731224946_AddNotasSeguimientoExpediente'),
            (N'20260801022203_AddInventarioSiger'),
            (N'20260801023442_AjustarLongitudesSiger'),
            (N'20260801024207_AjustarFormatoRequisito'),
            (N'20260801024942_AjustarDocSoporte'),
            (N'20260801030023_AmpliarCamposSiger'),
            (N'20260801031904_AmpliarCamposSiger2'),
            (N'20260801041054_LinkSigerInstitucion'),
            (N'20260803023243_LinkExpedienteTramiteSiger'),
            (N'20260803204352_AddTramiteFechaCreacionYEstado'),
            (N'20260805150429_AuditoriaExpedientes'),
            (N'20260810194553_AddConciliacionSiger'),
            (N'20260812174438_AddPermisosModule'),
            (N'20260813143220_AddRolesDinamicos'),
            (N'20260814160739_AgregarCamposVentanilla'),
            (N'20260814162437_CorregirColacionBusqueda'),
            (N'20260823005328_AgregarProyectos'),
            (N'20260823164015_AgregarSelloEdicionAvance'),
            (N'20260823172749_AgregarBitacoraProyecto'),
            (N'20260824144118_AgregarAlcanceProyecto'),
            (N'20260824174815_AgregarRiesgosEInteresados'),
            (N'20260824224817_InteresadoUsuarioObligatorio'),
            (N'20260825150115_VincularBloqueoConRiesgo'),
            (N'20260826014231_EstructuraEntregablesActividades'),
            (N'20260826135216_DependenciasEntreActividades'),
            (N'20260826205435_RepositorioDocumentalProyectos'),
            (N'20260826223615_AuditoriaEnEntregablesYActividades'),
            (N'20260831005008_RegistroDescargasDocumentos'),
            (N'20260831022204_VincularReunionesYExpedientesAProyectos'),
            (N'20260831174631_VincularTicketsAProyectos'),
            (N'20260902215804_AgregarJefeDeAreaYPmo'),
            (N'20260902215902_AgregarAccionProyecto'),
            (N'20260902221131_AgregarInteresadoAutomatico'),
            (N'20260903175629_AgregarAccionTramite'),
            (N'20260904173357_AgregarBorradoLogicoUsuario'),
            (N'20260918155856_CatalogoDePrioridadesDeProyecto'),
            (N'20260918162251_CatalogoDePrioridadesDeTicket'),
            (N'20260918191759_CatalogoDeCategoriasDeProyecto'),
            (N'20260918193137_LasQSonCategoriasNoPrioridades'),
            (N'20260923223127_AddReunionEncuestaActiva')
        ) v(Id))
    SELECT  c.Id AS MigracionFaltante
    FROM    Codigo c
    WHERE   NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory h WHERE h.MigrationId = c.Id)
    ORDER BY c.Id;
END;

PRINT '';
PRINT '════════════════ 7. Migraciones que la base tiene y el código NO ════════════════';
PRINT '(si sale algo, esa base va ADELANTE del código: avise antes de desplegar)';

IF OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NOT NULL
BEGIN
    WITH Codigo(Id) AS (SELECT * FROM (VALUES
            (N'20260702225346_AddContactoActivo'),
            (N'20260706200739_InitialCreate'),
            (N'20260706202412_AddDigerInstitucionSeed'),
            (N'20260707120000_AddTicketTemaOtro'),
            (N'20260707151717_AddReunionInstituciones'),
            (N'20260707154325_AddSoftDelete'),
            (N'20260708165625_AddSpGenerarCodigoMovimiento'),
            (N'20260708204449_FixPendingModelChanges'),
            (N'20260709163652_UpdateInstitucionesSeed'),
            (N'20260709205323_AddCertificadoThumbprint'),
            (N'20260709221010_FixContactoActivoDefault'),
            (N'20260709221250_AddReunionSatisfaccionCalificacion'),
            (N'20260709221700_SwapExpedienteSeccionEstadoOrden'),
            (N'20260709222307_AddPlantillaTramite'),
            (N'20260717202416_AddReunionHilo'),
            (N'20260720161207_AddNotificaciones'),
            (N'20260721151629_AddChatSoporte'),
            (N'20260721152706_AddActivoToAreaAndUnidad'),
            (N'20260721195522_AddComentariosCompromisos'),
            (N'20260722173346_AddLevantamientosAndCronograma'),
            (N'20260722182710_AddPlanTrabajo'),
            (N'20260722194801_AddMetaTramiteIndex'),
            (N'20260722200241_AddMetaResponsableUsuario'),
            (N'20260722201107_AddExpedienteAnalistaUsuario'),
            (N'20260722203223_AddAsistenteInstitucionCatalogo'),
            (N'20260722203942_AddAcuerdoResponsableContacto'),
            (N'20260723161230_AddPasswordResetTokenToUsuario'),
            (N'20260723170218_AddAreaToContacto'),
            (N'20260724153537_AddRecursosTable'),
            (N'20260727195213_AddReunionExpedienteYContraparte'),
            (N'20260731224946_AddNotasSeguimientoExpediente'),
            (N'20260801022203_AddInventarioSiger'),
            (N'20260801023442_AjustarLongitudesSiger'),
            (N'20260801024207_AjustarFormatoRequisito'),
            (N'20260801024942_AjustarDocSoporte'),
            (N'20260801030023_AmpliarCamposSiger'),
            (N'20260801031904_AmpliarCamposSiger2'),
            (N'20260801041054_LinkSigerInstitucion'),
            (N'20260803023243_LinkExpedienteTramiteSiger'),
            (N'20260803204352_AddTramiteFechaCreacionYEstado'),
            (N'20260805150429_AuditoriaExpedientes'),
            (N'20260810194553_AddConciliacionSiger'),
            (N'20260812174438_AddPermisosModule'),
            (N'20260813143220_AddRolesDinamicos'),
            (N'20260814160739_AgregarCamposVentanilla'),
            (N'20260814162437_CorregirColacionBusqueda'),
            (N'20260823005328_AgregarProyectos'),
            (N'20260823164015_AgregarSelloEdicionAvance'),
            (N'20260823172749_AgregarBitacoraProyecto'),
            (N'20260824144118_AgregarAlcanceProyecto'),
            (N'20260824174815_AgregarRiesgosEInteresados'),
            (N'20260824224817_InteresadoUsuarioObligatorio'),
            (N'20260825150115_VincularBloqueoConRiesgo'),
            (N'20260826014231_EstructuraEntregablesActividades'),
            (N'20260826135216_DependenciasEntreActividades'),
            (N'20260826205435_RepositorioDocumentalProyectos'),
            (N'20260826223615_AuditoriaEnEntregablesYActividades'),
            (N'20260831005008_RegistroDescargasDocumentos'),
            (N'20260831022204_VincularReunionesYExpedientesAProyectos'),
            (N'20260831174631_VincularTicketsAProyectos'),
            (N'20260902215804_AgregarJefeDeAreaYPmo'),
            (N'20260902215902_AgregarAccionProyecto'),
            (N'20260902221131_AgregarInteresadoAutomatico'),
            (N'20260903175629_AgregarAccionTramite'),
            (N'20260904173357_AgregarBorradoLogicoUsuario'),
            (N'20260918155856_CatalogoDePrioridadesDeProyecto'),
            (N'20260918162251_CatalogoDePrioridadesDeTicket'),
            (N'20260918191759_CatalogoDeCategoriasDeProyecto'),
            (N'20260918193137_LasQSonCategoriasNoPrioridades'),
            (N'20260923223127_AddReunionEncuestaActiva')
        ) v(Id))
    SELECT  h.MigrationId AS SoloEnLaBase
    FROM    __EFMigrationsHistory h
    WHERE   NOT EXISTS (SELECT 1 FROM Codigo c WHERE c.Id = h.MigrationId)
    ORDER BY h.MigrationId;
END;

PRINT '';
PRINT '════════════════ 8. Qué hay que correr ════════════════';

DECLARE @tablasFaltan      int = 0;
DECLARE @migracionesFaltan int = 0;
DECLARE @faltan0918        int = 0;
DECLARE @columnasFaltan    int = 0;
DECLARE @hayHistorial      bit = CASE WHEN OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NULL
                                      THEN 0 ELSE 1 END;

;WITH Modelo(Tabla) AS (SELECT * FROM (VALUES
        (N'AcuerdosReunion'), (N'Areas'), (N'AsignacionesUsuario'),
        (N'Asistentes'), (N'BitacoraExpediente'), (N'BitacoraProyecto'),
        (N'CategoriasDocumento'), (N'CategoriasProyecto'), (N'CategoriasTicket'),
        (N'CategoriasTramite'), (N'ChatMensajes'), (N'ChatSesiones'),
        (N'ComentariosCompromisos'), (N'ConciliacionesSiger'), (N'Contactos'),
        (N'DocumentosInternos'), (N'DocumentosSolicitados'), (N'EnlacesSiger'),
        (N'EntregablesSiger'), (N'ExpedienteEtapaAvances'), (N'ExpedienteEtapaCronogramas'),
        (N'ExpedienteSecciones'), (N'ExpedienteTramites'), (N'Expedientes'),
        (N'FlujoNodos'), (N'FundamentosLegales'), (N'InfraChecklist'),
        (N'InfraCondiciones'), (N'InfraPerfiles'), (N'Instituciones'),
        (N'LevantamientoDocumentos'), (N'LevantamientoEquipo'), (N'LevantamientoTramites'),
        (N'Levantamientos'), (N'LugaresAtencionSiger'), (N'Movimientos'),
        (N'NotasSeguimientoExpediente'), (N'Notificaciones'), (N'PasosSiger'),
        (N'Permisos'), (N'PermisosAuditoria'), (N'PlanTrabajo'),
        (N'PlanTrabajoMetas'), (N'PlantillaFundamentosLegales'), (N'PlantillaRequisitos'),
        (N'PlantillasTramite'), (N'Prefijos'), (N'PrioridadesProyecto'),
        (N'PrioridadesTicket'), (N'ProyectoActividades'), (N'ProyectoAvances'),
        (N'ProyectoDependenciasActividad'), (N'ProyectoDocumentoDescargas'), (N'ProyectoDocumentoVersiones'),
        (N'ProyectoDocumentos'), (N'ProyectoEntregables'), (N'ProyectoExpedientes'),
        (N'ProyectoInteresados'), (N'ProyectoReuniones'), (N'ProyectoRiesgos'),
        (N'ProyectoTickets'), (N'Proyectos'), (N'Recursos'),
        (N'RequisitosSiger'), (N'ReunionInstituciones'), (N'Reuniones'),
        (N'RolModuloAccesos'), (N'RolPermisos'), (N'Roles'),
        (N'TareasDigitalizacionSiger'), (N'TemasTicket'), (N'TicketAdjuntos'),
        (N'TicketComentarios'), (N'TicketTramites'), (N'Tickets'),
        (N'TramiteRequisitos'), (N'TramitesDefinicion'), (N'TramitesSiger'),
        (N'Unidades'), (N'UsuarioTemas'), (N'Usuarios')
    ) v(Tabla))
SELECT @tablasFaltan = SUM(CASE WHEN OBJECT_ID(QUOTENAME(Tabla), 'U') IS NULL THEN 1 ELSE 0 END)
FROM   Modelo;

SELECT @columnasFaltan = SUM(CASE WHEN OBJECT_ID(QUOTENAME(v.Tabla), 'U') IS NOT NULL
                                   AND COL_LENGTH(v.Tabla, v.Columna) IS NULL THEN 1 ELSE 0 END)
FROM (VALUES (N'Asistentes',    N'EsPreregistro'),
             (N'Asistentes',    N'Confirmado'),
             (N'Instituciones', N'Color'),
             (N'Reuniones',     N'EncuestaActiva')) v(Tabla, Columna);

IF @hayHistorial = 1
BEGIN
    ;WITH Codigo(Id) AS (SELECT * FROM (VALUES
            (N'20260702225346_AddContactoActivo'),
            (N'20260706200739_InitialCreate'),
            (N'20260706202412_AddDigerInstitucionSeed'),
            (N'20260707120000_AddTicketTemaOtro'),
            (N'20260707151717_AddReunionInstituciones'),
            (N'20260707154325_AddSoftDelete'),
            (N'20260708165625_AddSpGenerarCodigoMovimiento'),
            (N'20260708204449_FixPendingModelChanges'),
            (N'20260709163652_UpdateInstitucionesSeed'),
            (N'20260709205323_AddCertificadoThumbprint'),
            (N'20260709221010_FixContactoActivoDefault'),
            (N'20260709221250_AddReunionSatisfaccionCalificacion'),
            (N'20260709221700_SwapExpedienteSeccionEstadoOrden'),
            (N'20260709222307_AddPlantillaTramite'),
            (N'20260717202416_AddReunionHilo'),
            (N'20260720161207_AddNotificaciones'),
            (N'20260721151629_AddChatSoporte'),
            (N'20260721152706_AddActivoToAreaAndUnidad'),
            (N'20260721195522_AddComentariosCompromisos'),
            (N'20260722173346_AddLevantamientosAndCronograma'),
            (N'20260722182710_AddPlanTrabajo'),
            (N'20260722194801_AddMetaTramiteIndex'),
            (N'20260722200241_AddMetaResponsableUsuario'),
            (N'20260722201107_AddExpedienteAnalistaUsuario'),
            (N'20260722203223_AddAsistenteInstitucionCatalogo'),
            (N'20260722203942_AddAcuerdoResponsableContacto'),
            (N'20260723161230_AddPasswordResetTokenToUsuario'),
            (N'20260723170218_AddAreaToContacto'),
            (N'20260724153537_AddRecursosTable'),
            (N'20260727195213_AddReunionExpedienteYContraparte'),
            (N'20260731224946_AddNotasSeguimientoExpediente'),
            (N'20260801022203_AddInventarioSiger'),
            (N'20260801023442_AjustarLongitudesSiger'),
            (N'20260801024207_AjustarFormatoRequisito'),
            (N'20260801024942_AjustarDocSoporte'),
            (N'20260801030023_AmpliarCamposSiger'),
            (N'20260801031904_AmpliarCamposSiger2'),
            (N'20260801041054_LinkSigerInstitucion'),
            (N'20260803023243_LinkExpedienteTramiteSiger'),
            (N'20260803204352_AddTramiteFechaCreacionYEstado'),
            (N'20260805150429_AuditoriaExpedientes'),
            (N'20260810194553_AddConciliacionSiger'),
            (N'20260812174438_AddPermisosModule'),
            (N'20260813143220_AddRolesDinamicos'),
            (N'20260814160739_AgregarCamposVentanilla'),
            (N'20260814162437_CorregirColacionBusqueda'),
            (N'20260823005328_AgregarProyectos'),
            (N'20260823164015_AgregarSelloEdicionAvance'),
            (N'20260823172749_AgregarBitacoraProyecto'),
            (N'20260824144118_AgregarAlcanceProyecto'),
            (N'20260824174815_AgregarRiesgosEInteresados'),
            (N'20260824224817_InteresadoUsuarioObligatorio'),
            (N'20260825150115_VincularBloqueoConRiesgo'),
            (N'20260826014231_EstructuraEntregablesActividades'),
            (N'20260826135216_DependenciasEntreActividades'),
            (N'20260826205435_RepositorioDocumentalProyectos'),
            (N'20260826223615_AuditoriaEnEntregablesYActividades'),
            (N'20260831005008_RegistroDescargasDocumentos'),
            (N'20260831022204_VincularReunionesYExpedientesAProyectos'),
            (N'20260831174631_VincularTicketsAProyectos'),
            (N'20260902215804_AgregarJefeDeAreaYPmo'),
            (N'20260902215902_AgregarAccionProyecto'),
            (N'20260902221131_AgregarInteresadoAutomatico'),
            (N'20260903175629_AgregarAccionTramite'),
            (N'20260904173357_AgregarBorradoLogicoUsuario'),
            (N'20260918155856_CatalogoDePrioridadesDeProyecto'),
            (N'20260918162251_CatalogoDePrioridadesDeTicket'),
            (N'20260918191759_CatalogoDeCategoriasDeProyecto'),
            (N'20260918193137_LasQSonCategoriasNoPrioridades'),
            (N'20260923223127_AddReunionEncuestaActiva')
        ) v(Id))
    SELECT @migracionesFaltan = COUNT(*),
           @faltan0918 = SUM(CASE WHEN c.Id LIKE N'20260918%' THEN 1 ELSE 0 END)
    FROM   Codigo c
    WHERE  NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory h WHERE h.MigrationId = c.Id);
END;

SELECT  ISNULL(@tablasFaltan, 0)      AS TablasFaltantes,
        ISNULL(@columnasFaltan, 0)    AS ColumnasFaltantes,
        ISNULL(@migracionesFaltan, 0) AS MigracionesSinRegistrar;

SELECT  Orden, Recomendacion
FROM (
    SELECT 1 AS Orden,
           N'PARE: no existe __EFMigrationsHistory. Revise la base a mano antes de correr nada.' AS Recomendacion
    WHERE  @hayHistorial = 0
    UNION ALL
    SELECT 2,
           N'PARE: faltan ' + CAST(@tablasFaltan AS nvarchar(10))
         + N' tablas completas (ver sección 1). Esta base está muy atrasada; los scripts de '
         + N'«poner al día» agregan columnas, no módulos enteros. Avise antes de seguir.'
    WHERE  ISNULL(@tablasFaltan, 0) > 0
    UNION ALL
    SELECT 3,
           N'Corra primero database\2026-09-18_poner_al_dia.sql (faltan '
         + CAST(@faltan0918 AS nvarchar(10)) + N' migraciones de esa fecha).'
    WHERE  ISNULL(@faltan0918, 0) > 0
    UNION ALL
    SELECT 4,
           N'Corra database\2026-09-24_poner_al_dia.sql (faltan columnas o la migración de la encuesta).'
    WHERE  ISNULL(@columnasFaltan, 0) > 0
        OR EXISTS (SELECT 1 FROM (VALUES (1)) x(v)
                   WHERE @hayHistorial = 1
                     AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory h
                                     WHERE h.MigrationId = N'20260923223127_AddReunionEncuestaActiva'))
    UNION ALL
    SELECT 5,
           N'Nada que hacer: esta base ya corre el código de hoy.'
    WHERE  @hayHistorial = 1
      AND  ISNULL(@tablasFaltan, 0) = 0
      AND  ISNULL(@columnasFaltan, 0) = 0
      AND  ISNULL(@migracionesFaltan, 0) = 0
) r
ORDER BY Orden;

PRINT '';
PRINT '════════════════ fin del diagnóstico ════════════════';
GO
