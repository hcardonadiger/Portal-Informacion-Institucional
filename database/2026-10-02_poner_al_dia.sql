/* ═══════════════════════════════════════════════════════════════════════════════════════
   Poner al día la base con los cambios al 2026-10-02
   ───────────────────────────────────────────────────────────────────────────────────────
   Va DESPUÉS de 2026-09-24_poner_al_dia.sql. Corra antes 00_diagnostico.sql: él dice si
   hace falta aquél y si esta base está en condiciones de recibir éste.

   Trae las TRES migraciones que el código tiene y los scripts anteriores nunca cubrieron.
   Son lo único que este script crea, y es exactamente lo que «dotnet ef database update»
   habría aplicado. Al final se registran las tres en __EFMigrationsHistory para que EF no
   las repita.

   ┌─ 1. ModuloAmbitos ── migración 20260908161947_AgregarModuloAmbito ───────────────────
   │
   │  Recorta un módulo a una institución, área o unidad concretas. Es la tabla que vivió
   │  un tiempo sólo en un stash y en la rama rescate/modulo-ambito; de ahí que ninguna
   │  puesta al día anterior la mencione. ES MUY PROBABLE QUE ESTA BASE YA LA TENGA
   │  —se agregó a mano en su momento— y en ese caso el paso no hace nada.
   │
   │  El índice único (Modulo, InstitucionId, AreaId, UnidadId) es la regla de negocio: un
   │  mismo módulo no se recorta dos veces al mismo ámbito.
   │
   ├─ 2. TokensAccesoMcp ── migración 20260928160056_AddTokenAccesoMcp ───────────────────
   │
   │  Los tokens con los que un agente externo (Claude, típicamente) entra al portal por
   │  MCP. Guarda el token HASHEADO, nunca el token: HashSha256 es lo que se compara al
   │  autenticar, y Prefijo —único— es lo único legible, para que el dueño reconozca cuál
   │  es cuál en la lista sin que el valor completo vuelva a aparecer nunca.
   │
   │  ExpiraUtc, RevocadoUtc y UltimoUsoUtc son el ciclo de vida: cuándo caduca, cuándo se
   │  revocó a mano, y cuándo se usó por última vez. Un token revocado no se borra, se
   │  marca — así la auditoría conserva de quién era.
   │
   │  La FK a Usuarios va EN CASCADA: borrar un usuario se lleva sus tokens, que es lo que
   │  uno quiere —un token huérfano sigue abriendo la puerta—.
   │
   └─ 3. ReunionAdjuntos ── migración 20261002162236_AddReunionAdjuntos ──────────────────

      Guarda los documentos y enlaces del cierre de una reunión, en las dos tarjetas que
      ahora los capturan:

        · «Documentos y recursos»            → Seccion = 'Documento'
        · «Evidencia fotográfica y de video» → Seccion = 'Evidencia'

      Y distingue si la fila es un archivo que el portal guardó en disco o un enlace a algo
      que vive afuera:

        · Clase = 'Archivo' → Url es una ruta relativa bajo /uploads/reuniones
        · Clase = 'Enlace'  → Url es una dirección http/https (OneDrive, típicamente)

      Esa distinción es la que permite que un video de una capacitación no se coma el disco
      del servidor: el video se deja en OneDrive y acá queda su enlace.

   Qué NO trae, y conviene saberlo:

   · El cambio de las reuniones quitó del portal «Detalle de capacitación» —el objetivo
     formativo y el contenido— porque repetía el objetivo y el desarrollo de la memoria.
     Las columnas Reuniones.ObjetivoCap y Reuniones.Contenido SE QUEDAN donde están, con lo
     que ya tengan: nada las borra y nada las lee. No hay que hacer nada con ellas.

   · Tampoco cambia Reuniones.Foto1Url/Foto2Url. Los dos cupos de foto del acta siguen
     exactamente igual; la tabla nueva los acompaña, no los reemplaza.

   · Las cinco claves de permiso del módulo MCP (TokensMcp.Ver/Crear/Eliminar y
     TokensMcp.Admin.Ver/Eliminar) NO se insertan acá: el portal las descubre por reflexión
     al arrancar y sincroniza la tabla Permisos él mismo. Lo que ningún arranque hace es
     OTORGARLAS: salvo el Administrador —que aprueba todo por código—, nadie podrá emitir
     un token MCP hasta que alguien marque esas casillas en /Accesos/Permisos. Eso es a
     propósito, y es la única cosa de esta puesta al día que queda pendiente de una mano
     humana.

   Como el resto de los scripts de esta carpeta, no pregunta por __EFMigrationsHistory para
   decidir qué hacer: cada paso comprueba SU PROPIA condición contra el catálogo de la base
   —¿existe la tabla?, ¿existe el índice?—. Las filas del historial se escriben al final,
   así que si la corrida muriera a la mitad, esas filas no sirven para saber qué quedó
   hecho.

   Se puede correr las veces que haga falta. La segunda no hace nada.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-02_poner_al_dia.sql
   o pegarlo en SSMS y ejecutar.

     -b  detiene en el primer error. IMPORTANTE: sin esa bandera sqlcmd sigue de largo,
         llega al COMMIT y confirma lo que alcanzó a hacer.
     -I  QUOTED_IDENTIFIER ON.

   XACT_ABORT ON hace lo mismo desde adentro: cualquier error deshace TODO. O entra
   completo o no entra nada.
   ═══════════════════════════════════════════════════════════════════════════════════════ */

