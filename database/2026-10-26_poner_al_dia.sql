/* ╔══════════════════════════════════════════════════════════════════════════════════╗
   ║  2026-10-26 — Poner al día la base de PRODUCCIÓN con el código de ike_opt          ║
   ╚══════════════════════════════════════════════════════════════════════════════════╝

   QUÉ ES
   Un solo script que reúne, en orden y SIN REESCRIBIRLOS, los nueve «poner al día»
   escritos desde el 2026-10-02. Cada parte conserva su propio encabezado, que explica qué
   trae y por qué. Lo único nuevo de este archivo es la unión, esta guarda inicial y la
   comprobación final. Los cinco scripts SIGER que el del 2026-10-22 encadenaba con :r
   (scripts/sql/11 a 15) van ahora en línea, para que no haga falta modo SQLCMD ni correrlo
   desde la raíz del repositorio.

   VA DESPUÉS DE: 2026-09-24_poner_al_dia.sql. Es el estado de producción al 2026-10-26
   (respaldo GestionGD_021026, última migración registrada 20260923223127_AddReunionEncuestaActiva).
   Corra antes 00_diagnostico.sql si no está seguro de en qué estado está la base.

   QUÉ TRAE
     Parte 1  2026-10-02  TokensAccesoMcp, ReunionAdjuntos (y ModuloAmbitos si faltara)     [EF]
     Parte 2  2026-10-12  colores de los seis roles sembrados al Manual de Marca            [datos]
     Parte 3  2026-10-14  Reuniones.InstitucionDuenaId y su relleno; dueño de las reuniones [EF + datos]
     Parte 4  2026-10-19  PlantillasProyecto, PlantillaEntregables, PlantillaActividades    [EF]
     Parte 5  2026-10-19  Usuarios.PasswordCambiadoEl                                       [EF]
     Parte 6  2026-10-22  diez migraciones SIGER de agosto (scripts 11 a 15)                [EF + datos]
     Parte 7  2026-10-22  CategoriasInsumo, Insumos, ProyectoActividadInsumos               [EF + siembra]
     Parte 8  2026-10-22  panel de control de notificaciones (seis objetos)                 [EF + datos]
     Parte 9  2026-10-23  ConexionesMicrosoft y nueve columnas Outlook* en Reuniones        [EF]

   [EF] = corresponde a una migración real del código y se registra en __EFMigrationsHistory.
   Son 18 en total. Las partes 2, el paso 5 de la parte 3 y la siembra de la parte 7 NO son
   migraciones y no escriben historial.

   ESTE SCRIPT TOCA DATOS, NO SOLO ESQUEMA. RESPALDE LA BASE ANTES DE CORRERLO.
     · Parte 3: rellena la institución dueña y atribuye unidad a las reuniones sin dueño.
     · Parte 6: rellena la clave estable de las conciliaciones SIGER y BORRA las que apuntan
       a un trámite de expediente que ya no existe; reduce Modalidad al catálogo
       Virtual / Presencial / Hibrido (el texto original queda en ModalidadDetalle).
     · Parte 8: crea los interruptores por módulo con el CORREO APAGADO en los cinco. Desde
       que corra, los recordatorios no mandan correo hasta que se enciendan en el panel.
   Ninguna otra fila se borra.

   CÓMO SE COMPORTA
   Cada paso decide contra el catálogo de la base, no contra __EFMigrationsHistory (salvo los
   scripts 11 a 15, que van precedidos por la guarda de coherencia de la parte 6). Se puede
   correr las veces que haga falta: la segunda corrida no cambia nada.

   CÓMO CORRERLO (con -b: si una parte falla, la corrida se detiene ahí)
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-26_poner_al_dia.sql -o salida.txt
   En SSMS un error NO detiene los lotes siguientes. Si lo corre ahí, revise la salida
   completa y no dé por buena la corrida si apareció un error, aunque la comprobación final
   salga bien.

   AL TERMINAR, la última tabla («Comprobación final») debe decir «presente» en todas sus filas.
   ══════════════════════════════════════════════════════════════════════════════════════ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ── Guarda: ¿es ésta la base del portal, y está al nivel de 2026-09-24? ───────────── */
IF OBJECT_ID(N'[Reuniones]', N'U') IS NULL OR OBJECT_ID(N'[Usuarios]', N'U') IS NULL
   OR OBJECT_ID(N'[Instituciones]', N'U') IS NULL OR OBJECT_ID(N'[Proyectos]', N'U') IS NULL
   OR OBJECT_ID(N'[Notificaciones]', N'U') IS NULL OR OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
    THROW 50000, N'Faltan tablas base del portal. Corra 00_diagnostico.sql: no es la base del portal o está incompleta.', 1;

IF COL_LENGTH(N'Reuniones', N'EncuestaActiva') IS NULL
    THROW 50000, N'Falta Reuniones.EncuestaActiva: la base no llegó al 2026-09-24. Corra antes 2026-09-24_poner_al_dia.sql (00_diagnostico.sql lo confirma).', 1;

PRINT 'Base del portal al nivel de 2026-09-24: se puede seguir.';
GO

/* ######################################################################################
   PARTE 1 DE 9 — 2026-10-02_poner_al_dia.sql
   Tokens de MCP y adjuntos de reunión (y ámbito de módulos, si faltara)
   ###################################################################################### */
PRINT '';
PRINT '################ PARTE 1 DE 9: 2026-10-02_poner_al_dia.sql ################';
GO

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
GO

/* ######################################################################################
   PARTE 2 DE 9 — 2026-10-12_poner_al_dia.sql
   Colores de rol al Manual de Marca (solo valores)
   ###################################################################################### */
PRINT '';
PRINT '################ PARTE 2 DE 9: 2026-10-12_poner_al_dia.sql ################';
GO

/* ╔══════════════════════════════════════════════════════════════════╗
   ║  2026-10-12 — Colores de rol al Manual de Marca DIGER            ║
   ║                                                                  ║
   ║  QUÉ TRAE: actualiza los seis colores sembrados de la tabla      ║
   ║  Roles a la paleta del Manual de Marca. Son el fondo de los      ║
   ║  badges de rol en /Accesos/Roles y /Usuarios, y los seis         ║
   ║  estaban fuera de la paleta.                                     ║
   ║                                                                  ║
   ║  NO es una migración de EF: no cambia el esquema, solo valores.  ║
   ║  No se escribe fila en __EFMigrationsHistory a propósito —       ║
   ║  registrar un MigrationId que el código no reconoce solo         ║
   ║  confunde a quien luego compare historial contra migraciones.    ║
   ║                                                                  ║
   ║  VA DESPUÉS DE: database/2026-10-02_poner_al_dia.sql             ║
   ║                                                                  ║
   ║  CÓMO CORRERLO:                                                  ║
   ║  sqlcmd -S <servidor> -d <base> -b -I -f 65001                   ║
   ║         -i 2026-10-12_poner_al_dia.sql                           ║
   ╚══════════════════════════════════════════════════════════════════╝ */

SET NOCOUNT ON;

IF OBJECT_ID('dbo.Roles', 'U') IS NULL
BEGIN
    THROW 50000, 'No existe la tabla Roles. Corra primero database/00_diagnostico.sql.', 1;
END

/* Cada UPDATE exige el color sembrado en el WHERE. Si alguien ya cambió el
   color a mano desde el editor de roles, su elección se respeta y la fila no
   se toca. Eso es además lo que hace idempotente al script: la segunda
   corrida no encuentra ninguna fila que cambiar.

   Los seis colores nuevos pasan WCAG AA contra el texto blanco del badge
   (medido: 12.00, 8.42, 6.11, 5.02, 6.20 y 5.02) y son distintos entre sí:
   la separación más corta es 12.3, entre JefeUnidad y Empleado. Que se
   distingan importa tanto como que cumplan el manual — el badge existe para
   diferenciar roles de un vistazo. */

DECLARE @cambios TABLE (Rol NVARCHAR(100), Nuevo NVARCHAR(20));

UPDATE dbo.Roles SET Color = '#02395e'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'Administrador'   AND Color = '#3c3489';

UPDATE dbo.Roles SET Color = '#0c5278'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'JefeInstitucion' AND Color = '#0c447c';

UPDATE dbo.Roles SET Color = '#1a65a3'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'JefeArea'        AND Color = '#1a5fa0';

UPDATE dbo.Roles SET Color = '#15803d'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'JefeUnidad'      AND Color = '#1b5e20';

UPDATE dbo.Roles SET Color = '#0f6e56'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'Empleado'        AND Color = '#085041';

UPDATE dbo.Roles SET Color = '#b45309'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'Consultor'       AND Color = '#6d4c00';

/* El conteo va a una variable antes del PRINT: SQL Server no admite una
   subconsulta dentro de CONCAT en ese contexto y aborta el lote entero al
   compilarlo, sin ejecutar ni un UPDATE. */
DECLARE @n INT = (SELECT COUNT(*) FROM @cambios);

IF @n > 0
    PRINT CONCAT('actualizados: ', @n, ' rol(es)');
ELSE
    PRINT 'ya estaban al dia (o fueron cambiados a mano): no se toco ninguna fila';

/* ── Verificación: todo control debe leer 'presente' ──────────────────────
   Un 'FALTA' aquí significa que esa fila conserva un color fuera de la
   paleta. Puede ser legítimo —alguien lo cambió a mano— pero hay que
   mirarlo, no darlo por bueno. */
SELECT
    Id,
    Color,
    CASE WHEN Color IN ('#02395e','#0c5278','#1a65a3','#15803d','#0f6e56','#b45309')
         THEN 'presente' ELSE 'FALTA' END AS Control
FROM dbo.Roles
WHERE Id IN ('Administrador','JefeInstitucion','JefeArea',
             'JefeUnidad','Empleado','Consultor')
ORDER BY Id;
GO

/* ######################################################################################
   PARTE 3 DE 9 — 2026-10-14_poner_al_dia.sql
   Institución dueña de cada reunión, y su relleno
   ###################################################################################### */
PRINT '';
PRINT '################ PARTE 3 DE 9: 2026-10-14_poner_al_dia.sql ################';
GO

/* =====================================================================================
   Poner al día la base con los cambios al 2026-10-14
   -------------------------------------------------------------------------------------
   Va DESPUÉS de 2026-10-12_poner_al_dia.sql. Corra antes 00_diagnostico.sql: él dice si
   esta base está en condiciones de recibir éste.

   QUÉ TRAE, Y POR QUÉ

   Reuniones.InstitucionDuenaId — la migración 20261014172324_AgregarInstitucionDuenaAReunion.
   Es la institución que CONVOCÓ la reunión. No confundir con Reuniones.InstitucionId, que
   es la CONTRAPARTE invitada y vale null cuando no se eligió ninguna.

   El filtro de alcance de Reunion necesitaba un ancla institucional y solo tenía la
   contraparte, porque AreaId y UnidadId llegan nulas en 80 de las 81 reuniones de la base.
   Eso repartía mal en las dos direcciones:

     · Hacia adentro: un usuario de DIGER veía únicamente las 14 reuniones donde DIGER
       quedó anotada como invitada. De ahí salían los 3 acuerdos vencidos y las 27 personas
       capacitadas —de 21 instituciones ajenas— que aparecían en su Inicio sin ser suyos.

     · Hacia afuera, que es lo grave: los 3 usuarios de CONSUCOOP veían 17 reuniones
       internas de DIGER, con sus acuerdos y la lista completa de asistentes, solo por
       figurar como contraparte. Eso es una fuga de aislamiento, no una molestia de tablero.

   AGREGAR Y RELLENAR SON UN SOLO ACTO. La columna nueva es el ancla del respaldo del
   filtro: una base con la columna agregada y vacía deja a toda reunión sin área ni unidad
   invisible para todos salvo su creador y un administrador global. Por eso el paso 3 no es
   opcional y por eso va dentro de la misma transacción.

   La regla de relleno es: la institución de quien creó la reunión; y si no hay creador —las
   78 importadas del portal legado no lo tienen— la casa, que se deduce de dónde está
   asignada la mayoría de los usuarios en vez de escribir una sigla aquí dentro.

   Y UNA SEGUNDA COSA, QUE NO ES MIGRACIÓN (paso 5): atribuir dueño a las reuniones que no lo
   tenían. La regla es la que ya aplica el portal al crear —la reunión es de la unidad de quien
   la convocó— y las 78 migradas del portal legado, que no traen creador, van a Digitalización
   de Trámites (unidad DITRA, área GOBDIG). Sin eso, 80 de 81 reuniones quedaban sin dueño y el
   respaldo del filtro se las repartía a toda la institución: un Jefe de Unidad de Desarrollo de
   Proyectos veía las 78 de otra unidad. No cambia esquema, así que no va al historial.

   Como el resto de los scripts de esta carpeta, no pregunta por __EFMigrationsHistory para
   decidir qué hacer: cada paso comprueba SU PROPIA condición contra el catálogo de la base.
   La fila del historial se escribe al final, así que si la corrida muriera a la mitad, esa
   fila no sirve para saber qué quedó hecho.

   Se puede correr las veces que haga falta. La segunda no hace nada: el paso 3 solo toca
   filas en null.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-14_poner_al_dia.sql
   o pegarlo en SSMS y ejecutar.

     -b  detiene en el primer error. IMPORTANTE: sin esa bandera sqlcmd sigue de largo,
         llega al COMMIT y confirma lo que alcanzó a hacer.
     -I  QUOTED_IDENTIFIER ON.

   XACT_ABORT ON hace lo mismo desde adentro: cualquier error deshace TODO. O entra
   completo o no entra nada.
   ===================================================================================== */

