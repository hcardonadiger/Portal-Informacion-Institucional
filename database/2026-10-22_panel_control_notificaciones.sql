/* ════════════════════════════════════════════════════════════════════════════
   2026-10-22 · Panel de control → Notificaciones
   Va DESPUÉS de 2026-10-19_cambio_password_obligatorio.sql. Corra antes
   00_diagnostico.sql si no sabe en qué estado está la base.

   QUÉ TRAE (todo es la migración EF 20261022223203_PanelControlNotificaciones):
     1. Notificaciones.Cantidad y Notificaciones.FechaActualizacion: avisos agrupados.
     2. NotificacionElementos: los elementos de cada aviso, con los avisos existentes
        cargados (uno por aviso) para que el primer día no se repita lo ya avisado.
     3. AjustesNotificaciones (fila única): intervalo de revisión, correo saliente.
     4. ModulosNotificacion: interruptores Campana / Correo / Agrupar por módulo.
        Nacen con el CORREO APAGADO en los cinco: los recordatorios manuales dejan
        de mandar correo hasta que se encienda en el panel.
     5. CorreosPendientes: bandeja de salida.
     6. PanelControlBitacora: quién cambió qué en el panel.

   Cada paso se decide contra el catálogo de la base, no contra __EFMigrationsHistory.
   La fila del historial se escribe al final. Correrlo dos veces no cambia nada.

   CÓMO CORRERLO: sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-22_panel_control_notificaciones.sql
   ════════════════════════════════════════════════════════════════════════════ */
SET XACT_ABORT ON;
SET NOCOUNT ON;

IF OBJECT_ID(N'[Notificaciones]', 'U') IS NULL
    THROW 50001, N'No existe la tabla Notificaciones: esta no es una base del portal o le faltan scripts anteriores. Corra 00_diagnostico.sql.', 1;
GO

BEGIN TRANSACTION;

PRINT '── 1. Notificaciones: Cantidad y FechaActualizacion ──────────────────';
IF COL_LENGTH('Notificaciones', 'Cantidad') IS NULL
BEGIN
    ALTER TABLE Notificaciones ADD Cantidad int NOT NULL CONSTRAINT DF_Notificaciones_Cantidad DEFAULT 1;
    PRINT '   Cantidad agregada';
END ELSE PRINT '   Cantidad ya estaba';

IF COL_LENGTH('Notificaciones', 'FechaActualizacion') IS NULL
BEGIN
    ALTER TABLE Notificaciones ADD FechaActualizacion datetime2 NOT NULL
        CONSTRAINT DF_Notificaciones_FechaActualizacion DEFAULT '0001-01-01T00:00:00';
    EXEC (N'UPDATE Notificaciones SET FechaActualizacion = FechaCreacion;');
    PRINT '   FechaActualizacion agregada y cargada';
END ELSE PRINT '   FechaActualizacion ya estaba';