SET QUOTED_IDENTIFIER ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

PRINT '── 0. Comprobación previa ──────────────────────────────────────────';

/* Las dos tablas nuevas que llevan FK cuelgan de tablas que tienen que estar: TokensAccesoMcp
   de Usuarios y ReunionAdjuntos de Reuniones. Sin ellas no hay nada que crear, y seguir
   dejaría la transacción muriendo más abajo con un error de clave foránea que no le dice
   nada a quien corre el script.

   THROW y no RAISERROR: RAISERROR deja el mensaje pero NO corta el lote. */
IF OBJECT_ID(N'[Reuniones]', 'U') IS NULL
    THROW 50000, N'Falta la tabla Reuniones. Corra 00_diagnostico.sql y revise antes de seguir.', 1;

IF OBJECT_ID(N'[Usuarios]', 'U') IS NULL
    THROW 50000, N'Falta la tabla Usuarios. Corra 00_diagnostico.sql y revise antes de seguir.', 1;

PRINT '   tablas Reuniones y Usuarios presentes';

PRINT '── 1. Tabla ModuloAmbitos ──────────────────────────────────────────';

/* Columnas de auditoría incluidas: ModuloAmbito hereda de BaseAuditableEntity, y el modelo
   espera las cuatro. CreatedBy/UpdatedBy van nvarchar(max) porque así las declara EF —no es
   un descuido, es lo que la migración genera para una cadena sin longitud configurada—.

   AreaId y UnidadId son NULL-ables a propósito: un recorte a nivel de institución deja las
   dos vacías, uno a nivel de área deja UnidadId vacía. */
IF OBJECT_ID(N'[ModuloAmbitos]', 'U') IS NULL
BEGIN
    CREATE TABLE [ModuloAmbitos] (
        [Id]            int            IDENTITY(1,1) NOT NULL,
        [Modulo]        nvarchar(120)  NOT NULL,
        [InstitucionId] nvarchar(50)   NOT NULL,
        [AreaId]        nvarchar(50)   NULL,
        [UnidadId]      nvarchar(50)   NULL,
        [CreatedAt]     datetime2      NOT NULL,
        [CreatedBy]     nvarchar(max)  NULL,
        [UpdatedAt]     datetime2      NULL,
        [UpdatedBy]     nvarchar(max)  NULL,
        CONSTRAINT [PK_ModuloAmbitos] PRIMARY KEY ([Id])
    );
    PRINT '   tabla ModuloAmbitos creada';
END
ELSE
    PRINT '   tabla ModuloAmbitos ya estaba';

/* Los dos índices van dentro de EXEC por lo mismo de siempre: el lote se compila entero
   antes de ejecutarse, así que en la primera corrida la tabla de arriba todavía no existe
   para el compilador. */
IF OBJECT_ID(N'[ModuloAmbitos]', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_ModuloAmbitos_Modulo'
                     AND object_id = OBJECT_ID(N'[ModuloAmbitos]'))