SET QUOTED_IDENTIFIER ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

PRINT '── 0. Comprobación previa ──────────────────────────────────────────';

/* AsignacionesUsuario no es decorativa acá: es de donde sale el relleno del paso 3. Sin
   ella el script agregaría la columna y la dejaría vacía, que es justo el estado que no
   debe quedar. THROW y no RAISERROR: RAISERROR deja el mensaje pero NO corta el lote. */
IF OBJECT_ID(N'[Reuniones]', 'U') IS NULL
    OR OBJECT_ID(N'[AsignacionesUsuario]', 'U') IS NULL
    THROW 50000, N'Faltan tablas base (Reuniones o AsignacionesUsuario). Corra 00_diagnostico.sql y revise antes de seguir.', 1;

PRINT '   tablas base presentes';

PRINT '── 1. Reuniones.InstitucionDuenaId ─────────────────────────────────';

/* nvarchar(120) como Instituciones.Id. Admite nulo: una reunión puede existir antes de que
   se sepa la casa, y el paso 3 la rellena. */
IF COL_LENGTH('Reuniones', 'InstitucionDuenaId') IS NULL
BEGIN
    ALTER TABLE [Reuniones] ADD [InstitucionDuenaId] nvarchar(120) NULL;
    PRINT '   columna InstitucionDuenaId agregada';
END
ELSE
    PRINT '   columna InstitucionDuenaId ya estaba';

PRINT '── 2. Índice IX_Reuniones_InstitucionDuenaId_AreaId_UnidadId ───────';

/* Las tres juntas porque el respaldo del filtro pregunta por las tres a la vez.
   Dentro de EXEC a propósito: el lote se compila entero antes de ejecutarse, así que la
   columna agregada arriba todavía no existe para el compilador. Sin EXEC, la primera
   corrida sobre una base que no la tenía moriría con «Invalid column name». */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Reuniones_InstitucionDuenaId_AreaId_UnidadId'
                 AND object_id = OBJECT_ID(N'[Reuniones]'))
BEGIN
    EXEC(N'CREATE INDEX [IX_Reuniones_InstitucionDuenaId_AreaId_UnidadId]
             ON [Reuniones] ([InstitucionDuenaId], [AreaId], [UnidadId]);');
    PRINT '   índice creado';
END
ELSE
    PRINT '   índice ya estaba';

PRINT '── 3. Relleno de la institución dueña ──────────────────────────────';

/* También dentro de EXEC, y por el mismo motivo que el índice.
   Solo filas en null: ni pisa lo que ya esté puesto ni hace nada en la segunda corrida. */
EXEC(N'
DECLARE @casa nvarchar(120) = (
    SELECT TOP 1 InstitucionId FROM AsignacionesUsuario
    GROUP BY InstitucionId ORDER BY COUNT(DISTINCT UsuarioId) DESC);

IF @casa IS NULL
    PRINT ''   *** no hay asignaciones de usuario: no se pudo deducir la casa'';

UPDATE r SET r.InstitucionDuenaId = COALESCE(
        (SELECT TOP 1 a.InstitucionId FROM AsignacionesUsuario a WHERE a.UsuarioId = r.CreadoPorId),
        @casa)
FROM Reuniones r
WHERE r.InstitucionDuenaId IS NULL;

PRINT ''   reuniones rellenadas: '' + CAST(@@ROWCOUNT AS varchar(10));
');

PRINT '── 4. Historial de migraciones ─────────────────────────────────────';

/* Esta sí corresponde a una migración que EF reconoce, así que se registra para que
   «dotnet ef database update» no intente repetirla. */
IF OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NOT NULL
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT N'20261014172324_AgregarInstitucionDuenaAReunion', N'9.0.0'
    WHERE  NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory
                       WHERE MigrationId = N'20261014172324_AgregarInstitucionDuenaAReunion');
    PRINT '   historial al día';
END
ELSE
    PRINT '   *** no existe __EFMigrationsHistory: no se registró nada';

PRINT '── 5. Unidad dueña de las reuniones ───────────────────────────────';

/* Esto NO es una migración de EF: no cambia el esquema, solo atribuye dueño a filas que no lo
   tenían. Por eso no se registra en el historial.

   La regla es la que ya aplica el portal al crear: la reunión es de la unidad de quien la
   convocó. Las 78 migradas del portal legado no traen creador, y por decisión del área se
   atribuyen a Digitalización de Trámites —unidad DITRA, del área GOBDIG—, que es quien hizo
   ese trabajo.

   Sin esto, 80 de las 81 reuniones quedaban sin dueño y el respaldo del filtro se las repartía
   a toda la institución: un Jefe de Unidad de Desarrollo de Proyectos veía las 78 de otra
   unidad. Dentro de EXEC por lo mismo que los pasos anteriores. */
IF NOT EXISTS (SELECT 1 FROM Unidades WHERE Id = N'DITRA')
    PRINT '   *** no existe la unidad DITRA: las migradas quedan sin dueño';
ELSE
EXEC(N'
UPDATE r
SET r.AreaId   = COALESCE(r.AreaId,   a.AreaId),
    r.UnidadId = COALESCE(r.UnidadId, a.UnidadId)
FROM Reuniones r
CROSS APPLY (SELECT TOP 1 x.AreaId, x.UnidadId FROM AsignacionesUsuario x
             WHERE x.UsuarioId = r.CreadoPorId ORDER BY x.Id) a
WHERE r.CreadoPorId IS NOT NULL AND (r.AreaId IS NULL OR r.UnidadId IS NULL)
  AND (a.AreaId IS NOT NULL OR a.UnidadId IS NOT NULL);

PRINT ''   atribuidas a la unidad de su creador: '' + CAST(@@ROWCOUNT AS varchar(10));

UPDATE Reuniones
SET AreaId = ''GOBDIG'', UnidadId = ''DITRA''
WHERE OrigenExternoId IS NOT NULL AND AreaId IS NULL AND UnidadId IS NULL;

PRINT ''   migradas atribuidas a Digitalización de Trámites: '' + CAST(@@ROWCOUNT AS varchar(10));
');
COMMIT;
GO

PRINT '';
PRINT '════════ Comprobación ════════';

SELECT  N'Reuniones.InstitucionDuenaId' AS Control,
        CASE WHEN COL_LENGTH('Reuniones', 'InstitucionDuenaId') IS NULL
             THEN 'FALTA' ELSE 'presente' END AS Valor

UNION ALL
SELECT  N'IX_Reuniones_InstitucionDuenaId_AreaId_UnidadId',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                          WHERE name = N'IX_Reuniones_InstitucionDuenaId_AreaId_UnidadId'
                            AND object_id = OBJECT_ID(N'[Reuniones]'))
             THEN 'presente' ELSE 'FALTA' END

UNION ALL
/* La que de verdad importa: una reunión sin dueña no la ve nadie. */
SELECT  N'Ninguna reunión sin institución dueña',
        CASE WHEN EXISTS (SELECT 1 FROM Reuniones WHERE InstitucionDuenaId IS NULL AND IsDeleted = 0)
             THEN 'FALTA' ELSE 'presente' END

UNION ALL
/* Las migradas tienen que haber quedado con unidad; si no, siguen repartiéndose solas. */
SELECT  N'Ninguna reunión migrada sin unidad dueña',
        CASE WHEN EXISTS (SELECT 1 FROM Reuniones
                          WHERE OrigenExternoId IS NOT NULL AND IsDeleted = 0 AND UnidadId IS NULL)
             THEN 'FALTA' ELSE 'presente' END

UNION ALL
SELECT  N'Migración registrada en el historial',
        CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory
                          WHERE MigrationId = N'20261014172324_AgregarInstitucionDuenaAReunion')
             THEN 'presente' ELSE 'FALTA' END;

PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
GO

/* ######################################################################################
   PARTE 4 DE 9 — 2026-10-19_poner_al_dia.sql
   Plantillas de proyecto
   ###################################################################################### */
PRINT '';
PRINT '################ PARTE 4 DE 9: 2026-10-19_poner_al_dia.sql ################';
GO

/* =====================================================================================
   Poner al día la base con los cambios al 2026-10-19
   -------------------------------------------------------------------------------------
   Va DESPUÉS de 2026-10-14_poner_al_dia.sql. Corra antes 00_diagnostico.sql: él dice si
   esta base está en condiciones de recibir éste.

   QUÉ TRAE, Y POR QUÉ

   Tres tablas nuevas — la migración 20261019171050_AgregarPlantillasDeProyecto:

     PlantillasProyecto     la plantilla: nombre, descripción, si está activa y hasta
                            dónde se ve (institución / área / unidad).
     PlantillaEntregables   sus entregables, con orden.
     PlantillaActividades   las actividades de cada entregable, con orden.

   Son para los proyectos CÍCLICOS: los que se repiten cada año o cada convocatoria con
   los mismos entregables y las mismas actividades, y que hoy obligan a recapturar el
   árbol entero desde cero. La plantilla guarda SOLO el esqueleto —qué entregables, qué
   actividades y en qué orden—. No guarda fechas, ni estados, ni avances, ni
   responsables: eso es del proyecto concreto y cambia en cada edición, así que una
   plantilla que los guardara los traería caducados a la siguiente.

   ESTE SCRIPT ES ADITIVO. No toca ninguna tabla existente: ni agrega columnas, ni
   cambia tipos, ni mueve datos. Si algo sale mal, lo peor que puede pasar es que las
   tres tablas queden a medias, y volver a correrlo las completa.

   La visibilidad reusa el vocabulario de NivelAlcance que el portal ya tiene:
     2 = Institución   la ve toda la institución
     3 = Área          solo quien esté en esa área
     4 = Unidad        solo quien esté en esa unidad
   El 1 (Global) NO se usa acá: una plantilla siempre nace en una institución.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-19_poner_al_dia.sql
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

/* QUOTED_IDENTIFIER tiene que ir ON y no es opcional: PlantillasProyecto lleva un
   índice FILTRADO —el de unicidad, que solo mira las filas vivas— y SQL Server rechaza
   cualquier INSERT, UPDATE o DELETE sobre una tabla con índice filtrado si la sesión lo
   tiene apagado. Comprobado: sin esta línea, un INSERT de prueba muere con el error
   1934. EF siempre lo manda ON, así que la aplicación nunca lo sufre; quien sí lo
   sufriría es el operador corriendo SQL a mano, que es exactamente para quien se
   escribe este archivo. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ── Guarda: ¿es ésta la base correcta? ──────────────────────────────────────────── */
IF OBJECT_ID(N'[Proyectos]', N'U') IS NULL
   OR OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    THROW 50000,
        'Esta base no tiene Proyectos o __EFMigrationsHistory. Corra 00_diagnostico.sql: no es la base del portal o está incompleta.',
        1;
END
GO

PRINT '════════ Plantillas de estructura de proyecto ════════';
GO

/* ── 1. PlantillasProyecto ───────────────────────────────────────────────────────────
   Idempotente contra el CATÁLOGO y no contra __EFMigrationsHistory: la fila del
   historial se escribe al final, así que una corrida que muera a la mitad la deja sin
   escribir y el historial mentiría sobre lo que sí llegó a crearse. */
IF OBJECT_ID(N'[PlantillasProyecto]', N'U') IS NULL
BEGIN
    CREATE TABLE [PlantillasProyecto] (
        [Id]            int            IDENTITY(1,1) NOT NULL,
        [Nombre]        nvarchar(300)  NOT NULL,
        [Descripcion]   nvarchar(1000) NULL,
        [Activa]        bit            NOT NULL CONSTRAINT [DF_PlantillasProyecto_Activa] DEFAULT (CONVERT(bit, 1)),
        [Nivel]         int            NOT NULL,
        [InstitucionId] nvarchar(60)   NOT NULL,
        [AreaId]        nvarchar(60)   NULL,
        [UnidadId]      nvarchar(60)   NULL,
        [IsDeleted]     bit            NOT NULL CONSTRAINT [DF_PlantillasProyecto_IsDeleted] DEFAULT (CONVERT(bit, 0)),
        [CreatedAt]     datetime2      NOT NULL,
        [CreatedBy]     nvarchar(max)  NULL,
        [UpdatedAt]     datetime2      NULL,
        [UpdatedBy]     nvarchar(max)  NULL,
        CONSTRAINT [PK_PlantillasProyecto] PRIMARY KEY ([Id])
    );
    PRINT '  PlantillasProyecto .............. creada';
END
ELSE PRINT '  PlantillasProyecto .............. ya estaba';
GO