PRINT '── 2. NotificacionElementos ──────────────────────────────────────────';
IF OBJECT_ID(N'[NotificacionElementos]', 'U') IS NULL
BEGIN
    CREATE TABLE NotificacionElementos (
        Id             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificacionElementos PRIMARY KEY,
        NotificacionId int            NOT NULL,
        Clave          nvarchar(500)  NOT NULL,
        Titulo         nvarchar(200)  NOT NULL,
        Url            nvarchar(500)  NULL,
        FechaUtc       datetime2      NOT NULL,
        CONSTRAINT FK_NotificacionElementos_Notificaciones_NotificacionId
            FOREIGN KEY (NotificacionId) REFERENCES Notificaciones (Id) ON DELETE CASCADE);
    CREATE INDEX IX_NotificacionElementos_NotificacionId ON NotificacionElementos (NotificacionId);
    CREATE INDEX IX_NotificacionElementos_Clave_FechaUtc ON NotificacionElementos (Clave, FechaUtc);
    EXEC (N'INSERT INTO NotificacionElementos (NotificacionId, Clave, Titulo, Url, FechaUtc)
            SELECT Id, LEFT(COALESCE(Url, Titulo), 500), Titulo, Url, FechaCreacion FROM Notificaciones;');
    PRINT '   creada y cargada con los avisos existentes';
END ELSE PRINT '   ya estaba';

PRINT '── 3. AjustesNotificaciones ──────────────────────────────────────────';
IF OBJECT_ID(N'[AjustesNotificaciones]', 'U') IS NULL
BEGIN
    CREATE TABLE AjustesNotificaciones (
        Id                  int            NOT NULL CONSTRAINT PK_AjustesNotificaciones PRIMARY KEY,
        IntervaloMinutos    int            NOT NULL,
        SmtpConfigurado     bit            NOT NULL,
        SmtpHost            nvarchar(200)  NULL,
        SmtpPuerto          int            NOT NULL,
        SmtpSsl             bit            NOT NULL,
        SmtpUsuario         nvarchar(200)  NULL,
        SmtpRemitente       nvarchar(200)  NULL,
        SmtpNombreRemitente nvarchar(200)  NULL,
        SmtpPasswordCifrada nvarchar(max)  NULL,
        UltimaRevisionUtc   datetime2      NULL,
        CreatedAt           datetime2      NOT NULL,
        CreatedBy           nvarchar(200)  NULL,
        UpdatedAt           datetime2      NULL,
        UpdatedBy           nvarchar(200)  NULL);
    PRINT '   creada';
END ELSE PRINT '   ya estaba';

IF NOT EXISTS (SELECT 1 FROM AjustesNotificaciones WHERE Id = 1)
BEGIN
    INSERT INTO AjustesNotificaciones (Id, IntervaloMinutos, SmtpConfigurado, SmtpPuerto, SmtpSsl, CreatedAt)
    VALUES (1, 240, 0, 587, 1, SYSUTCDATETIME());
    PRINT '   fila inicial: revisión cada 4 h, correo desde appsettings';
END ELSE PRINT '   fila inicial ya estaba';

PRINT '── 4. ModulosNotificacion ────────────────────────────────────────────';
IF OBJECT_ID(N'[ModulosNotificacion]', 'U') IS NULL
BEGIN
    CREATE TABLE ModulosNotificacion (
        Modulo        nvarchar(30) NOT NULL CONSTRAINT PK_ModulosNotificacion PRIMARY KEY,
        CampanaActiva bit          NOT NULL,
        CorreoActivo  bit          NOT NULL,
        Agrupar       bit          NOT NULL);
    PRINT '   creada';
END ELSE PRINT '   ya estaba';

INSERT INTO ModulosNotificacion (Modulo, CampanaActiva, CorreoActivo, Agrupar)
SELECT v.Modulo, 1, 0, 1
FROM (VALUES (N'Tickets'), (N'Reuniones'), (N'Expedientes'), (N'Proyectos'), (N'Soporte')) v(Modulo)
WHERE NOT EXISTS (SELECT 1 FROM ModulosNotificacion m WHERE m.Modulo = v.Modulo);
PRINT CONCAT('   módulos agregados: ', @@ROWCOUNT);

PRINT '── 5. CorreosPendientes ──────────────────────────────────────────────';
IF OBJECT_ID(N'[CorreosPendientes]', 'U') IS NULL
BEGIN
    CREATE TABLE CorreosPendientes (
        Id             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CorreosPendientes PRIMARY KEY,
        DestinatarioId uniqueidentifier NOT NULL,
        Modulo         nvarchar(30)     NOT NULL,
        Tipo           nvarchar(30)     NOT NULL,
        Titulo         nvarchar(200)    NOT NULL,
        Url            nvarchar(500)    NULL,
        Clave          nvarchar(500)    NOT NULL,
        CreadoUtc      datetime2        NOT NULL,
        Estado         nvarchar(20)     NOT NULL,
        Intentos       int              NOT NULL,
        UltimoError    nvarchar(500)    NULL,
        EnviadoUtc     datetime2        NULL);
    CREATE INDEX IX_CorreosPendientes_Estado_DestinatarioId ON CorreosPendientes (Estado, DestinatarioId);
    PRINT '   creada';
END ELSE PRINT '   ya estaba';

PRINT '── 6. PanelControlBitacora ───────────────────────────────────────────';
IF OBJECT_ID(N'[PanelControlBitacora]', 'U') IS NULL
BEGIN
    CREATE TABLE PanelControlBitacora (
        Id      int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PanelControlBitacora PRIMARY KEY,
        Seccion nvarchar(100)  NOT NULL,
        Detalle nvarchar(1000) NOT NULL,
        Actor   nvarchar(200)  NOT NULL,
        Fecha   datetime2      NOT NULL);
    CREATE INDEX IX_PanelControlBitacora_Fecha ON PanelControlBitacora (Fecha);
    PRINT '   creada';
END ELSE PRINT '   ya estaba';

PRINT '── 7. Historial de migraciones ───────────────────────────────────────';
IF OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NOT NULL
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT N'20261022223203_PanelControlNotificaciones', N'9.0.0'
    WHERE NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261022223203_PanelControlNotificaciones');
    PRINT '   historial al día';
END
ELSE PRINT '   *** no existe __EFMigrationsHistory: no se registró nada';

COMMIT;
GO

PRINT '';
PRINT '════════ Comprobación ════════';
SELECT v.Control,
       CASE WHEN COL_LENGTH(v.Tabla, v.Columna) IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
FROM (VALUES
        (N'Notificaciones.Cantidad',           N'Notificaciones',        N'Cantidad'),
        (N'Notificaciones.FechaActualizacion', N'Notificaciones',        N'FechaActualizacion'),
        (N'NotificacionElementos',             N'NotificacionElementos', N'Clave'),
        (N'AjustesNotificaciones',             N'AjustesNotificaciones', N'IntervaloMinutos'),
        (N'ModulosNotificacion',               N'ModulosNotificacion',   N'CorreoActivo'),
        (N'CorreosPendientes',                 N'CorreosPendientes',     N'Estado'),
        (N'PanelControlBitacora',              N'PanelControlBitacora',  N'Detalle')
     ) v(Control, Tabla, Columna)
UNION ALL
SELECT N'Fila de ajustes', CASE WHEN EXISTS (SELECT 1 FROM AjustesNotificaciones WHERE Id = 1) THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Cinco módulos', CASE WHEN (SELECT COUNT(*) FROM ModulosNotificacion) = 5 THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Migración registrada',
       CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261022223203_PanelControlNotificaciones') THEN 'presente' ELSE 'FALTA' END;

PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