BEGIN
    EXEC(N'CREATE INDEX [IX_ModuloAmbitos_Modulo] ON [ModuloAmbitos] ([Modulo]);');
    PRINT '   índice IX_ModuloAmbitos_Modulo creado';
END
ELSE
    PRINT '   índice IX_ModuloAmbitos_Modulo ya estaba';

/* El único. Es la regla: un módulo no se recorta dos veces al mismo ámbito. */
IF OBJECT_ID(N'[ModuloAmbitos]', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_ModuloAmbitos_Modulo_InstitucionId_AreaId_UnidadId'
                     AND object_id = OBJECT_ID(N'[ModuloAmbitos]'))
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ModuloAmbitos_Modulo_InstitucionId_AreaId_UnidadId]
             ON [ModuloAmbitos] ([Modulo], [InstitucionId], [AreaId], [UnidadId]);');
    PRINT '   índice único creado';
END
ELSE
    PRINT '   índice único ya estaba';

PRINT '── 2. Tabla TokensAccesoMcp ────────────────────────────────────────';

/* HashSha256 nvarchar(64): un SHA-256 en hexadecimal son 64 caracteres exactos. Acá no se
   guarda el token, se guarda su huella — si esta tabla se filtra, no hay nada que usar.

   AsignacionId no lleva FK (así lo declara el modelo): el token recuerda con qué asignación
   —institución + área/unidad + rol— se emitió, para que el agente externo entre con el
   mismo alcance que su dueño tenía en ese momento, y no con el que tenga después.

   ON DELETE CASCADE sobre Usuarios: un token sin dueño sigue abriendo la puerta. */
IF OBJECT_ID(N'[TokensAccesoMcp]', 'U') IS NULL
BEGIN
    CREATE TABLE [TokensAccesoMcp] (
        [Id]           uniqueidentifier NOT NULL,
        [UsuarioId]    uniqueidentifier NOT NULL,
        [AsignacionId] uniqueidentifier NOT NULL,
        [Nombre]       nvarchar(120)    NOT NULL,
        [Prefijo]      nvarchar(32)     NOT NULL,
        [HashSha256]   nvarchar(64)     NOT NULL,
        [ExpiraUtc]    datetime2        NULL,
        [RevocadoUtc]  datetime2        NULL,
        [UltimoUsoUtc] datetime2        NULL,
        [CreatedAt]    datetime2        NOT NULL,
        [CreatedBy]    nvarchar(max)    NULL,
        [UpdatedAt]    datetime2        NULL,
        [UpdatedBy]    nvarchar(max)    NULL,
        CONSTRAINT [PK_TokensAccesoMcp] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TokensAccesoMcp_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId])
            REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
    );
    PRINT '   tabla TokensAccesoMcp creada';
END
ELSE
    PRINT '   tabla TokensAccesoMcp ya estaba';

/* Único: el prefijo es por el que se busca el token al autenticar, así que dos tokens no
   pueden compartirlo. */
IF OBJECT_ID(N'[TokensAccesoMcp]', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_TokensAccesoMcp_Prefijo'
                     AND object_id = OBJECT_ID(N'[TokensAccesoMcp]'))
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TokensAccesoMcp_Prefijo]
             ON [TokensAccesoMcp] ([Prefijo]);');
    PRINT '   índice único de Prefijo creado';
END
ELSE
    PRINT '   índice único de Prefijo ya estaba';

IF OBJECT_ID(N'[TokensAccesoMcp]', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_TokensAccesoMcp_UsuarioId'
                     AND object_id = OBJECT_ID(N'[TokensAccesoMcp]'))