/* ── 2. PlantillaEntregables ─────────────────────────────────────────────────────── */
IF OBJECT_ID(N'[PlantillaEntregables]', N'U') IS NULL
BEGIN
    CREATE TABLE [PlantillaEntregables] (
        [Id]                  int            IDENTITY(1,1) NOT NULL,
        [PlantillaProyectoId] int            NOT NULL,
        [Orden]               int            NOT NULL,
        [Nombre]              nvarchar(300)  NOT NULL,
        [Descripcion]         nvarchar(1000) NULL,
        CONSTRAINT [PK_PlantillaEntregables] PRIMARY KEY ([Id]),
        /* Cascade: un entregable de plantilla no existe fuera de su plantilla. */
        CONSTRAINT [FK_PlantillaEntregables_PlantillasProyecto_PlantillaProyectoId]
            FOREIGN KEY ([PlantillaProyectoId]) REFERENCES [PlantillasProyecto] ([Id]) ON DELETE CASCADE
    );
    PRINT '  PlantillaEntregables ............ creada';
END
ELSE PRINT '  PlantillaEntregables ............ ya estaba';
GO

/* ── 3. PlantillaActividades ─────────────────────────────────────────────────────── */
IF OBJECT_ID(N'[PlantillaActividades]', N'U') IS NULL
BEGIN
    CREATE TABLE [PlantillaActividades] (
        [Id]                    int            IDENTITY(1,1) NOT NULL,
        [PlantillaEntregableId] int            NOT NULL,
        [Orden]                 int            NOT NULL,
        [Nombre]                nvarchar(300)  NOT NULL,
        [Descripcion]           nvarchar(1000) NULL,
        CONSTRAINT [PK_PlantillaActividades] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlantillaActividades_PlantillaEntregables_PlantillaEntregableId]
            FOREIGN KEY ([PlantillaEntregableId]) REFERENCES [PlantillaEntregables] ([Id]) ON DELETE CASCADE
    );
    PRINT '  PlantillaActividades ............ creada';
END
ELSE PRINT '  PlantillaActividades ............ ya estaba';
GO

/* ── 4. Índices ──────────────────────────────────────────────────────────────────────
   El único con filtro es el de unicidad: el nombre es único POR INSTITUCIÓN y solo
   entre las vivas. Dos instituciones pueden tener cada una su "Plan Operativo Anual"
   sin pisarse, y una plantilla borrada no puede impedir que se cree otra con su
   nombre. */
IF OBJECT_ID(N'[PlantillasProyecto]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_PlantillasProyecto_InstitucionId_Nombre'
                     AND object_id = OBJECT_ID(N'[PlantillasProyecto]'))
BEGIN
    CREATE UNIQUE INDEX [IX_PlantillasProyecto_InstitucionId_Nombre]
        ON [PlantillasProyecto] ([InstitucionId], [Nombre])
        WHERE [IsDeleted] = 0;
    PRINT '  IX ..._InstitucionId_Nombre ..... creado';
END
ELSE PRINT '  IX ..._InstitucionId_Nombre ..... ya estaba';
GO

IF OBJECT_ID(N'[PlantillasProyecto]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_PlantillasProyecto_InstitucionId_Activa'
                     AND object_id = OBJECT_ID(N'[PlantillasProyecto]'))
BEGIN
    CREATE INDEX [IX_PlantillasProyecto_InstitucionId_Activa]
        ON [PlantillasProyecto] ([InstitucionId], [Activa]);
    PRINT '  IX ..._InstitucionId_Activa ..... creado';
END
ELSE PRINT '  IX ..._InstitucionId_Activa ..... ya estaba';
GO

IF OBJECT_ID(N'[PlantillaEntregables]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_PlantillaEntregables_PlantillaProyectoId'
                     AND object_id = OBJECT_ID(N'[PlantillaEntregables]'))
BEGIN
    CREATE INDEX [IX_PlantillaEntregables_PlantillaProyectoId]
        ON [PlantillaEntregables] ([PlantillaProyectoId]);
    PRINT '  IX PlantillaEntregables ......... creado';
END
ELSE PRINT '  IX PlantillaEntregables ......... ya estaba';
GO

IF OBJECT_ID(N'[PlantillaActividades]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_PlantillaActividades_PlantillaEntregableId'
                     AND object_id = OBJECT_ID(N'[PlantillaActividades]'))
BEGIN
    CREATE INDEX [IX_PlantillaActividades_PlantillaEntregableId]
        ON [PlantillaActividades] ([PlantillaEntregableId]);
    PRINT '  IX PlantillaActividades ......... creado';
END
ELSE PRINT '  IX PlantillaActividades ......... ya estaba';
GO

/* ── 5. El historial ─────────────────────────────────────────────────────────────────
   Al final y solo si las tres tablas están: la fila dice "esta migración ya corrió",
   y escribirla antes de que lo esté haría que `dotnet ef database update` se la saltara
   dejando la base a medias sin que nadie se entere. */
IF OBJECT_ID(N'[PlantillasProyecto]',   N'U') IS NOT NULL
   AND OBJECT_ID(N'[PlantillaEntregables]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[PlantillaActividades]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory
                   WHERE MigrationId = N'20261019171050_AgregarPlantillasDeProyecto')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20261019171050_AgregarPlantillasDeProyecto', N'9.0.0');
    PRINT '  historial ....................... registrado';
END
ELSE PRINT '  historial ....................... ya estaba';
GO

PRINT '';
PRINT '════════ Comprobación ════════';

SELECT  N'Tabla PlantillasProyecto' AS Control,
        CASE WHEN OBJECT_ID(N'[PlantillasProyecto]', N'U') IS NULL
             THEN 'FALTA' ELSE 'presente' END AS Valor

UNION ALL
SELECT  N'Tabla PlantillaEntregables',
        CASE WHEN OBJECT_ID(N'[PlantillaEntregables]', N'U') IS NULL
             THEN 'FALTA' ELSE 'presente' END

UNION ALL
SELECT  N'Tabla PlantillaActividades',
        CASE WHEN OBJECT_ID(N'[PlantillaActividades]', N'U') IS NULL
             THEN 'FALTA' ELSE 'presente' END

UNION ALL
/* La unicidad por institución: sin ella, dos plantillas con el mismo nombre en la
   misma casa y nadie sabría cuál trae qué. */
SELECT  N'IX_PlantillasProyecto_InstitucionId_Nombre',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                          WHERE name = N'IX_PlantillasProyecto_InstitucionId_Nombre'
                            AND object_id = OBJECT_ID(N'[PlantillasProyecto]'))
             THEN 'presente' ELSE 'FALTA' END

UNION ALL
SELECT  N'IX_PlantillaEntregables_PlantillaProyectoId',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                          WHERE name = N'IX_PlantillaEntregables_PlantillaProyectoId'
                            AND object_id = OBJECT_ID(N'[PlantillaEntregables]'))
             THEN 'presente' ELSE 'FALTA' END

UNION ALL
SELECT  N'IX_PlantillaActividades_PlantillaEntregableId',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                          WHERE name = N'IX_PlantillaActividades_PlantillaEntregableId'
                            AND object_id = OBJECT_ID(N'[PlantillaActividades]'))
             THEN 'presente' ELSE 'FALTA' END

UNION ALL
/* Las dos claves foráneas en cascada: si faltaran, borrar una plantilla dejaría sus
   entregables huérfanos en la tabla. */
SELECT  N'FK PlantillaEntregables → PlantillasProyecto',
        CASE WHEN OBJECT_ID(N'[FK_PlantillaEntregables_PlantillasProyecto_PlantillaProyectoId]', N'F') IS NULL
             THEN 'FALTA' ELSE 'presente' END

UNION ALL
SELECT  N'FK PlantillaActividades → PlantillaEntregables',
        CASE WHEN OBJECT_ID(N'[FK_PlantillaActividades_PlantillaEntregables_PlantillaEntregableId]', N'F') IS NULL
             THEN 'FALTA' ELSE 'presente' END

UNION ALL
SELECT  N'Migración registrada en el historial',
        CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory
                          WHERE MigrationId = N'20261019171050_AgregarPlantillasDeProyecto')
             THEN 'presente' ELSE 'FALTA' END;

PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
GO

/* ######################################################################################
   PARTE 5 DE 9 — 2026-10-19_cambio_password_obligatorio.sql
   Usuarios.PasswordCambiadoEl
   ###################################################################################### */
PRINT '';
PRINT '################ PARTE 5 DE 9: 2026-10-19_cambio_password_obligatorio.sql ################';
GO

/* ╔══════════════════════════════════════════════════════════════════╗
   ║  2026-10-19 — Cambio de contraseña obligatorio por única vez     ║
   ║                                                                  ║
   ║  QUÉ TRAE: la columna Usuarios.PasswordCambiadoEl (datetime2     ║
   ║  NULL), que es lo único que el portal necesita para exigir el    ║
   ║  cambio de contraseña una sola vez a partir del 01/11/2026.      ║
   ║                                                                  ║
   ║  Nula significa «la contraseña vigente no la eligió el dueño     ║
   ║  de la cuenta». Toda fila existente nace nula, y eso es          ║
   ║  justamente la tanda única: en su primer ingreso a partir del    ║
   ║  corte, cada usuario pasa una vez por la pantalla de cambio y    ║
   ║  queda estampado. La misma columna sirve después para las        ║
   ║  cuentas nuevas con contraseña temporal, sin agregar nada.       ║
   ║                                                                  ║
   ║  NO HACE FALTA BORRAR NI TOCAR DATOS: la columna se agrega       ║
   ║  vacía a propósito. No se rellena con fechas inventadas —        ║
   ║  cualquier valor puesto acá eximiría del cambio a ese usuario.   ║
   ║                                                                  ║
   ║  SÍ es una migración de EF:                                      ║
   ║  20261019174802_AddPasswordCambiadoEl. La fila del historial     ║
   ║  se escribe al final del script.                                 ║
   ║                                                                  ║
   ║  OJO, NO ES PARTE DE ESTE SCRIPT: la fecha de corte vive en      ║
   ║  appsettings.json, en Auth:CambioPasswordObligatorioDesde. Sin   ║
   ║  esa clave el portal no retiene a nadie por más que la columna   ║
   ║  exista — está hecho para fallar abierto.                        ║
   ║                                                                  ║
   ║  VA DESPUÉS DE: database/2026-10-19_poner_al_dia.sql             ║
   ║                                                                  ║
   ║  CÓMO CORRERLO:                                                  ║
   ║  sqlcmd -S <servidor> -d <base> -b -I -f 65001                   ║
   ║         -i 2026-10-19_cambio_password_obligatorio.sql            ║
   ╚══════════════════════════════════════════════════════════════════╝ */

SET NOCOUNT ON;

IF OBJECT_ID('dbo.Usuarios', 'U') IS NULL
BEGIN
    THROW 50000, 'No existe la tabla Usuarios. Corra primero database/00_diagnostico.sql.', 1;
END

PRINT '── 1. Usuarios.PasswordCambiadoEl ──────────────────────────────────';

/* La condición se comprueba contra el catálogo de la base, no contra
   __EFMigrationsHistory: la fila del historial se escribe al final, así que si
   esta corrida muriera a la mitad no serviría para saber qué quedó hecho. */
IF COL_LENGTH('dbo.Usuarios', 'PasswordCambiadoEl') IS NULL
BEGIN
    ALTER TABLE dbo.Usuarios ADD PasswordCambiadoEl datetime2 NULL;
    PRINT '   agregada';
END
ELSE
    PRINT '   ya estaba';
GO

PRINT '── 2. Historial de migraciones ─────────────────────────────────────';

/* El paso de arriba sí corresponde a una migración que EF reconoce, así que se
   registra. */
IF OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NOT NULL
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT v.Id, N'9.0.0'
    FROM   (VALUES (N'20261019174802_AddPasswordCambiadoEl')) v(Id)
    WHERE  NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory h WHERE h.MigrationId = v.Id);
    PRINT '   historial al dia';
END
ELSE
    PRINT '   *** no existe __EFMigrationsHistory: no se registro nada';
GO

PRINT '';
PRINT '════════ Comprobación ════════';

/* ── Verificación: todo control debe leer 'presente' ──────────────────────
   Un 'FALTA' significa que el paso no se aplicó y el portal fallará al leer
   la columna en el login. */
SELECT
    'Usuarios.PasswordCambiadoEl' AS Objeto,
    CASE WHEN COL_LENGTH('dbo.Usuarios', 'PasswordCambiadoEl') IS NOT NULL
         THEN 'presente' ELSE 'FALTA' END AS Control
UNION ALL
SELECT
    'tipo datetime2, acepta nulos',
    CASE WHEN EXISTS (
            SELECT 1
            FROM sys.columns c
            JOIN sys.types  t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID('dbo.Usuarios')
              AND c.name      = 'PasswordCambiadoEl'
              AND t.name      = 'datetime2'
              AND c.is_nullable = 1)
         THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT
    'historial 20261019174802_AddPasswordCambiadoEl',
    CASE WHEN EXISTS (
            SELECT 1 FROM __EFMigrationsHistory
            WHERE MigrationId = N'20261019174802_AddPasswordCambiadoEl')
         THEN 'presente' ELSE 'FALTA' END;

/* Cuántos usuarios quedan pendientes del cambio. No es un control que pueda
   fallar: es el número de personas que el 01/11 pasarán una vez por la
   pantalla. Si la columna acaba de agregarse, son todos. */
SELECT
    COUNT(*)                                                  AS UsuariosActivos,
    SUM(CASE WHEN PasswordCambiadoEl IS NULL THEN 1 ELSE 0 END) AS PendientesDeCambio
FROM dbo.Usuarios
WHERE Activo = 1 AND IsDeleted = 0;
GO

/* ######################################################################################
   PARTE 6 DE 9 — 2026-10-22_poner_al_dia.sql
   Las diez migraciones SIGER de agosto (scripts 11 a 15, en línea)
   ###################################################################################### */
PRINT '';
PRINT '################ PARTE 6 DE 9: 2026-10-22_poner_al_dia.sql ################';
GO

/* =====================================================================================
   Poner al día la base con los cambios al 2026-10-22 — lo que trajo la rama Jamil
   -------------------------------------------------------------------------------------
   Va DESPUÉS de 2026-10-19_poner_al_dia.sql. Corra antes 00_diagnostico.sql: él dice si
   esta base está en condiciones de recibir éste.

   EL FALLO QUE ARREGLA

   La fusión de Jamil en ike_opt (commit b1385e7, 2026-10-20) trajo diez migraciones de
   EF fechadas entre el 21 y el 27 de agosto. Sus fechas son ANTERIORES a migraciones que
   las bases ya tienen, así que ningún «poner al día» posterior las incluyó, y las bases
   que no venían de Jamil se quedaron sin ellas. El modelo sí las mapea: la primera
   consulta que lee Instituciones revienta con

       Invalid column name 'RutaSol'.

   y con ella caen «Nueva reunión», el editor de instituciones y cualquier pantalla que
   cargue el catálogo de instituciones.

   QUÉ TRAE — diez migraciones de EF, todas reales:

     20260821210115_SigerIdOpcional                 ┐
     20260824173317_ArchivoSigerOriginal            ├ scripts/sql/11-esquema-siger-fase1-a-fase3.sql
     20260824192857_ConciliacionConClaveEstable     ┘
     20260824222016_ColaLlenadoAsistido             ─ scripts/sql/12-cola-llenado-asistido.sql
     20260825160050_UrlSolCompuesta                 ─ scripts/sql/13-url-sol-compuesta.sql
                                                      (Instituciones.RutaSol: el error de arriba)
     20260825165843_FichaCompletaEnTramiteExpediente┐
     20260825170007_ConvertirModalidadesExistentes  ├ scripts/sql/14-ficha-completa-en-expediente.sql
     20260825170158_SembrarEntregablesYLugares      ┘
     20260825190000_ColumnaFichaCompleta            ─ scripts/sql/15-ficha-completa-columna.sql
     20260827161837_FusionRamaDev                   ─ vacía a propósito; solo se registra aquí

   Este archivo NO reescribe esas migraciones: las encadena con :r. Los scripts 11 a 15
   los escribió y los midió la rama Jamil contra Ensayo, y copiarlos aquí sería tener dos
   versiones del mismo cambio esperando a divergir.

   ESTE SÍ TOCA DATOS (los toca el 11 y el 14, no este archivo):
     · 11 rellena la clave estable de las conciliaciones y BORRA las que apunten a un
       trámite de expediente que ya no existe.
     · 14 copia cada Modalidad de texto libre a ModalidadDetalle, la reduce al catálogo
       Virtual / Presencial / Hibrido, y siembra entregables y lugares del expediente.
   Ninguna otra fila se borra. RESPALDE ANTES.

   POR QUÉ HAY UNA GUARDA DE COHERENCIA, Y NO SOLO EL «YA ESTABA»

   Los scripts 11 a 15 son los idempotentes que genera EF: deciden por la fila de
   __EFMigrationsHistory, no por el catálogo. En una base sin nada de Jamil eso es
   correcto. En una base donde alguien aplicó un pedazo a mano, sin escribir el
   historial, volverían a crear lo que ya existe y morirían con «objeto duplicado» a
   mitad de camino. Por eso el paso 1 mira el CATÁLOGO de cada migración contra su fila
   del historial y, si no cuadran, se niega a empezar y dice cuál. Esa base hay que
   conciliarla a mano: adivinar qué parte de una migración llegó no es trabajo de un
   script.

   CÓMO CORRERLO — desde la RAÍZ del repositorio, porque :r resuelve las rutas contra la
   carpeta desde la que se llama a sqlcmd, no contra la de este archivo:
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i database\2026-10-22_poner_al_dia.sql
   El -b no es opcional: es lo que hace que un fallo en el 11 no deje correr el 12.
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ── Guarda: ¿es ésta la base correcta? ──────────────────────────────────────────── */
IF OBJECT_ID(N'[Instituciones]', N'U') IS NULL
   OR OBJECT_ID(N'[TramitesSiger]', N'U') IS NULL
   OR OBJECT_ID(N'[ExpedienteTramites]', N'U') IS NULL
   OR OBJECT_ID(N'[ConciliacionesSiger]', N'U') IS NULL
   OR OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    THROW 50000,
        'Faltan Instituciones, TramitesSiger, ExpedienteTramites, ConciliacionesSiger o __EFMigrationsHistory. Corra 00_diagnostico.sql: no es la base del portal o está incompleta.',
        1;
END
GO

PRINT '════════ 1. Coherencia entre catálogo e historial ════════';
GO

/* Cada migración con su huella en el catálogo: el último objeto que deja, que solo
   existe si la migración llegó hasta el final. SembrarEntregablesYLugares no tiene
   huella de esquema —solo inserta filas— y se juzga junto con la 14 que la contiene. */
DECLARE @Huellas TABLE (MigrationId nvarchar(150), Presente bit);
INSERT INTO @Huellas VALUES
 (N'20260821210115_SigerIdOpcional',
    CASE WHEN COLUMNPROPERTY(OBJECT_ID(N'[TramitesSiger]'), N'IdSiger', 'AllowsNull') = 1 THEN 1 ELSE 0 END),
 (N'20260824173317_ArchivoSigerOriginal',
    CASE WHEN OBJECT_ID(N'[FotosTramiteSiger]', N'U') IS NOT NULL THEN 1 ELSE 0 END),
 (N'20260824192857_ConciliacionConClaveEstable',
    CASE WHEN COL_LENGTH(N'ExpedienteTramites', N'ClaveEstable') IS NOT NULL
          AND COL_LENGTH(N'ConciliacionesSiger', N'ExpedienteTramiteId') IS NULL THEN 1 ELSE 0 END),
 (N'20260824222016_ColaLlenadoAsistido',
    CASE WHEN OBJECT_ID(N'[PropuestasLlenado]', N'U') IS NOT NULL THEN 1 ELSE 0 END),
 (N'20260825160050_UrlSolCompuesta',
    CASE WHEN COL_LENGTH(N'Instituciones', N'RutaSol') IS NOT NULL
          AND COL_LENGTH(N'TramitesSiger', N'SolTramo') IS NOT NULL THEN 1 ELSE 0 END),
 (N'20260825165843_FichaCompletaEnTramiteExpediente',
    CASE WHEN OBJECT_ID(N'[ExpedienteTramiteLugares]', N'U') IS NOT NULL
          AND COL_LENGTH(N'ExpedienteTramites', N'ModalidadDetalle') IS NOT NULL THEN 1 ELSE 0 END),
 (N'20260825170007_ConvertirModalidadesExistentes',
    CASE WHEN OBJECT_ID(N'[CK_ExpedienteTramites_Modalidad]', N'C') IS NOT NULL THEN 1 ELSE 0 END),
 (N'20260825190000_ColumnaFichaCompleta',
    CASE WHEN COL_LENGTH(N'TramitesSiger', N'FichaCompleta') IS NOT NULL THEN 1 ELSE 0 END);

DECLARE @Descuadres nvarchar(max) =
    (SELECT STRING_AGG(
                h.MigrationId + CASE WHEN h.Presente = 1 THEN N' (está en la base y no en el historial)'
                                                         ELSE N' (está en el historial y no en la base)' END,
                N'; ')
       FROM @Huellas h
      WHERE h.Presente <> CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory m
                                            WHERE m.MigrationId = h.MigrationId) THEN 1 ELSE 0 END);

IF @Descuadres IS NOT NULL
BEGIN
    DECLARE @Msg nvarchar(2048) =
        N'Catálogo e historial no cuadran — concilie a mano antes de seguir: ' + @Descuadres;
    THROW 50001, @Msg, 1;
END

PRINT '  catálogo e historial .......... cuadran';
GO