BEGIN
    EXEC(N'CREATE INDEX [IX_TokensAccesoMcp_UsuarioId]
             ON [TokensAccesoMcp] ([UsuarioId]);');
    PRINT '   índice de UsuarioId creado';
END
ELSE
    PRINT '   índice de UsuarioId ya estaba';

PRINT '── 3. Tabla ReunionAdjuntos ────────────────────────────────────────';

/* Seccion y Clase van como texto y no como número a propósito: estas filas se leen a mano
   cuando algo no cuadra en un despliegue, y un 'Evidencia'/'Enlace' legible vale más que un
   2. Es el mismo criterio que ya sigue Reuniones.Visibilidad.

   Url es nvarchar(1000) y no 600 como Foto1Url: un enlace de OneDrive compartido trae el
   token en el query string y pasa largo de los 600 que alcanzan para una ruta local.

   ON DELETE CASCADE: los adjuntos no tienen vida propia fuera de su reunión. */
IF OBJECT_ID(N'[ReunionAdjuntos]', 'U') IS NULL
BEGIN
    CREATE TABLE [ReunionAdjuntos] (
        [Id]            int            IDENTITY(1,1) NOT NULL,
        [ReunionId]     int            NOT NULL,
        [Orden]         int            NOT NULL,
        [Seccion]       nvarchar(20)   NOT NULL,
        [Clase]         nvarchar(20)   NOT NULL,
        [Titulo]        nvarchar(250)  NOT NULL,
        [Descripcion]   nvarchar(500)  NULL,
        [NombreArchivo] nvarchar(260)  NULL,
        [Url]           nvarchar(1000) NOT NULL,
        [Tamano]        bigint         NOT NULL,
        CONSTRAINT [PK_ReunionAdjuntos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReunionAdjuntos_Reuniones_ReunionId] FOREIGN KEY ([ReunionId])
            REFERENCES [Reuniones] ([Id]) ON DELETE CASCADE
    );
    PRINT '   tabla ReunionAdjuntos creada';
END
ELSE
    PRINT '   tabla ReunionAdjuntos ya estaba';

/* Es por el que se listan: todo lo de una reunión, agrupado por tarjeta y en el orden en que
   se capturó. */
IF OBJECT_ID(N'[ReunionAdjuntos]', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_ReunionAdjuntos_ReunionId_Seccion_Orden'
                     AND object_id = OBJECT_ID(N'[ReunionAdjuntos]'))
BEGIN
    EXEC(N'CREATE INDEX [IX_ReunionAdjuntos_ReunionId_Seccion_Orden]
             ON [ReunionAdjuntos] ([ReunionId], [Seccion], [Orden]);');
    PRINT '   índice creado';
END
ELSE
    PRINT '   índice ya estaba';

PRINT '── 4. Historial de migraciones ─────────────────────────────────────';

/* Las tres tablas de arriba sí corresponden a migraciones que EF reconoce, así que se
   registran. */
IF OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NOT NULL
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT v.Id, N'9.0.0'
    FROM   (VALUES
              (N'20260908161947_AgregarModuloAmbito'),
              (N'20260928160056_AddTokenAccesoMcp'),
              (N'20261002162236_AddReunionAdjuntos')
           ) v(Id)
    WHERE  NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory h WHERE h.MigrationId = v.Id);
    PRINT '   historial al día';
END
ELSE
    PRINT '   *** no existe __EFMigrationsHistory: no se registró nada';

COMMIT;
GO

PRINT '';
PRINT '════════ Comprobación ════════';

/* 1. Las tres tablas */
SELECT  v.Control,
        CASE WHEN OBJECT_ID(QUOTENAME(v.Tabla), 'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
FROM (VALUES
        (N'Tabla ModuloAmbitos',    N'ModuloAmbitos'),
        (N'Tabla TokensAccesoMcp',  N'TokensAccesoMcp'),
        (N'Tabla ReunionAdjuntos',  N'ReunionAdjuntos')
     ) v(Control, Tabla)

/* 2. Columna por columna, que es donde se nota una tabla creada a medias en otro momento */
UNION ALL
SELECT  v.Control,
        CASE WHEN OBJECT_ID(QUOTENAME(v.Tabla), 'U') IS NULL THEN 'NO EXISTE LA TABLA'
             WHEN COL_LENGTH(v.Tabla, v.Columna) IS NULL     THEN 'FALTA'
             ELSE 'presente' END
FROM (VALUES
        (N'ModuloAmbitos.Modulo',          N'ModuloAmbitos',   N'Modulo'),
        (N'ModuloAmbitos.InstitucionId',   N'ModuloAmbitos',   N'InstitucionId'),
        (N'ModuloAmbitos.AreaId',          N'ModuloAmbitos',   N'AreaId'),
        (N'ModuloAmbitos.UnidadId',        N'ModuloAmbitos',   N'UnidadId'),
        (N'ModuloAmbitos.CreatedAt',       N'ModuloAmbitos',   N'CreatedAt'),

        (N'TokensAccesoMcp.UsuarioId',     N'TokensAccesoMcp', N'UsuarioId'),
        (N'TokensAccesoMcp.AsignacionId',  N'TokensAccesoMcp', N'AsignacionId'),
        (N'TokensAccesoMcp.Nombre',        N'TokensAccesoMcp', N'Nombre'),
        (N'TokensAccesoMcp.Prefijo',       N'TokensAccesoMcp', N'Prefijo'),
        (N'TokensAccesoMcp.HashSha256',    N'TokensAccesoMcp', N'HashSha256'),
        (N'TokensAccesoMcp.ExpiraUtc',     N'TokensAccesoMcp', N'ExpiraUtc'),
        (N'TokensAccesoMcp.RevocadoUtc',   N'TokensAccesoMcp', N'RevocadoUtc'),
        (N'TokensAccesoMcp.UltimoUsoUtc',  N'TokensAccesoMcp', N'UltimoUsoUtc'),

        (N'ReunionAdjuntos.ReunionId',     N'ReunionAdjuntos', N'ReunionId'),
        (N'ReunionAdjuntos.Orden',         N'ReunionAdjuntos', N'Orden'),
        (N'ReunionAdjuntos.Seccion',       N'ReunionAdjuntos', N'Seccion'),
        (N'ReunionAdjuntos.Clase',         N'ReunionAdjuntos', N'Clase'),
        (N'ReunionAdjuntos.Titulo',        N'ReunionAdjuntos', N'Titulo'),
        (N'ReunionAdjuntos.Descripcion',   N'ReunionAdjuntos', N'Descripcion'),
        (N'ReunionAdjuntos.NombreArchivo', N'ReunionAdjuntos', N'NombreArchivo'),
        (N'ReunionAdjuntos.Url',           N'ReunionAdjuntos', N'Url'),
        (N'ReunionAdjuntos.Tamano',        N'ReunionAdjuntos', N'Tamano')
     ) v(Control, Tabla, Columna)

/* 3. Las dos claves foráneas en cascada */
UNION ALL
SELECT  v.Control,
        CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = v.Fk)
             THEN 'presente' ELSE 'FALTA' END
FROM (VALUES
        (N'FK TokensAccesoMcp → Usuarios',  N'FK_TokensAccesoMcp_Usuarios_UsuarioId'),
        (N'FK ReunionAdjuntos → Reuniones', N'FK_ReunionAdjuntos_Reuniones_ReunionId')
     ) v(Control, Fk)

/* 4. Los cinco índices */
UNION ALL
SELECT  v.Ix,
        CASE WHEN OBJECT_ID(QUOTENAME(v.Tabla), 'U') IS NULL THEN 'NO EXISTE LA TABLA'
             WHEN EXISTS (SELECT 1 FROM sys.indexes
                          WHERE name = v.Ix AND object_id = OBJECT_ID(QUOTENAME(v.Tabla)))
             THEN 'presente' ELSE 'FALTA' END
FROM (VALUES
        (N'IX_ModuloAmbitos_Modulo',                               N'ModuloAmbitos'),
        (N'IX_ModuloAmbitos_Modulo_InstitucionId_AreaId_UnidadId', N'ModuloAmbitos'),
        (N'IX_TokensAccesoMcp_Prefijo',                            N'TokensAccesoMcp'),
        (N'IX_TokensAccesoMcp_UsuarioId',                          N'TokensAccesoMcp'),
        (N'IX_ReunionAdjuntos_ReunionId_Seccion_Orden',            N'ReunionAdjuntos')
     ) v(Ix, Tabla)

/* 5. Las tres filas del historial */
UNION ALL
SELECT  N'Migración ' + v.Id,
        CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = v.Id)
             THEN 'presente' ELSE 'FALTA' END
FROM (VALUES
        (N'20260908161947_AgregarModuloAmbito'),
        (N'20260928160056_AddTokenAccesoMcp'),
        (N'20261002162236_AddReunionAdjuntos')
     ) v(Id);

PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
PRINT '';
PRINT 'Queda UNA cosa a mano, y el script no puede hacerla: otorgar las claves TokensMcp.*';
PRINT 'en /Accesos/Permisos al rol que deba emitir tokens MCP. El Administrador no las';
PRINT 'necesita —aprueba todo por código—; cualquier otro rol sí.';
GO