PRINT '';
PRINT '════════ 2. Esquema SIGER, fases 1 a 3 (script 11) ════════';
GO
/* ---- inicio de scripts\sql\11-esquema-siger-fase1-a-fase3.sql (en línea; antes se encadenaba con :r) ---- */
-- ============================================================================
-- Esquema SIGER — fases 1 a 3 del plan de promoción (docs/promocion-siger)
--
-- Para el Producción real, que Aplicar-CambiosProduccion.ps1 NO migra: ese
-- script solo aplica los .sql de esta carpeta. Desplegar.ps1 sí corre
-- "dotnet ef database update", así que quien despliegue con él no necesita esto.
--
-- Cubre tres migraciones, en este orden:
--   20260821210115_SigerIdOpcional             IdSiger pasa a opcional, con
--                                              índice único FILTRADO por IS NOT NULL
--                                              (SQL Server solo admite un nulo sin filtro).
--   20260824173317_ArchivoSigerOriginal        Crea FotosTramiteSiger, el archivo del
--                                              inventario tal como llegó de SIGER.
--   20260824192857_ConciliacionConClaveEstable Da identidad estable al trámite de
--                                              expediente y reapunta las decisiones de
--                                              conciliación a ella.
--
-- ES IDEMPOTENTE: cada bloque comprueba __EFMigrationsHistory antes de actuar, así
-- que se puede ejecutar más de una vez sin daño. Va dentro de una transacción.
--
-- DOS COSAS QUE HAY QUE SABER ANTES DE CORRERLO:
--
--   1. Lleva un UPDATE de datos, no solo cambios de esquema. Rellena la clave de las
--      conciliaciones existentes cruzando contra ExpedienteTramites, y BORRA las que
--      apuntaran a un trámite que ya no existe. Conviene respaldar antes.
--
--   2. ClaveEstable se crea con DEFAULT NEWID() para que cada fila existente reciba
--      una clave distinta. Si alguien lo cambia por un valor fijo, todas quedarán
--      iguales y el índice único fallará.
--
-- Ejecutar con codificación 65001 (ver "Codificación: por qué estos scripts llevan
-- BOM" en scripts/DESPLIEGUE.md):
--   sqlcmd -S <servidor> -d <base> -f 65001 -i 11-esquema-siger-fase1-a-fase3.sql
-- ============================================================================

-- Obligatorias, y no son decoración: la migración SigerIdOpcional crea un índice
-- FILTRADO (WHERE [IdSiger] IS NOT NULL) y SQL Server rechaza crearlo si
-- QUOTED_IDENTIFIER está apagado. sqlcmd arranca con esa opción en OFF, así que sin
-- estas dos líneas el script falla con "Msg 1934" en la primera migración —y falla
-- solo contra una base sin migrar, es decir, justo en Producción.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821210115_SigerIdOpcional'
)
BEGIN
    DROP INDEX [IX_TramitesSiger_IdSiger] ON [TramitesSiger];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821210115_SigerIdOpcional'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TramitesSiger]') AND [c].[name] = N'IdSiger');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TramitesSiger] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TramitesSiger] ALTER COLUMN [IdSiger] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821210115_SigerIdOpcional'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TramitesSiger_IdSiger] ON [TramitesSiger] ([IdSiger]) WHERE [IdSiger] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821210115_SigerIdOpcional'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260821210115_SigerIdOpcional', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824173317_ArchivoSigerOriginal'
)
BEGIN
    CREATE TABLE [FotosTramiteSiger] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [Version] int NOT NULL,
        [Origen] nvarchar(40) NOT NULL,
        [Codigo] nvarchar(20) NOT NULL,
        [IdSiger] int NULL,
        [CapturadaEl] datetime2 NOT NULL,
        [Contenido] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_FotosTramiteSiger] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824173317_ArchivoSigerOriginal'
)
BEGIN
    CREATE INDEX [IX_FotosTramiteSiger_Codigo] ON [FotosTramiteSiger] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824173317_ArchivoSigerOriginal'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FotosTramiteSiger_TramiteSigerId_Version] ON [FotosTramiteSiger] ([TramiteSigerId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824173317_ArchivoSigerOriginal'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824173317_ArchivoSigerOriginal', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    ALTER TABLE [ConciliacionesSiger] DROP CONSTRAINT [FK_ConciliacionesSiger_ExpedienteTramites_ExpedienteTramiteId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    DROP INDEX [IX_ConciliacionesSiger_ExpedienteTramiteId] ON [ConciliacionesSiger];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [ClaveEstable] uniqueidentifier NOT NULL DEFAULT (NEWID());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    ALTER TABLE [ConciliacionesSiger] ADD [ClaveTramite] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    ALTER TABLE [ConciliacionesSiger] ADD [ExpedienteId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    -- Va dentro de EXEC porque SQL Server compila el lote completo ANTES de evaluar el
    -- IF de arriba, y ExpedienteTramiteId ya no existe en una base que ya aplicó esta
    -- migración. Sin esto, la segunda corrida del script fallaba con "Invalid column
    -- name" pese a estar guardada — es decir, el script decía ser idempotente y no lo era.
    EXEC(N'UPDATE c
              SET c.ClaveTramite = t.ClaveEstable,
                  c.ExpedienteId = t.ExpedienteId
             FROM ConciliacionesSiger c
            INNER JOIN ExpedienteTramites t ON t.Id = c.ExpedienteTramiteId;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    -- En EXEC por lo mismo que el UPDATE de arriba: ClaveTramite se agrega en este mismo
    -- lote, y SQL Server lo compila entero antes de ejecutar nada. Contra una base ya
    -- migrada pasa —la columna existe— y por eso este fallo solo habría salido en el
    -- Producción real, que es donde peor se descubre.
    EXEC(N'DELETE FROM ConciliacionesSiger WHERE ClaveTramite IS NULL OR ExpedienteId IS NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ConciliacionesSiger]') AND [c].[name] = N'ClaveTramite');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [ConciliacionesSiger] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [ConciliacionesSiger] ALTER COLUMN [ClaveTramite] uniqueidentifier NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ConciliacionesSiger]') AND [c].[name] = N'ExpedienteId');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [ConciliacionesSiger] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [ConciliacionesSiger] ALTER COLUMN [ExpedienteId] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ConciliacionesSiger]') AND [c].[name] = N'ExpedienteTramiteId');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [ConciliacionesSiger] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [ConciliacionesSiger] DROP COLUMN [ExpedienteTramiteId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExpedienteTramites_ClaveEstable] ON [ExpedienteTramites] ([ClaveEstable]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ConciliacionesSiger_ClaveTramite] ON [ConciliacionesSiger] ([ClaveTramite]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    CREATE INDEX [IX_ConciliacionesSiger_ExpedienteId] ON [ConciliacionesSiger] ([ExpedienteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    ALTER TABLE [ConciliacionesSiger] ADD CONSTRAINT [FK_ConciliacionesSiger_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824192857_ConciliacionConClaveEstable', N'9.0.0');
END;

COMMIT;
GO
GO
/* ---- fin de scripts\sql\11-esquema-siger-fase1-a-fase3.sql ---- */
GO

PRINT '';
PRINT '════════ 3. Cola de llenado asistido (script 12) ════════';
GO
/* ---- inicio de scripts\sql\12-cola-llenado-asistido.sql (en línea; antes se encadenaba con :r) ---- */
-- ============================================================================
-- Cola del llenado asistido — Fase 5 del plan de promoción (docs/promocion-siger)
--
-- Para el Producción real, que Aplicar-CambiosProduccion.ps1 NO migra: ese
-- script solo aplica los .sql de esta carpeta. Desplegar.ps1 sí corre
-- "dotnet ef database update", así que quien despliegue con él no necesita esto.
--
-- Cubre una migración:
--   20260824222016_ColaLlenadoAsistido   Crea PropuestasLlenado, la cola donde se
--                                        revisa lo que el sistema propone para los
--                                        campos vacíos de las fichas SIGER.
--
-- Requiere aplicado antes el 11-esquema-siger-fase1-a-fase3.sql.
--
-- NO TOCA DATOS EXISTENTES. Crea una tabla vacía, dos índices y su llave foránea;
-- ninguna fila de TramitesSiger ni de ninguna otra tabla se modifica. La cola nace
-- vacía y se llena desde la pantalla (SIGER › Llenado asistido), nunca desde acá.
--
-- Ejecutar con codificación 65001 (ver "Codificación: por qué estos scripts llevan
-- BOM" en scripts/DESPLIEGUE.md):
--   sqlcmd -S <servidor> -d <base> -f 65001 -i 12-cola-llenado-asistido.sql
-- ============================================================================

-- Obligatorias, y no son decoración: esta migración crea un índice único FILTRADO
-- (WHERE [Estado] = 0) y SQL Server rechaza crearlo si QUOTED_IDENTIFIER está
-- apagado. sqlcmd arranca con esa opción en OFF. Que EF haya envuelto el CREATE
-- INDEX en un EXEC() no salva: el SQL dinámico hereda la opción del lote que lo
-- llama, así que sin estas dos líneas el script falla con "Msg 1934" —y falla solo
-- contra una base sin migrar, es decir, justo en Producción.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824222016_ColaLlenadoAsistido'
)
BEGIN
    CREATE TABLE [PropuestasLlenado] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [Campo] int NOT NULL,
        [ValorPropuesto] nvarchar(300) NULL,
        [Certeza] int NOT NULL,
        [Justificacion] nvarchar(400) NOT NULL,
        [Estado] int NOT NULL,
        [DecididaEl] datetime2 NULL,
        [DecididaPor] nvarchar(120) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PropuestasLlenado] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PropuestasLlenado_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824222016_ColaLlenadoAsistido'
)
BEGIN
    CREATE INDEX [IX_PropuestasLlenado_Estado_Certeza] ON [PropuestasLlenado] ([Estado], [Certeza]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824222016_ColaLlenadoAsistido'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PropuestasLlenado_TramiteSigerId_Campo] ON [PropuestasLlenado] ([TramiteSigerId], [Campo]) WHERE [Estado] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824222016_ColaLlenadoAsistido'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824222016_ColaLlenadoAsistido', N'9.0.0');
END;

COMMIT;
GO
GO
/* ---- fin de scripts\sql\12-cola-llenado-asistido.sql ---- */
GO

PRINT '';
PRINT '════════ 4. URL de SOL compuesta — Instituciones.RutaSol (script 13) ════════';
GO
/* ---- inicio de scripts\sql\13-url-sol-compuesta.sql (en línea; antes se encadenaba con :r) ---- */
-- =============================================================================
-- 13 — URL de SOL compuesta (plan de promoción SIGER, Fase 7)
--
-- QUÉ HACE
--   • Agrega TramitesSiger.SolTramo  (nvarchar(300), NULL) — el tramo final que
--     captura el trámite. Lo que va delante lo pone la institución.
--   • Agrega Instituciones.RutaSol   (nvarchar(300), NULL) — la ruta de esa
--     institución dentro de SOL. NULL es lo normal y significa «vale su llave».
--   • Rehace el índice IX_TramitesSiger_EstaEnSol para que también cubra SolTramo.
--   • Rehace el CHECK CK_TramitesSiger_Sol: desde ahora un trámite marcado como
--     «está en SOL» vale con el tramo NUEVO o con la URL completa heredada.
--     Sin esa segunda rama, guardar una ficha capturada como manda D-13 fallaría
--     contra la restricción.
--
-- QUÉ NO HACE
--   NO TOCA DATOS EXISTENTES. Las dos columnas nacen en NULL y ninguna fila se
--   modifica. Las direcciones ya cargadas se respetan tal cual (D-14).
--
-- ORDEN RESPECTO AL SCRIPT 12
--   Es independiente: 12 crea la tabla PropuestasLlenado y este no la mira. Se
--   puede aplicar antes o después. Cada bloque se guarda por su propia fila de
--   __EFMigrationsHistory, así que correrlo dos veces no hace nada la segunda.
--
-- CÓMO SE CORRE
--   sqlcmd -S <servidor> -d <base> -U <usuario> -P <clave> -f 65001 -i 13-url-sol-compuesta.sql
--
--   El -f 65001 no es opcional: el archivo va en UTF-8 y sin él los acentos de
--   los comentarios llegan rotos.
--
-- POR QUÉ EL SET DE ABAJO
--   El índice es FILTRADO (WHERE [EstaEnSol] = 1) y SQL Server exige
--   QUOTED_IDENTIFIER ON para crearlo; sqlcmd arranca con la opción en OFF y
--   fallaría con «Msg 1934». Que EF lo envuelva en EXEC(N'...') no salva: el SQL
--   dinámico hereda la opción del lote que lo llama, no la arregla.
-- =============================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    DROP INDEX [IX_TramitesSiger_EstaEnSol] ON [TramitesSiger];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    ALTER TABLE [TramitesSiger] DROP CONSTRAINT [CK_TramitesSiger_Sol];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    ALTER TABLE [TramitesSiger] ADD [SolTramo] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    ALTER TABLE [Instituciones] ADD [RutaSol] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TramitesSiger_EstaEnSol] ON [TramitesSiger] ([EstaEnSol]) INCLUDE ([SolUrl], [SolTramo]) WHERE [EstaEnSol] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    EXEC(N'ALTER TABLE [TramitesSiger] ADD CONSTRAINT [CK_TramitesSiger_Sol] CHECK ([EstaEnSol] = 0 OR [SolTramo] IS NOT NULL OR ([SolUrl] IS NOT NULL AND ([SolUrl] LIKE ''http://%'' OR [SolUrl] LIKE ''https://%'')))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825160050_UrlSolCompuesta', N'9.0.0');
END;

COMMIT;
GO
GO
/* ---- fin de scripts\sql\13-url-sol-compuesta.sql ---- */
GO

PRINT '';
PRINT '════════ 5. Ficha completa en el expediente (script 14) ════════';
GO
/* ---- inicio de scripts\sql\14-ficha-completa-en-expediente.sql (en línea; antes se encadenaba con :r) ---- */
-- =============================================================================
-- 14 — El expediente aprende a guardar todo lo que guarda una ficha SIGER
--      (plan de promoción SIGER, Fase 8)
--
-- POR QUÉ HACE FALTA
--   D-17 invierte quién manda: en cuanto una ficha SIGER queda enlazada a un
--   expediente, sus campos de contenido pasan a ser de SOLO LECTURA en la ficha
--   y solo se editan desde el expediente. Un campo que el expediente no sepa
--   guardar es un campo que, a partir de ese momento, nadie puede editar en
--   ninguna parte — y nada avisaría.
--
-- QUÉ HACE, EN ESTE ORDEN
--   1. Agrega ocho columnas a ExpedienteTramites (categoría, detalle de la
--      modalidad, gratuidad, vigencia, temporalidad, observaciones DIGER, si
--      está en SOL y el tramo del enlace) y crea las tablas
--      ExpedienteTramiteEntregables y ExpedienteTramiteLugares.
--   2. **Convierte** las modalidades de texto libre que ya existen y recién
--      entonces pone el CHECK del catálogo cerrado.
--   3. **Siembra** las dos tablas nuevas con lo que el expediente ya sabía.
--
-- EL ORDEN DE 2 NO ES NEGOCIABLE
--   El CHECK no se crea junto con las columnas a propósito. En Ensayo hay 202
--   trámites con la modalidad escrita a mano —«En línea», «En línea (total)»,
--   «Trámite en línea»…— y ninguno cumple el catálogo. Puesto antes, el script
--   fallaría contra cualquier base con datos.
--
-- ESTE SÍ TOCA DATOS. Es el primero de la serie que lo hace:
--   • UPDATE sobre ExpedienteTramites.Modalidad — el texto original NO se pierde,
--     se copia antes a ModalidadDetalle, y el Down de la migración lo devuelve.
--   • INSERT en las dos tablas nuevas, que están vacías.
--   Ninguna fila se borra.
--
-- Medido en Ensayo el 25-08-2026 sobre 240 trámites de expediente:
--   modalidades → 183 Virtual · 16 Hibrido · 3 Presencial · 38 sin modalidad
--                 (0 fuera del catálogo, 202 con su texto original conservado)
--   siembra     → 202 entregables (los 202 que tenían DocEntregado)
--                 236 lugares (197 con teléfono, 46 con dirección de sede)
--
-- CÓMO SE CORRE
--   sqlcmd -S <servidor> -d <base> -U <usuario> -P <clave> -f 65001 -i 14-ficha-completa-en-expediente.sql
--
--   El -f 65001 no es opcional: el archivo va en UTF-8 y sin él los acentos de
--   los comentarios llegan rotos.
--
-- ORDEN RESPECTO A LOS SCRIPTS 12 Y 13
--   Este supone el 13 aplicado (la fase anterior). Cada bloque se guarda por su
--   propia fila de __EFMigrationsHistory, así que correrlo dos veces no hace
--   nada la segunda.
--
-- POR QUÉ EL SET DE ABAJO
--   Hay índices FILTRADOS en juego y SQL Server exige QUOTED_IDENTIFIER ON para
--   crearlos y para insertar en las tablas que los tienen; sqlcmd arranca con la
--   opción en OFF y fallaría con «Msg 1934». Que EF lo envuelva en EXEC(N'...')
--   no salva: el SQL dinámico hereda la opción del lote que lo llama.
-- =============================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [CategoriaId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [EsGratuito] bit NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [EstaEnSol] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [ModalidadDetalle] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [ObservacionesDiger] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [SolTramo] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [Temporalidad] nvarchar(60) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [VigenciaDocumento] nvarchar(120) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    CREATE TABLE [ExpedienteTramiteEntregables] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [TramiteIndex] int NOT NULL,
        [Orden] int NOT NULL,
        [Entregable] nvarchar(500) NOT NULL,
        [Formato] nvarchar(120) NULL,
        [Presentacion] nvarchar(120) NULL,
        CONSTRAINT [PK_ExpedienteTramiteEntregables] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpedienteTramiteEntregables_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    CREATE TABLE [ExpedienteTramiteLugares] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [TramiteIndex] int NOT NULL,
        [Orden] int NOT NULL,
        [Lugar] nvarchar(400) NOT NULL,
        [Ciudad] nvarchar(120) NULL,
        [Direccion] nvarchar(500) NULL,
        [Telefonos] nvarchar(200) NULL,
        CONSTRAINT [PK_ExpedienteTramiteLugares] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpedienteTramiteLugares_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ExpedienteTramites_CategoriaId] ON [ExpedienteTramites] ([CategoriaId]) WHERE [CategoriaId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    CREATE INDEX [IX_ExpedienteTramiteEntregables_ExpedienteId_TramiteIndex] ON [ExpedienteTramiteEntregables] ([ExpedienteId], [TramiteIndex]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    CREATE INDEX [IX_ExpedienteTramiteLugares_ExpedienteId_TramiteIndex] ON [ExpedienteTramiteLugares] ([ExpedienteId], [TramiteIndex]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD CONSTRAINT [FK_ExpedienteTramites_CategoriasTramite_CategoriaId] FOREIGN KEY ([CategoriaId]) REFERENCES [CategoriasTramite] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825165843_FichaCompletaEnTramiteExpediente', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170007_ConvertirModalidadesExistentes'
)
BEGIN

                    EXEC(N'
                        UPDATE ExpedienteTramites
                        SET    ModalidadDetalle = Modalidad
                        WHERE  Modalidad IS NOT NULL
                          AND  LTRIM(RTRIM(Modalidad)) <> ''''
                          AND  ModalidadDetalle IS NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170007_ConvertirModalidadesExistentes'
)
BEGIN

                    UPDATE ExpedienteTramites
                    SET    Modalidad = CASE
                             WHEN Modalidad COLLATE Latin1_General_CI_AI IN ('Virtual', 'Presencial', 'Hibrido')
                                  THEN Modalidad
                             WHEN (Modalidad COLLATE Latin1_General_CI_AI LIKE '%linea%'
                                OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%virtual%'
                                OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%online%')
                              AND  Modalidad COLLATE Latin1_General_CI_AI LIKE '%presencial%'
                                  THEN 'Hibrido'
                             WHEN  Modalidad COLLATE Latin1_General_CI_AI LIKE '%linea%'
                                OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%virtual%'
                                OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%online%'
                                  THEN 'Virtual'
                             WHEN  Modalidad COLLATE Latin1_General_CI_AI LIKE '%presencial%'
                                  THEN 'Presencial'
                             ELSE NULL
                           END
                    WHERE  Modalidad IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170007_ConvertirModalidadesExistentes'
)
BEGIN
    EXEC(N'ALTER TABLE [ExpedienteTramites] ADD CONSTRAINT [CK_ExpedienteTramites_Modalidad] CHECK ([Modalidad] IS NULL OR [Modalidad] IN (''Virtual'', ''Presencial'', ''Hibrido''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170007_ConvertirModalidadesExistentes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825170007_ConvertirModalidadesExistentes', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170158_SembrarEntregablesYLugares'
)
BEGIN

                    INSERT INTO ExpedienteTramiteEntregables (ExpedienteId, TramiteIndex, Orden, Entregable)
                    SELECT ExpedienteId, TramiteIndex, 0, LTRIM(RTRIM(DocEntregado))
                    FROM   ExpedienteTramites
                    WHERE  DocEntregado IS NOT NULL AND LTRIM(RTRIM(DocEntregado)) <> '';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170158_SembrarEntregablesYLugares'
)
BEGIN

                    INSERT INTO ExpedienteTramiteLugares
                           (ExpedienteId, TramiteIndex, Orden, Lugar, Direccion, Telefonos)
                    SELECT t.ExpedienteId, t.TramiteIndex, 0,
                           e.Institucion, NULLIF(LTRIM(RTRIM(e.DirSede)), ''), NULLIF(LTRIM(RTRIM(t.Telefono)), '')
                    FROM   ExpedienteTramites t
                    JOIN   Expedientes e ON e.Id = t.ExpedienteId
                    WHERE  LTRIM(RTRIM(ISNULL(e.Institucion, ''))) <> ''
                      AND  ((t.Telefono IS NOT NULL AND LTRIM(RTRIM(t.Telefono)) <> '')
                        OR  (e.DirSede  IS NOT NULL AND LTRIM(RTRIM(e.DirSede))  <> ''));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170158_SembrarEntregablesYLugares'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825170158_SembrarEntregablesYLugares', N'9.0.0');
END;

COMMIT;
GO
GO
/* ---- fin de scripts\sql\14-ficha-completa-en-expediente.sql ---- */
GO

PRINT '';
PRINT '════════ 6. Columna FichaCompleta (script 15) ════════';
GO
/* ---- inicio de scripts\sql\15-ficha-completa-columna.sql (en línea; antes se encadenaba con :r) ---- */
-- =============================================================================
-- 15 — «Ficha completa» pasa a ser una columna de la base
--      (separación de la API pública)
--
-- POR QUÉ HACE FALTA
--   Hasta ahora la API pública decidía por su cuenta si una ficha estaba
--   completa: llevaba una copia en C# de la regla de PortalDigital y otra en
--   SQL dentro de su consulta. Eso ataba los dos sistemas — agregar un campo
--   obligatorio en PortalDigital obligaba a tocar la API y a desplegarla.
--
--   Con esta columna se invierte: PORTALDIGITAL DECIDE, LA API SOLO LEE.
--
-- POR QUÉ LA CALCULA LA BASE Y NO LA APLICACIÓN
--   Una columna que alguien tiene que acordarse de recalcular al guardar es una
--   columna que tarde o temprano miente: basta un camino de escritura que la
--   olvide —una carga masiva, un UPDATE directo, una importación— para que
--   quede desfasada, y nadie lo nota hasta que el ciudadano ve una ficha a
--   medias. Calculada por SQL Server no existe ese camino: no hay forma de
--   escribirla mal porque no hay forma de escribirla.
--
-- REQUISITO: VA DESPUÉS DEL 13
--   La expresión menciona SolTramo, que agrega el script 13. Si esa columna no
--   está, este script se detiene y lo dice; no deja la base a medias.
--
-- CÓMO SE CORRE
--   El archivo va en UTF-8 con BOM y se ejecuta con -f 65001. Y hace falta
--   QUOTED_IDENTIFIER ON: SQL Server no crea columnas calculadas persistidas sin
--   él, y sqlcmd arranca con la opción APAGADA.
--
--       sqlcmd -S <servidor> -d <base> -f 65001 -i 15-ficha-completa-columna.sql
--
--   Es idempotente: correrlo dos veces no hace nada la segunda.
--
-- QUÉ NO HACE
--   No cambia ningún dato y no cambia lo que ve nadie. La columna dice
--   exactamente lo mismo que ya decía el filtro de la API; se comprobó fila por
--   fila sobre las 1 057 fichas de Ensayo antes de escribir esto.
-- =============================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH('TramitesSiger', 'SolTramo') IS NULL
BEGIN
    RAISERROR(
        'Falta la columna SolTramo. Aplique primero 13-url-sol-compuesta.sql: la expresion de FichaCompleta la necesita.',
        16, 1);
    RETURN;
END

IF COL_LENGTH('TramitesSiger', 'FichaCompleta') IS NOT NULL
BEGIN
    PRINT 'FichaCompleta ya existe. Nada que hacer.';
    RETURN;
END

-- La comparación es contra NULL y no contra cadena vacía, igual que su gemela en
-- C# (FichaPublicaCompletitud.CamposFaltantes): un texto en blanco cuenta como
-- capturado. Apretar el criterio solo de un lado haría que la alerta del editor y
-- el catálogo público discreparan en silencio.
--
-- El costo se decide por CostoEsGratuito y no por CostoTexto: «es gratuito» ya es
-- una respuesta completa aunque no haya monto que escribir.
--
-- El enlace a SOL solo se exige cuando EstaEnSol, y vale cualquiera de los dos:
-- el tramo nuevo o la URL heredada de antes de la Fase 7.
ALTER TABLE TramitesSiger ADD FichaCompleta AS (
    CASE WHEN CategoriaId      IS NOT NULL
          AND Modalidad        IS NOT NULL
          AND TiempoTexto      IS NOT NULL
          AND CostoEsGratuito  IS NOT NULL
          AND (EstaEnSol = 0 OR SolUrl IS NOT NULL OR SolTramo IS NOT NULL)
         THEN CAST(1 AS bit)
         ELSE CAST(0 AS bit)
    END
) PERSISTED;

PRINT 'FichaCompleta creada.';

-- Registra la migración para que EF no intente aplicarla de nuevo.
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20260825190000_ColumnaFichaCompleta')
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT '20260825190000_ColumnaFichaCompleta', MAX(ProductVersion) FROM __EFMigrationsHistory;
GO

-- Qué quedó, para que quien lo corra lo vea sin tener que preguntar.
-- Va en su propio lote: SQL Server compila el lote entero antes de ejecutarlo, y en
-- el mismo lote que el ALTER esta consulta muere con «Invalid column name
-- 'FichaCompleta'» —la columna aún no existe al compilar— y se lleva consigo el
-- ALTER, que nunca llega a correr. Medido el 2026-10-22 contra GestionGD_new.
SELECT  'publicadas'            = COUNT(*),
        'de esas, completas'    = SUM(CASE WHEN FichaCompleta = 1 THEN 1 ELSE 0 END),
        'de esas, incompletas'  = SUM(CASE WHEN FichaCompleta = 0 THEN 1 ELSE 0 END)
FROM    TramitesSiger
WHERE   Publicado = 1;
GO
/* ---- fin de scripts\sql\15-ficha-completa-columna.sql ---- */
GO

/* ── 7. FusionRamaDev ────────────────────────────────────────────────────────────────
   Su Up está vacío: existe solo para guardar el snapshot unificado de dev + Jamil. Se
   registra al final y solo si las nueve anteriores están, porque registrarla con la
   base a medias haría creer a `dotnet ef` que no queda nada pendiente. */
PRINT '';
PRINT '════════ 7. Historial ════════';
IF (SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId IN (
        N'20260821210115_SigerIdOpcional',
        N'20260824173317_ArchivoSigerOriginal',
        N'20260824192857_ConciliacionConClaveEstable',
        N'20260824222016_ColaLlenadoAsistido',
        N'20260825160050_UrlSolCompuesta',
        N'20260825165843_FichaCompletaEnTramiteExpediente',
        N'20260825170007_ConvertirModalidadesExistentes',
        N'20260825170158_SembrarEntregablesYLugares',
        N'20260825190000_ColumnaFichaCompleta')) = 9
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory
                   WHERE MigrationId = N'20260827161837_FusionRamaDev')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260827161837_FusionRamaDev', N'9.0.0');
    PRINT '  FusionRamaDev ................. registrada';
END
ELSE PRINT '  FusionRamaDev ................. ya estaba (o faltan las anteriores: ver abajo)';
GO

PRINT '';
PRINT '════════ Comprobación ════════';

SELECT  N'Instituciones.RutaSol (el error de «Nueva reunión»)' AS Control,
        CASE WHEN COL_LENGTH(N'Instituciones', N'RutaSol') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT  N'TramitesSiger.SolTramo',
        CASE WHEN COL_LENGTH(N'TramitesSiger', N'SolTramo') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'TramitesSiger.IdSiger admite nulos',
        CASE WHEN COLUMNPROPERTY(OBJECT_ID(N'[TramitesSiger]'), N'IdSiger', 'AllowsNull') = 1
             THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT  N'Tabla FotosTramiteSiger',
        CASE WHEN OBJECT_ID(N'[FotosTramiteSiger]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'ExpedienteTramites.ClaveEstable',
        CASE WHEN COL_LENGTH(N'ExpedienteTramites', N'ClaveEstable') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'Tabla PropuestasLlenado',
        CASE WHEN OBJECT_ID(N'[PropuestasLlenado]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'Tabla ExpedienteTramiteEntregables',
        CASE WHEN OBJECT_ID(N'[ExpedienteTramiteEntregables]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'Tabla ExpedienteTramiteLugares',
        CASE WHEN OBJECT_ID(N'[ExpedienteTramiteLugares]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'CK_ExpedienteTramites_Modalidad',
        CASE WHEN OBJECT_ID(N'[CK_ExpedienteTramites_Modalidad]', N'C') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'TramitesSiger.FichaCompleta',
        CASE WHEN COL_LENGTH(N'TramitesSiger', N'FichaCompleta') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'Las diez migraciones en el historial',
        CASE WHEN (SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId IN (
                     N'20260821210115_SigerIdOpcional',
                     N'20260824173317_ArchivoSigerOriginal',
                     N'20260824192857_ConciliacionConClaveEstable',
                     N'20260824222016_ColaLlenadoAsistido',
                     N'20260825160050_UrlSolCompuesta',
                     N'20260825165843_FichaCompletaEnTramiteExpediente',
                     N'20260825170007_ConvertirModalidadesExistentes',
                     N'20260825170158_SembrarEntregablesYLugares',
                     N'20260825190000_ColumnaFichaCompleta',
                     N'20260827161837_FusionRamaDev')) = 10
             THEN 'presente' ELSE 'FALTA' END;

PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
GO

/* ######################################################################################
   PARTE 7 DE 9 — 2026-10-22_catalogo_insumos.sql
   Catálogo de insumos
   ###################################################################################### */
PRINT '';
PRINT '################ PARTE 7 DE 9: 2026-10-22_catalogo_insumos.sql ################';
GO

/* =====================================================================================
   Poner al día la base con los cambios al 2026-10-22 — catálogo de insumos
   -------------------------------------------------------------------------------------
   Va DESPUÉS de 2026-10-22_poner_al_dia.sql. Corra antes 00_diagnostico.sql: él dice si
   esta base está en condiciones de recibir éste.

   QUÉ TRAE, Y POR QUÉ

   Tres tablas nuevas — la migración 20261022203532_AgregarCatalogoDeInsumos:

     CategoriasInsumo          las categorías del catálogo: nombre único, orden, activa.
     Insumos                   los insumos: categoría, nombre (único DENTRO de su
                               categoría), descripción opcional, activo.
     ProyectoActividadInsumos  qué insumos necesita cada actividad de un proyecto, con
                               quién y cuándo lo agregó. Llave: actividad + insumo.

   Es para que el Administrador mantenga un catálogo de insumos —materiales, equipo,
   servicios— y en cada actividad se elijan los que necesita. Sin cantidad, costo ni
   inventario. En el portal se llama «Insumos», para no confundirlo con «Recursos y
   plantillas», que no cambia.

   Nada se borra: insumos y categorías se DESACTIVAN. Por eso las llaves foráneas hacia
   el catálogo son sin cascada: quitar una fila del catálogo a mano no puede llevarse lo
   que las actividades ya tenían asignado. La única cascada es la de la actividad: si se
   quita una actividad, se van sus insumos asignados.

   Y siembra las seis categorías iniciales —Materiales, Equipo y maquinaria,
   Herramientas, Equipo informático, Servicios y Otros— SOLO SI LA TABLA ESTÁ VACÍA. Son
   editables: sembrarlas nombre por nombre volvería a crear «Otros» en una segunda
   corrida si alguien ya la renombró a «Varios».

   ESTE SCRIPT ES ADITIVO. No toca ninguna tabla existente: ni agrega columnas, ni
   cambia tipos, ni mueve datos. Correrlo dos veces no cambia nada. Para deshacerlo está
   2026-10-22_revertir_insumos.sql.

   El permiso nuevo (Insumos.Catalogo.Editar) NO va acá: lo registra el portal solo al
   arrancar, sin otorgárselo a ningún rol. El Administrador lo tiene por serlo.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-22_catalogo_insumos.sql
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ── Guarda: ¿es ésta la base correcta? ──────────────────────────────────────────── */
IF OBJECT_ID(N'[ProyectoActividades]', N'U') IS NULL
   OR OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    THROW 50000,
        'Esta base no tiene ProyectoActividades o __EFMigrationsHistory. Corra 00_diagnostico.sql: no es la base del portal o está incompleta.',
        1;
END
GO

PRINT '════════ Catálogo de insumos ════════';
GO

/* ── 1. CategoriasInsumo ─────────────────────────────────────────────────────────────
   Idempotente contra el CATÁLOGO y no contra __EFMigrationsHistory: la fila del
   historial se escribe al final, así que una corrida que muera a la mitad la deja sin
   escribir y el historial no miente sobre lo que llegó a crearse. */
IF OBJECT_ID(N'[CategoriasInsumo]', N'U') IS NULL
BEGIN
    CREATE TABLE [CategoriasInsumo] (
        [Id]        int           IDENTITY(1,1) NOT NULL,
        [Nombre]    nvarchar(60)  NOT NULL,
        [Orden]     int           NOT NULL CONSTRAINT [DF_CategoriasInsumo_Orden]  DEFAULT (0),
        [Activo]    bit           NOT NULL CONSTRAINT [DF_CategoriasInsumo_Activo] DEFAULT (CONVERT(bit, 1)),
        [CreatedAt] datetime2     NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2     NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CategoriasInsumo] PRIMARY KEY ([Id])
    );
    PRINT '  CategoriasInsumo ................ creada';
END
ELSE PRINT '  CategoriasInsumo ................ ya estaba';
GO

/* ── 2. Insumos ──────────────────────────────────────────────────────────────────── */
IF OBJECT_ID(N'[Insumos]', N'U') IS NULL
BEGIN
    CREATE TABLE [Insumos] (
        [Id]          int           IDENTITY(1,1) NOT NULL,
        [CategoriaId] int           NOT NULL,
        [Nombre]      nvarchar(120) NOT NULL,
        [Descripcion] nvarchar(500) NULL,
        [Activo]      bit           NOT NULL CONSTRAINT [DF_Insumos_Activo] DEFAULT (CONVERT(bit, 1)),
        [CreatedAt]   datetime2     NOT NULL,
        [CreatedBy]   nvarchar(max) NULL,
        [UpdatedAt]   datetime2     NULL,
        [UpdatedBy]   nvarchar(max) NULL,
        CONSTRAINT [PK_Insumos] PRIMARY KEY ([Id]),
        /* Sin cascada: quitar una categoría no se lleva sus insumos. */
        CONSTRAINT [FK_Insumos_CategoriasInsumo_CategoriaId]
            FOREIGN KEY ([CategoriaId]) REFERENCES [CategoriasInsumo] ([Id])
    );
    PRINT '  Insumos ......................... creada';
END
ELSE PRINT '  Insumos ......................... ya estaba';
GO

/* ── 3. ProyectoActividadInsumos ─────────────────────────────────────────────────── */
IF OBJECT_ID(N'[ProyectoActividadInsumos]', N'U') IS NULL
BEGIN
    CREATE TABLE [ProyectoActividadInsumos] (
        [ActividadId] int           NOT NULL,
        [InsumoId]    int           NOT NULL,
        [AgregadoEl]  datetime2     NOT NULL,
        [AgregadoPor] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_ProyectoActividadInsumos] PRIMARY KEY ([ActividadId], [InsumoId]),
        /* Sin cascada desde el catálogo: lo asignado no se pierde por tocar el catálogo. */
        CONSTRAINT [FK_ProyectoActividadInsumos_Insumos_InsumoId]
            FOREIGN KEY ([InsumoId]) REFERENCES [Insumos] ([Id]),
        /* Con cascada desde la actividad: es la única ruta desde Proyectos, así que no
           choca con el error 1785. */
        CONSTRAINT [FK_ProyectoActividadInsumos_ProyectoActividades_ActividadId]
            FOREIGN KEY ([ActividadId]) REFERENCES [ProyectoActividades] ([Id]) ON DELETE CASCADE
    );
    PRINT '  ProyectoActividadInsumos ........ creada';
END
ELSE PRINT '  ProyectoActividadInsumos ........ ya estaba';
GO

/* ── 4. Índices ──────────────────────────────────────────────────────────────────── */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CategoriasInsumo_Nombre'
                 AND object_id = OBJECT_ID(N'[CategoriasInsumo]'))
BEGIN
    CREATE UNIQUE INDEX [IX_CategoriasInsumo_Nombre] ON [CategoriasInsumo] ([Nombre]);
    PRINT '  IX_CategoriasInsumo_Nombre ...... creado';
END
ELSE PRINT '  IX_CategoriasInsumo_Nombre ...... ya estaba';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CategoriasInsumo_Orden'
                 AND object_id = OBJECT_ID(N'[CategoriasInsumo]'))
BEGIN
    CREATE INDEX [IX_CategoriasInsumo_Orden] ON [CategoriasInsumo] ([Orden]);
    PRINT '  IX_CategoriasInsumo_Orden ....... creado';
END
ELSE PRINT '  IX_CategoriasInsumo_Orden ....... ya estaba';
GO

/* El nombre se repite entre categorías pero no dentro de una. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Insumos_CategoriaId_Nombre'
                 AND object_id = OBJECT_ID(N'[Insumos]'))
BEGIN
    CREATE UNIQUE INDEX [IX_Insumos_CategoriaId_Nombre] ON [Insumos] ([CategoriaId], [Nombre]);
    PRINT '  IX_Insumos_CategoriaId_Nombre ... creado';
END
ELSE PRINT '  IX_Insumos_CategoriaId_Nombre ... ya estaba';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProyectoActividadInsumos_InsumoId'
                 AND object_id = OBJECT_ID(N'[ProyectoActividadInsumos]'))
BEGIN
    CREATE INDEX [IX_ProyectoActividadInsumos_InsumoId] ON [ProyectoActividadInsumos] ([InsumoId]);
    PRINT '  IX ..._InsumoId ................. creado';
END
ELSE PRINT '  IX ..._InsumoId ................. ya estaba';
GO

/* ── 5. Las categorías iniciales ─────────────────────────────────────────────────────
   Solo si no hay NINGUNA: ver el encabezado. */
IF NOT EXISTS (SELECT 1 FROM [CategoriasInsumo])
BEGIN
    INSERT INTO [CategoriasInsumo] ([Nombre], [Orden], [Activo], [CreatedAt], [CreatedBy])
    VALUES (N'Materiales',          1, 1, SYSUTCDATETIME(), N'migracion'),
           (N'Equipo y maquinaria', 2, 1, SYSUTCDATETIME(), N'migracion'),
           (N'Herramientas',        3, 1, SYSUTCDATETIME(), N'migracion'),
           (N'Equipo informático',  4, 1, SYSUTCDATETIME(), N'migracion'),
           (N'Servicios',           5, 1, SYSUTCDATETIME(), N'migracion'),
           (N'Otros',               6, 1, SYSUTCDATETIME(), N'migracion');
    PRINT '  categorías iniciales ............ sembradas (6)';
END
ELSE PRINT '  categorías iniciales ............ ya había categorías, no se tocan';
GO

/* ── 6. El historial ─────────────────────────────────────────────────────────────────
   Al final y solo si las tres tablas están: escribirla antes haría que `dotnet ef
   database update` se saltara la migración dejando la base a medias. */
IF OBJECT_ID(N'[CategoriasInsumo]',         N'U') IS NOT NULL
   AND OBJECT_ID(N'[Insumos]',                  N'U') IS NOT NULL
   AND OBJECT_ID(N'[ProyectoActividadInsumos]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory
                   WHERE MigrationId = N'20261022203532_AgregarCatalogoDeInsumos')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20261022203532_AgregarCatalogoDeInsumos', N'9.0.0');
    PRINT '  historial ....................... registrado';
END
ELSE PRINT '  historial ....................... ya estaba';
GO

PRINT '';
PRINT '════════ Comprobación ════════';

SELECT  N'Tabla CategoriasInsumo' AS Control,
        CASE WHEN OBJECT_ID(N'[CategoriasInsumo]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT  N'Tabla Insumos',
        CASE WHEN OBJECT_ID(N'[Insumos]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'Tabla ProyectoActividadInsumos',
        CASE WHEN OBJECT_ID(N'[ProyectoActividadInsumos]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'IX_CategoriasInsumo_Nombre (único)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CategoriasInsumo_Nombre' AND is_unique = 1)
             THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT  N'IX_Insumos_CategoriaId_Nombre (único)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Insumos_CategoriaId_Nombre' AND is_unique = 1)
             THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT  N'IX_ProyectoActividadInsumos_InsumoId',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProyectoActividadInsumos_InsumoId')
             THEN 'presente' ELSE 'FALTA' END
UNION ALL
/* Que la categoría NO borre en cascada: es lo que protege lo ya asignado. */
SELECT  N'FK Insumos → CategoriasInsumo, sin cascada',
        CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys
                          WHERE name = N'FK_Insumos_CategoriasInsumo_CategoriaId'
                            AND delete_referential_action = 0)
             THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT  N'FK asignación → Insumos, sin cascada',
        CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys
                          WHERE name = N'FK_ProyectoActividadInsumos_Insumos_InsumoId'
                            AND delete_referential_action = 0)
             THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT  N'FK asignación → ProyectoActividades, en cascada',
        CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys
                          WHERE name = N'FK_ProyectoActividadInsumos_ProyectoActividades_ActividadId'
                            AND delete_referential_action = 1)
             THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT  N'Al menos una categoría de insumo',
        CASE WHEN EXISTS (SELECT 1 FROM [CategoriasInsumo]) THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT  N'Migración registrada en el historial',
        CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory
                          WHERE MigrationId = N'20261022203532_AgregarCatalogoDeInsumos')
             THEN 'presente' ELSE 'FALTA' END;

PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
GO

/* ######################################################################################
   PARTE 8 DE 9 — 2026-10-22_panel_control_notificaciones.sql
   Panel de control de notificaciones
   ###################################################################################### */
PRINT '';
PRINT '################ PARTE 8 DE 9: 2026-10-22_panel_control_notificaciones.sql ################';
GO

/* ════════════════════════════════════════════════════════════════════════════
   2026-10-22 · Panel de control → Notificaciones
   Va DESPUÉS de 2026-10-22_poner_al_dia.sql (el de la mañana del 2026-10-22, que
   trae las migraciones de Jamil). Corra antes 00_diagnostico.sql si no
   sabe en qué estado está la base.

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
GO

/* ######################################################################################
   PARTE 9 DE 9 — 2026-10-23_poner_al_dia.sql
   Reuniones a Outlook
   ###################################################################################### */
PRINT '';
PRINT '################ PARTE 9 DE 9: 2026-10-23_poner_al_dia.sql ################';
GO

/* =====================================================================================
   Poner al día la base con los cambios al 2026-10-23 — reuniones a Outlook (fase 1)
   -------------------------------------------------------------------------------------
   Va DESPUÉS de 2026-10-22_poner_al_dia.sql. Corra antes 00_diagnostico.sql.

   QUÉ TRAE — la migración EF 20261022221928_ReunionesAOutlook:

     ConexionesMicrosoft   una fila por usuario que conectó su cuenta Microsoft desde su
                           perfil. Guarda la caché de tokens de MSAL CIFRADA con Data
                           Protection: sin las llaves del servidor no se puede leer.
     Reuniones.Outlook*    nueve columnas que recuerdan la cita de Outlook de cada reunión:
                           su id, quién la organiza, el enlace de Teams, la huella de lo último
                           enviado y si la última sincronización quedó pendiente.

   ES ADITIVO. No cambia tipos ni mueve datos. OutlookConTeams nace en 1 para las filas
   existentes: Teams es el comportamiento por defecto.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-23_poner_al_dia.sql
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'[Reuniones]', N'U') IS NULL
   OR OBJECT_ID(N'[Usuarios]', N'U') IS NULL
   OR OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    THROW 50000,
        'Faltan Reuniones, Usuarios o __EFMigrationsHistory. Corra 00_diagnostico.sql: no es la base del portal o está incompleta.',
        1;
END
GO

PRINT '════════ Reuniones a Outlook ════════';
GO

IF OBJECT_ID(N'[ConexionesMicrosoft]', N'U') IS NULL
BEGIN
    CREATE TABLE [ConexionesMicrosoft] (
        [Id]                 uniqueidentifier NOT NULL,
        [Upn]                nvarchar(256)    NOT NULL,
        [HomeAccountId]      nvarchar(200)    NOT NULL,
        [CacheTokens]        varbinary(max)   NOT NULL,
        [ConectadaEl]        datetime2        NOT NULL,
        [RequiereReconexion] bit              NOT NULL,
        [UltimoError]        nvarchar(500)    NULL,
        [CreatedAt]          datetime2        NOT NULL,
        [CreatedBy]          nvarchar(max)    NULL,
        [UpdatedAt]          datetime2        NULL,
        [UpdatedBy]          nvarchar(max)    NULL,
        CONSTRAINT [PK_ConexionesMicrosoft] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConexionesMicrosoft_Usuarios_Id]
            FOREIGN KEY ([Id]) REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
    );
    PRINT '  ConexionesMicrosoft ............. creada';
END
ELSE PRINT '  ConexionesMicrosoft ............. ya estaba';
GO

IF COL_LENGTH(N'Reuniones', N'OutlookEventId') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookEventId] nvarchar(400) NULL; PRINT '  OutlookEventId .................. agregada'; END
ELSE PRINT '  OutlookEventId .................. ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookTransaccionId') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookTransaccionId] uniqueidentifier NULL; PRINT '  OutlookTransaccionId ............ agregada'; END
ELSE PRINT '  OutlookTransaccionId ............ ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookOrganizadorId') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookOrganizadorId] uniqueidentifier NULL; PRINT '  OutlookOrganizadorId ............ agregada'; END
ELSE PRINT '  OutlookOrganizadorId ............ ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookConTeams') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookConTeams] bit NOT NULL CONSTRAINT [DF_Reuniones_OutlookConTeams] DEFAULT (CONVERT(bit, 1)); PRINT '  OutlookConTeams ................. agregada'; END
ELSE PRINT '  OutlookConTeams ................. ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookEnlaceTeams') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookEnlaceTeams] nvarchar(1000) NULL; PRINT '  OutlookEnlaceTeams .............. agregada'; END
ELSE PRINT '  OutlookEnlaceTeams .............. ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookHuella') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookHuella] nchar(64) NULL; PRINT '  OutlookHuella ................... agregada'; END
ELSE PRINT '  OutlookHuella ................... ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookSincronizadaEl') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookSincronizadaEl] datetime2 NULL; PRINT '  OutlookSincronizadaEl ........... agregada'; END
ELSE PRINT '  OutlookSincronizadaEl ........... ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookPendiente') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookPendiente] bit NOT NULL CONSTRAINT [DF_Reuniones_OutlookPendiente] DEFAULT (CONVERT(bit, 0)); PRINT '  OutlookPendiente ................ agregada'; END
ELSE PRINT '  OutlookPendiente ................ ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookError') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookError] nvarchar(500) NULL; PRINT '  OutlookError .................... agregada'; END
ELSE PRINT '  OutlookError .................... ya estaba';
GO

/* El historial al final y solo si todo está: escribirlo antes haría que `dotnet ef` se
   saltara una base a medias. */
IF OBJECT_ID(N'[ConexionesMicrosoft]', N'U') IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookEventId')        IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookTransaccionId')  IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookOrganizadorId')  IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookConTeams')       IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookEnlaceTeams')    IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookHuella')         IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookSincronizadaEl') IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookPendiente')      IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookError')          IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261022221928_ReunionesAOutlook')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES (N'20261022221928_ReunionesAOutlook', N'9.0.0');
    PRINT '  historial ....................... registrado';
END
ELSE PRINT '  historial ....................... ya estaba';
GO

PRINT '';
PRINT '════════ Comprobación ════════';
SELECT N'Tabla ConexionesMicrosoft' AS Control,
       CASE WHEN OBJECT_ID(N'[ConexionesMicrosoft]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL SELECT N'FK ConexionesMicrosoft → Usuarios',
       CASE WHEN OBJECT_ID(N'[FK_ConexionesMicrosoft_Usuarios_Id]', N'F') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Las nueve columnas Outlook de Reuniones',
       CASE WHEN (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[Reuniones]') AND name LIKE N'Outlook%') = 9
            THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT N'Migración registrada en el historial',
       CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261022221928_ReunionesAOutlook')
            THEN 'presente' ELSE 'FALTA' END;
PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
GO

/* ══════════════════════════════════════════════════════════════════════════════════════
   COMPROBACIÓN FINAL — todo control debe decir «presente», nunca «FALTA».
   ══════════════════════════════════════════════════════════════════════════════════════ */
PRINT '';
PRINT '════════════════ Comprobación final ════════════════';
GO
-- La consulta que lee Reuniones.InstitucionDuenaId se compila aparte: si la columna faltara,
-- este lote fallaría al compilar en vez de decir FALTA.
SELECT N'Tabla TokensAccesoMcp' AS Control, CASE WHEN OBJECT_ID(N'[TokensAccesoMcp]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla ReunionAdjuntos' AS Control, CASE WHEN OBJECT_ID(N'[ReunionAdjuntos]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla ModuloAmbitos' AS Control, CASE WHEN OBJECT_ID(N'[ModuloAmbitos]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla PlantillasProyecto' AS Control, CASE WHEN OBJECT_ID(N'[PlantillasProyecto]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla PlantillaEntregables' AS Control, CASE WHEN OBJECT_ID(N'[PlantillaEntregables]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla PlantillaActividades' AS Control, CASE WHEN OBJECT_ID(N'[PlantillaActividades]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla FotosTramiteSiger' AS Control, CASE WHEN OBJECT_ID(N'[FotosTramiteSiger]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla PropuestasLlenado' AS Control, CASE WHEN OBJECT_ID(N'[PropuestasLlenado]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla ExpedienteTramiteEntregables' AS Control, CASE WHEN OBJECT_ID(N'[ExpedienteTramiteEntregables]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla ExpedienteTramiteLugares' AS Control, CASE WHEN OBJECT_ID(N'[ExpedienteTramiteLugares]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla CategoriasInsumo' AS Control, CASE WHEN OBJECT_ID(N'[CategoriasInsumo]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla Insumos' AS Control, CASE WHEN OBJECT_ID(N'[Insumos]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla ProyectoActividadInsumos' AS Control, CASE WHEN OBJECT_ID(N'[ProyectoActividadInsumos]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla NotificacionElementos' AS Control, CASE WHEN OBJECT_ID(N'[NotificacionElementos]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla AjustesNotificaciones' AS Control, CASE WHEN OBJECT_ID(N'[AjustesNotificaciones]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla ModulosNotificacion' AS Control, CASE WHEN OBJECT_ID(N'[ModulosNotificacion]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla CorreosPendientes' AS Control, CASE WHEN OBJECT_ID(N'[CorreosPendientes]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla PanelControlBitacora' AS Control, CASE WHEN OBJECT_ID(N'[PanelControlBitacora]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Tabla ConexionesMicrosoft' AS Control, CASE WHEN OBJECT_ID(N'[ConexionesMicrosoft]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT N'Reuniones.InstitucionDuenaId', CASE WHEN COL_LENGTH(N'Reuniones', N'InstitucionDuenaId') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT N'Usuarios.PasswordCambiadoEl', CASE WHEN COL_LENGTH(N'Usuarios', N'PasswordCambiadoEl') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT N'Instituciones.RutaSol', CASE WHEN COL_LENGTH(N'Instituciones', N'RutaSol') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT N'TramitesSiger.SolTramo', CASE WHEN COL_LENGTH(N'TramitesSiger', N'SolTramo') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT N'TramitesSiger.FichaCompleta', CASE WHEN COL_LENGTH(N'TramitesSiger', N'FichaCompleta') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT N'ExpedienteTramites.ClaveEstable', CASE WHEN COL_LENGTH(N'ExpedienteTramites', N'ClaveEstable') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT N'ExpedienteTramites.ModalidadDetalle', CASE WHEN COL_LENGTH(N'ExpedienteTramites', N'ModalidadDetalle') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT N'ConciliacionesSiger.ClaveTramite', CASE WHEN COL_LENGTH(N'ConciliacionesSiger', N'ClaveTramite') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT N'Notificaciones.Cantidad', CASE WHEN COL_LENGTH(N'Notificaciones', N'Cantidad') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT N'Notificaciones.FechaActualizacion', CASE WHEN COL_LENGTH(N'Notificaciones', N'FechaActualizacion') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT N'Reuniones.OutlookEventId', CASE WHEN COL_LENGTH(N'Reuniones', N'OutlookEventId') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT N'Reuniones.OutlookPendiente', CASE WHEN COL_LENGTH(N'Reuniones', N'OutlookPendiente') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT N'Reuniones.OutlookConTeams', CASE WHEN COL_LENGTH(N'Reuniones', N'OutlookConTeams') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT N'TramitesSiger.IdSiger admite nulos', CASE WHEN COLUMNPROPERTY(OBJECT_ID(N'[TramitesSiger]'), N'IdSiger', 'AllowsNull') = 1 THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'ConciliacionesSiger sin ExpedienteTramiteId (reemplazada por ClaveTramite)', CASE WHEN COL_LENGTH(N'ConciliacionesSiger', N'ExpedienteTramiteId') IS NULL THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Toda reunión tiene institución dueña', CASE WHEN EXISTS (SELECT 1 FROM Reuniones WHERE InstitucionDuenaId IS NULL) THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT N'Historial: 20260821210115_SigerIdOpcional', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20260821210115_SigerIdOpcional') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20260824173317_ArchivoSigerOriginal', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20260824173317_ArchivoSigerOriginal') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20260824192857_ConciliacionConClaveEstable', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20260824192857_ConciliacionConClaveEstable') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20260824222016_ColaLlenadoAsistido', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20260824222016_ColaLlenadoAsistido') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20260825160050_UrlSolCompuesta', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20260825160050_UrlSolCompuesta') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20260825165843_FichaCompletaEnTramiteExpediente', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20260825165843_FichaCompletaEnTramiteExpediente') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20260825170007_ConvertirModalidadesExistentes', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20260825170007_ConvertirModalidadesExistentes') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20260825170158_SembrarEntregablesYLugares', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20260825170158_SembrarEntregablesYLugares') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20260825190000_ColumnaFichaCompleta', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20260825190000_ColumnaFichaCompleta') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20260827161837_FusionRamaDev', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20260827161837_FusionRamaDev') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20260928160056_AddTokenAccesoMcp', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20260928160056_AddTokenAccesoMcp') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20261002162236_AddReunionAdjuntos', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261002162236_AddReunionAdjuntos') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20261014172324_AgregarInstitucionDuenaAReunion', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261014172324_AgregarInstitucionDuenaAReunion') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20261019171050_AgregarPlantillasDeProyecto', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261019171050_AgregarPlantillasDeProyecto') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20261019174802_AddPasswordCambiadoEl', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261019174802_AddPasswordCambiadoEl') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20261022203532_AgregarCatalogoDeInsumos', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261022203532_AgregarCatalogoDeInsumos') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20261022221928_ReunionesAOutlook', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261022221928_ReunionesAOutlook') THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Historial: 20261022223203_PanelControlNotificaciones', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261022223203_PanelControlNotificaciones') THEN 'presente' ELSE 'FALTA' END;
GO
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó: revise la salida.';
GO
