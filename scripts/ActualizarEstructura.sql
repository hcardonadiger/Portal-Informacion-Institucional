IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702225346_AddContactoActivo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260702225346_AddContactoActivo', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [CategoriasTicket] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(80) NOT NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CategoriasTicket] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [Instituciones] (
        [Id] nvarchar(120) NOT NULL,
        [Nombre] nvarchar(120) NOT NULL,
        [Descripcion] nvarchar(max) NULL,
        [NombreCorto] nvarchar(max) NULL,
        [LogoUrl] nvarchar(max) NULL,
        [InfoExtra] nvarchar(max) NULL,
        [Activo] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Instituciones] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [Movimientos] (
        [Id] nvarchar(120) NOT NULL,
        [Nombre] nvarchar(120) NOT NULL,
        [Descripcion] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Movimientos] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [Prefijos] (
        [PrefijoInstitucion] nvarchar(120) NOT NULL,
        [PrefijoMovimiento] nvarchar(120) NOT NULL,
        [UltimoValor] int NOT NULL,
        [UltimoCodigo] nvarchar(max) NULL,
        CONSTRAINT [PK_Prefijos] PRIMARY KEY ([PrefijoInstitucion], [PrefijoMovimiento])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [RolModuloAccesos] (
        [Id] int NOT NULL IDENTITY,
        [Rol] nvarchar(20) NOT NULL,
        [Modulo] nvarchar(40) NOT NULL,
        CONSTRAINT [PK_RolModuloAccesos] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [Usuarios] (
        [Id] uniqueidentifier NOT NULL,
        [Nombre] nvarchar(150) NOT NULL,
        [Correo] nvarchar(200) NOT NULL,
        [PasswordHash] nvarchar(300) NOT NULL,
        [Telefono] nvarchar(max) NULL,
        [Activo] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Usuarios] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [TemasTicket] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(80) NOT NULL,
        [HorasResolucion] int NOT NULL DEFAULT 0,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CategoriaId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TemasTicket] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TemasTicket_CategoriasTicket_CategoriaId] FOREIGN KEY ([CategoriaId]) REFERENCES [CategoriasTicket] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [Areas] (
        [Id] nvarchar(120) NOT NULL,
        [InstitucionId] nvarchar(120) NOT NULL,
        [Nombre] nvarchar(120) NOT NULL,
        [Descripcion] nvarchar(max) NULL,
        [NombreCorto] nvarchar(max) NULL,
        [LogoUrl] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Areas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Areas_Instituciones_InstitucionId] FOREIGN KEY ([InstitucionId]) REFERENCES [Instituciones] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [TramitesDefinicion] (
        [Id] int NOT NULL IDENTITY,
        [InstitucionId] nvarchar(120) NOT NULL,
        [Nombre] nvarchar(400) NOT NULL,
        [Orden] int NOT NULL,
        CONSTRAINT [PK_TramitesDefinicion] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TramitesDefinicion_Instituciones_InstitucionId] FOREIGN KEY ([InstitucionId]) REFERENCES [Instituciones] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [AsignacionesUsuario] (
        [Id] uniqueidentifier NOT NULL,
        [UsuarioId] uniqueidentifier NOT NULL,
        [InstitucionId] nvarchar(120) NOT NULL,
        [AreaId] nvarchar(120) NULL,
        [UnidadId] nvarchar(120) NULL,
        [Rol] nvarchar(60) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AsignacionesUsuario] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AsignacionesUsuario_Instituciones_InstitucionId] FOREIGN KEY ([InstitucionId]) REFERENCES [Instituciones] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AsignacionesUsuario_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [UsuarioTemas] (
        [Id] int NOT NULL IDENTITY,
        [UsuarioId] uniqueidentifier NOT NULL,
        [TemaId] int NOT NULL,
        CONSTRAINT [PK_UsuarioTemas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UsuarioTemas_TemasTicket_TemaId] FOREIGN KEY ([TemaId]) REFERENCES [TemasTicket] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UsuarioTemas_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [Unidades] (
        [Id] nvarchar(120) NOT NULL,
        [AreaId] nvarchar(120) NOT NULL,
        [Nombre] nvarchar(120) NOT NULL,
        [Descripcion] nvarchar(max) NULL,
        [NombreCorto] nvarchar(max) NULL,
        [LogoUrl] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Unidades] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Unidades_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [Contactos] (
        [Id] int NOT NULL IDENTITY,
        [InstitucionId] nvarchar(120) NOT NULL,
        [AreaId] nvarchar(120) NULL,
        [UnidadId] nvarchar(120) NULL,
        [Institucion] nvarchar(120) NOT NULL,
        [Nombre] nvarchar(150) NOT NULL,
        [Cargo] nvarchar(150) NULL,
        [Correo] nvarchar(200) NULL,
        [Telefono] nvarchar(40) NULL,
        [Notas] nvarchar(1000) NULL,
        [Origen] nvarchar(20) NOT NULL,
        [Activo] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Contactos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Contactos_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Contactos_Instituciones_InstitucionId] FOREIGN KEY ([InstitucionId]) REFERENCES [Instituciones] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Contactos_Unidades_UnidadId] FOREIGN KEY ([UnidadId]) REFERENCES [Unidades] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [Expedientes] (
        [Id] int NOT NULL IDENTITY,
        [Codigo] nvarchar(40) NOT NULL,
        [InstitucionId] nvarchar(120) NOT NULL,
        [AreaId] nvarchar(120) NULL,
        [UnidadId] nvarchar(120) NULL,
        [Institucion] nvarchar(120) NOT NULL,
        [OrigenExternoId] nvarchar(120) NULL,
        [FechaApertura] date NULL,
        [Analista] nvarchar(150) NOT NULL,
        [DirSede] nvarchar(300) NULL,
        [NumTramitesProd] int NOT NULL,
        [ContactoNombre] nvarchar(150) NULL,
        [ContactoCargo] nvarchar(150) NULL,
        [ContactoCorreo] nvarchar(200) NULL,
        [ContactoTel] nvarchar(40) NULL,
        [ObsLegal] nvarchar(4000) NULL,
        [NumFuncionarios] int NULL,
        [VolumenAnual] int NULL,
        [TiempoObservado] nvarchar(100) NULL,
        [TiempoNorma] nvarchar(100) NULL,
        [DescProceso] nvarchar(4000) NULL,
        [DocsAdicionales] nvarchar(2000) NULL,
        [ObsFlujo] nvarchar(2000) NULL,
        [FuncionariosDig] int NULL,
        [TiempoDig] nvarchar(100) NULL,
        [ObsModelo] nvarchar(2000) NULL,
        [InfraPersonal] nvarchar(30) NULL,
        [InfraPersonalTI] int NULL,
        [InfraRespSol] nvarchar(200) NULL,
        [InfraAcomp] nvarchar(10) NULL,
        [InfraDcModalidad] nvarchar(60) NULL,
        [InfraDcVirt] nvarchar(60) NULL,
        [InfraDcVirtOtro] nvarchar(120) NULL,
        [InfraDcDisp] nvarchar(60) NULL,
        [InfraDcObs] nvarchar(2000) NULL,
        [InfraPlan] nvarchar(4000) NULL,
        [EstadoExpediente] nvarchar(30) NOT NULL,
        [EstadoLevantamiento] nvarchar(30) NULL,
        [ObsExpediente] nvarchar(2000) NULL,
        [ObsLevantamiento] nvarchar(2000) NULL,
        [ValidadoDiger] nvarchar(150) NULL,
        [ValidadoInst] nvarchar(200) NULL,
        [FechaValidacion] date NULL,
        [NumActa] nvarchar(60) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Expedientes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Expedientes_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Expedientes_Instituciones_InstitucionId] FOREIGN KEY ([InstitucionId]) REFERENCES [Instituciones] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Expedientes_Unidades_UnidadId] FOREIGN KEY ([UnidadId]) REFERENCES [Unidades] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [Reuniones] (
        [Id] int NOT NULL IDENTITY,
        [Titulo] nvarchar(250) NOT NULL,
        [OrigenExternoId] nvarchar(60) NULL,
        [Visibilidad] nvarchar(20) NOT NULL DEFAULT N'Publica',
        [CreadoPorId] uniqueidentifier NULL,
        [RegistroToken] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [RegistroAbierto] bit NOT NULL,
        [Fecha] date NULL,
        [Hora] nvarchar(20) NULL,
        [Duracion] nvarchar(60) NULL,
        [Modalidad] nvarchar(40) NULL,
        [Lugar] nvarchar(250) NULL,
        [InstitucionId] nvarchar(120) NULL,
        [AreaId] nvarchar(120) NULL,
        [UnidadId] nvarchar(120) NULL,
        [Institucion] nvarchar(120) NULL,
        [Tipo] nvarchar(60) NULL,
        [EsCapacitacionPlataforma] bit NOT NULL,
        [ObjetivoAgenda] nvarchar(4000) NULL,
        [Desarrollo] nvarchar(max) NULL,
        [Tema] nvarchar(250) NULL,
        [ObjetivoCap] nvarchar(2000) NULL,
        [Contenido] nvarchar(4000) NULL,
        [EpNombre] nvarchar(150) NULL,
        [EpCargo] nvarchar(150) NULL,
        [EpCorreo] nvarchar(200) NULL,
        [EpTel] nvarchar(40) NULL,
        [FacNombre] nvarchar(150) NULL,
        [FacCargo] nvarchar(150) NULL,
        [FacCorreo] nvarchar(200) NULL,
        [Convocados] int NULL,
        [NumAsistentes] int NULL,
        [PctAsistencia] int NULL,
        [Satisfaccion] nvarchar(60) NULL,
        [Compromisos] nvarchar(4000) NULL,
        [ValDiger] nvarchar(200) NULL,
        [ValInst] nvarchar(200) NULL,
        [DocsRecursos] nvarchar(4000) NULL,
        [Foto1Url] nvarchar(600) NULL,
        [Foto1Desc] nvarchar(300) NULL,
        [Foto2Url] nvarchar(600) NULL,
        [Foto2Desc] nvarchar(300) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Reuniones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reuniones_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reuniones_Instituciones_InstitucionId] FOREIGN KEY ([InstitucionId]) REFERENCES [Instituciones] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reuniones_Unidades_UnidadId] FOREIGN KEY ([UnidadId]) REFERENCES [Unidades] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reuniones_Usuarios_CreadoPorId] FOREIGN KEY ([CreadoPorId]) REFERENCES [Usuarios] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [DocumentosInternos] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [Orden] int NOT NULL,
        [Documento] nvarchar(300) NOT NULL,
        [Area] nvarchar(200) NULL,
        [Obs] nvarchar(1000) NULL,
        CONSTRAINT [PK_DocumentosInternos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentosInternos_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [DocumentosSolicitados] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [Orden] int NOT NULL,
        [Nombre] nvarchar(300) NOT NULL,
        [Tipo] nvarchar(60) NULL,
        [Recibido] bit NOT NULL,
        [Fecha] date NULL,
        [Url] nvarchar(600) NULL,
        CONSTRAINT [PK_DocumentosSolicitados] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentosSolicitados_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [ExpedienteEtapaAvances] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [TramiteIndex] int NOT NULL,
        [SubId] nvarchar(20) NOT NULL,
        [Estado] int NOT NULL,
        CONSTRAINT [PK_ExpedienteEtapaAvances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpedienteEtapaAvances_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [ExpedienteSecciones] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [Seccion] int NOT NULL,
        [Estado] nvarchar(20) NOT NULL,
        [Nota] nvarchar(500) NULL,
        CONSTRAINT [PK_ExpedienteSecciones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpedienteSecciones_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [ExpedienteTramites] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [TramiteIndex] int NOT NULL,
        [NombreTramite] nvarchar(400) NOT NULL,
        [NombreCorto] nvarchar(120) NULL,
        [AreaResponsable] nvarchar(200) NULL,
        [Modalidad] nvarchar(60) NULL,
        [PlazoLegal] nvarchar(100) NULL,
        [Tercero] nvarchar(200) NULL,
        [TiempoReal] nvarchar(100) NULL,
        [MetodoPago] nvarchar(60) NULL,
        [PagoBanco] nvarchar(120) NULL,
        [PagoCuenta] nvarchar(60) NULL,
        [TgrInst] nvarchar(200) NULL,
        [TgrRubro] nvarchar(200) NULL,
        [TgrMonto] nvarchar(60) NULL,
        [DocEntregado] nvarchar(300) NULL,
        [Objetivo] nvarchar(2000) NULL,
        [Alcance] nvarchar(60) NULL,
        [AlcanceObs] nvarchar(2000) NULL,
        [Descripcion] nvarchar(4000) NULL,
        [Dirigido] nvarchar(300) NULL,
        [Horario] nvarchar(120) NULL,
        [Telefono] nvarchar(60) NULL,
        [EmailTramite] nvarchar(200) NULL,
        [SitioWeb] nvarchar(300) NULL,
        CONSTRAINT [PK_ExpedienteTramites] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpedienteTramites_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [FlujoNodos] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [TramiteIndex] int NOT NULL,
        [Fase] nvarchar(20) NOT NULL,
        [Orden] int NOT NULL,
        [Tipo] nvarchar(20) NOT NULL,
        [Titulo] nvarchar(300) NULL,
        [Area] nvarchar(200) NULL,
        [Tiempo] nvarchar(100) NULL,
        [DocEmitido] nvarchar(300) NULL,
        [Obs] nvarchar(2000) NULL,
        [RetornoA] nvarchar(100) NULL,
        CONSTRAINT [PK_FlujoNodos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FlujoNodos_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [FundamentosLegales] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [Orden] int NOT NULL,
        [Instrumento] nvarchar(400) NOT NULL,
        [Articulos] nvarchar(300) NULL,
        [Obs] nvarchar(1000) NULL,
        CONSTRAINT [PK_FundamentosLegales] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FundamentosLegales_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [InfraChecklist] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [Orden] int NOT NULL,
        [Grupo] nvarchar(120) NOT NULL,
        [Requisito] nvarchar(300) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Obs] nvarchar(1000) NULL,
        CONSTRAINT [PK_InfraChecklist] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InfraChecklist_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [InfraCondiciones] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [Condicion] nvarchar(120) NOT NULL,
        CONSTRAINT [PK_InfraCondiciones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InfraCondiciones_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [InfraPerfiles] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [Perfil] nvarchar(120) NOT NULL,
        [Nombre] nvarchar(150) NULL,
        [Correo] nvarchar(200) NULL,
        CONSTRAINT [PK_InfraPerfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InfraPerfiles_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [Tickets] (
        [Id] int NOT NULL IDENTITY,
        [Numero] nvarchar(30) NOT NULL,
        [Titulo] nvarchar(200) NOT NULL,
        [Descripcion] nvarchar(4000) NULL,
        [TemaId] int NULL,
        [Prioridad] nvarchar(20) NOT NULL,
        [Estado] nvarchar(20) NOT NULL,
        [InstitucionId] nvarchar(120) NULL,
        [AreaId] nvarchar(120) NULL,
        [UnidadId] nvarchar(120) NULL,
        [Institucion] nvarchar(120) NULL,
        [ExpedienteId] int NULL,
        [ExpedienteCodigo] nvarchar(40) NULL,
        [ReportanteNombre] nvarchar(150) NULL,
        [ReportanteCorreo] nvarchar(200) NULL,
        [ReportanteTelefono] nvarchar(40) NULL,
        [CreadoPorId] uniqueidentifier NULL,
        [CreadoPor] nvarchar(150) NULL,
        [AsignadoAId] uniqueidentifier NULL,
        [AsignadoA] nvarchar(150) NULL,
        [FechaResolucion] datetime2 NULL,
        [NotaResolucion] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Tickets_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Tickets_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Tickets_Instituciones_InstitucionId] FOREIGN KEY ([InstitucionId]) REFERENCES [Instituciones] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Tickets_TemasTicket_TemaId] FOREIGN KEY ([TemaId]) REFERENCES [TemasTicket] ([Id]),
        CONSTRAINT [FK_Tickets_Unidades_UnidadId] FOREIGN KEY ([UnidadId]) REFERENCES [Unidades] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Tickets_Usuarios_AsignadoAId] FOREIGN KEY ([AsignadoAId]) REFERENCES [Usuarios] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Tickets_Usuarios_CreadoPorId] FOREIGN KEY ([CreadoPorId]) REFERENCES [Usuarios] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [TramiteRequisitos] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [TramiteIndex] int NOT NULL,
        [Orden] int NOT NULL,
        [Requisito] nvarchar(500) NOT NULL,
        [Obs] nvarchar(2000) NULL,
        [Accion] nvarchar(30) NULL,
        [Justificacion] nvarchar(2000) NULL,
        CONSTRAINT [PK_TramiteRequisitos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TramiteRequisitos_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [AcuerdosReunion] (
        [Id] int NOT NULL IDENTITY,
        [ReunionId] int NOT NULL,
        [Orden] int NOT NULL,
        [Compromiso] nvarchar(500) NOT NULL,
        [Responsable] nvarchar(200) NULL,
        [Plazo] date NULL,
        [Estado] nvarchar(20) NOT NULL DEFAULT N'Pendiente',
        [FechaCumplimiento] date NULL,
        [NotaSeguimiento] nvarchar(1000) NULL,
        [SeguimientoActualizadoEl] datetime2 NULL,
        [SeguimientoActualizadoPor] nvarchar(150) NULL,
        CONSTRAINT [PK_AcuerdosReunion] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AcuerdosReunion_Reuniones_ReunionId] FOREIGN KEY ([ReunionId]) REFERENCES [Reuniones] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [Asistentes] (
        [Id] int NOT NULL IDENTITY,
        [ReunionId] int NOT NULL,
        [Nombre] nvarchar(150) NOT NULL,
        [Cargo] nvarchar(150) NULL,
        [Institucion] nvarchar(120) NULL,
        [Departamento] nvarchar(150) NULL,
        [Correo] nvarchar(200) NULL,
        [Telefono] nvarchar(40) NULL,
        [AutoRegistro] bit NOT NULL,
        [RegistradoEl] datetime2 NULL,
        CONSTRAINT [PK_Asistentes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Asistentes_Reuniones_ReunionId] FOREIGN KEY ([ReunionId]) REFERENCES [Reuniones] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [TicketAdjuntos] (
        [Id] int NOT NULL IDENTITY,
        [TicketId] int NOT NULL,
        [ComentarioId] int NULL,
        [NombreArchivo] nvarchar(260) NOT NULL,
        [Url] nvarchar(600) NOT NULL,
        [Tamano] bigint NOT NULL,
        CONSTRAINT [PK_TicketAdjuntos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketAdjuntos_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [TicketComentarios] (
        [Id] int NOT NULL IDENTITY,
        [TicketId] int NOT NULL,
        [Tipo] nvarchar(20) NOT NULL,
        [Autor] nvarchar(150) NOT NULL,
        [Texto] nvarchar(2000) NOT NULL,
        [Fecha] datetime2 NOT NULL,
        CONSTRAINT [PK_TicketComentarios] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketComentarios_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE TABLE [TicketTramites] (
        [Id] int NOT NULL IDENTITY,
        [TicketId] int NOT NULL,
        [TramiteDefinicionId] int NULL,
        [Tramite] nvarchar(400) NOT NULL,
        CONSTRAINT [PK_TicketTramites] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketTramites_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'CreatedAt', N'CreatedBy', N'Descripcion', N'InfoExtra', N'LogoUrl', N'Nombre', N'NombreCorto', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Instituciones]'))
        SET IDENTITY_INSERT [Instituciones] ON;
    EXEC(N'INSERT INTO [Instituciones] ([Id], [Activo], [CreatedAt], [CreatedBy], [Descripcion], [InfoExtra], [LogoUrl], [Nombre], [NombreCorto], [UpdatedAt], [UpdatedBy])
    VALUES (N''1'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''CONVIVIENDA'', NULL, NULL, NULL),
    (N''10'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''SEN'', NULL, NULL, NULL),
    (N''11'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''CONSUCOOP'', NULL, NULL, NULL),
    (N''12'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''CONATEL'', NULL, NULL, NULL),
    (N''13'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''IHCINE'', NULL, NULL, NULL),
    (N''14'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''SAG'', NULL, NULL, NULL),
    (N''15'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''SECAPPH'', NULL, NULL, NULL),
    (N''16'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''SRECI'', NULL, NULL, NULL),
    (N''17'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''SERNA'', NULL, NULL, NULL),
    (N''18'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''SGJD'', NULL, NULL, NULL),
    (N''19'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''CANATURH / IHT'', NULL, NULL, NULL),
    (N''2'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''COPECO'', NULL, NULL, NULL),
    (N''20'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''IP'', NULL, NULL, NULL),
    (N''21'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''SENASA'', NULL, NULL, NULL),
    (N''22'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''SESAL'', NULL, NULL, NULL),
    (N''23'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''FOSOVI'', NULL, NULL, NULL),
    (N''24'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''IHT'', NULL, NULL, NULL),
    (N''3'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''SIT'', NULL, NULL, NULL),
    (N''4'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''IHADFA'', NULL, NULL, NULL),
    (N''5'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''BANHPROVI'', NULL, NULL, NULL),
    (N''6'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''INPREUNAH'', NULL, NULL, NULL),
    (N''7'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''CNBS'', NULL, NULL, NULL),
    (N''8'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''INPREMA'', NULL, NULL, NULL),
    (N''9'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''IHTT'', NULL, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'CreatedAt', N'CreatedBy', N'Descripcion', N'InfoExtra', N'LogoUrl', N'Nombre', N'NombreCorto', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Instituciones]'))
        SET IDENTITY_INSERT [Instituciones] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AcuerdosReunion_Estado] ON [AcuerdosReunion] ([Estado]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AcuerdosReunion_Plazo] ON [AcuerdosReunion] ([Plazo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AcuerdosReunion_ReunionId] ON [AcuerdosReunion] ([ReunionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Areas_InstitucionId] ON [Areas] ([InstitucionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AsignacionesUsuario_InstitucionId] ON [AsignacionesUsuario] ([InstitucionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AsignacionesUsuario_UsuarioId_InstitucionId_AreaId_UnidadId] ON [AsignacionesUsuario] ([UsuarioId], [InstitucionId], [AreaId], [UnidadId]) WHERE [AreaId] IS NOT NULL AND [UnidadId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Asistentes_ReunionId] ON [Asistentes] ([ReunionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CategoriasTicket_Nombre] ON [CategoriasTicket] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Contactos_AreaId] ON [Contactos] ([AreaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Contactos_Institucion] ON [Contactos] ([Institucion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Contactos_InstitucionId] ON [Contactos] ([InstitucionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Contactos_Nombre] ON [Contactos] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Contactos_UnidadId] ON [Contactos] ([UnidadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DocumentosInternos_ExpedienteId] ON [DocumentosInternos] ([ExpedienteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DocumentosSolicitados_ExpedienteId] ON [DocumentosSolicitados] ([ExpedienteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExpedienteEtapaAvances_ExpedienteId_TramiteIndex_SubId] ON [ExpedienteEtapaAvances] ([ExpedienteId], [TramiteIndex], [SubId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Expedientes_AreaId] ON [Expedientes] ([AreaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Expedientes_Codigo] ON [Expedientes] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Expedientes_CreatedAt] ON [Expedientes] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Expedientes_EstadoExpediente] ON [Expedientes] ([EstadoExpediente]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Expedientes_InstitucionId] ON [Expedientes] ([InstitucionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Expedientes_OrigenExternoId] ON [Expedientes] ([OrigenExternoId]) WHERE [OrigenExternoId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Expedientes_UnidadId] ON [Expedientes] ([UnidadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ExpedienteSecciones_ExpedienteId] ON [ExpedienteSecciones] ([ExpedienteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ExpedienteTramites_ExpedienteId_TramiteIndex] ON [ExpedienteTramites] ([ExpedienteId], [TramiteIndex]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FlujoNodos_ExpedienteId_TramiteIndex_Fase] ON [FlujoNodos] ([ExpedienteId], [TramiteIndex], [Fase]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FundamentosLegales_ExpedienteId] ON [FundamentosLegales] ([ExpedienteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InfraChecklist_ExpedienteId] ON [InfraChecklist] ([ExpedienteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InfraCondiciones_ExpedienteId] ON [InfraCondiciones] ([ExpedienteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InfraPerfiles_ExpedienteId] ON [InfraPerfiles] ([ExpedienteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Instituciones_Nombre] ON [Instituciones] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reuniones_AreaId] ON [Reuniones] ([AreaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reuniones_CreadoPorId] ON [Reuniones] ([CreadoPorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reuniones_Fecha] ON [Reuniones] ([Fecha]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reuniones_InstitucionId] ON [Reuniones] ([InstitucionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Reuniones_OrigenExternoId] ON [Reuniones] ([OrigenExternoId]) WHERE [OrigenExternoId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Reuniones_RegistroToken] ON [Reuniones] ([RegistroToken]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reuniones_UnidadId] ON [Reuniones] ([UnidadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reuniones_Visibilidad_CreadoPorId] ON [Reuniones] ([Visibilidad], [CreadoPorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RolModuloAccesos_Rol_Modulo] ON [RolModuloAccesos] ([Rol], [Modulo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TemasTicket_CategoriaId] ON [TemasTicket] ([CategoriaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TemasTicket_Nombre] ON [TemasTicket] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TicketAdjuntos_TicketId] ON [TicketAdjuntos] ([TicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TicketComentarios_TicketId] ON [TicketComentarios] ([TicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_AreaId] ON [Tickets] ([AreaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_AsignadoAId] ON [Tickets] ([AsignadoAId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_CreadoPorId] ON [Tickets] ([CreadoPorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_CreatedAt] ON [Tickets] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_Estado] ON [Tickets] ([Estado]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_ExpedienteId] ON [Tickets] ([ExpedienteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_InstitucionId] ON [Tickets] ([InstitucionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tickets_Numero] ON [Tickets] ([Numero]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_TemaId] ON [Tickets] ([TemaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_UnidadId] ON [Tickets] ([UnidadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TicketTramites_TicketId] ON [TicketTramites] ([TicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TramiteRequisitos_ExpedienteId_TramiteIndex] ON [TramiteRequisitos] ([ExpedienteId], [TramiteIndex]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TramitesDefinicion_InstitucionId_Orden] ON [TramitesDefinicion] ([InstitucionId], [Orden]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Unidades_AreaId] ON [Unidades] ([AreaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Usuarios_Correo] ON [Usuarios] ([Correo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UsuarioTemas_TemaId] ON [UsuarioTemas] ([TemaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UsuarioTemas_UsuarioId_TemaId] ON [UsuarioTemas] ([UsuarioId], [TemaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706200739_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260706200739_InitialCreate', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706202412_AddDigerInstitucionSeed'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'CreatedAt', N'CreatedBy', N'Descripcion', N'InfoExtra', N'LogoUrl', N'Nombre', N'NombreCorto', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Instituciones]'))
        SET IDENTITY_INSERT [Instituciones] ON;
    EXEC(N'INSERT INTO [Instituciones] ([Id], [Activo], [CreatedAt], [CreatedBy], [Descripcion], [InfoExtra], [LogoUrl], [Nombre], [NombreCorto], [UpdatedAt], [UpdatedBy])
    VALUES (N''DIGER'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''DIGER (Sistema)'', NULL, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'CreatedAt', N'CreatedBy', N'Descripcion', N'InfoExtra', N'LogoUrl', N'Nombre', N'NombreCorto', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Instituciones]'))
        SET IDENTITY_INSERT [Instituciones] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706202412_AddDigerInstitucionSeed'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260706202412_AddDigerInstitucionSeed', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707120000_AddTicketTemaOtro'
)
BEGIN
    ALTER TABLE [Tickets] ADD [TemaOtro] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707120000_AddTicketTemaOtro'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260707120000_AddTicketTemaOtro', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707151717_AddReunionInstituciones'
)
BEGIN
    CREATE TABLE [ReunionInstituciones] (
        [Id] int NOT NULL IDENTITY,
        [ReunionId] int NOT NULL,
        [InstitucionId] nvarchar(120) NOT NULL,
        [Orden] int NOT NULL,
        CONSTRAINT [PK_ReunionInstituciones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReunionInstituciones_Instituciones_InstitucionId] FOREIGN KEY ([InstitucionId]) REFERENCES [Instituciones] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReunionInstituciones_Reuniones_ReunionId] FOREIGN KEY ([ReunionId]) REFERENCES [Reuniones] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707151717_AddReunionInstituciones'
)
BEGIN
    CREATE INDEX [IX_ReunionInstituciones_InstitucionId] ON [ReunionInstituciones] ([InstitucionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707151717_AddReunionInstituciones'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReunionInstituciones_ReunionId_InstitucionId] ON [ReunionInstituciones] ([ReunionId], [InstitucionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707151717_AddReunionInstituciones'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260707151717_AddReunionInstituciones', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707154325_AddSoftDelete'
)
BEGIN
    ALTER TABLE [Tickets] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707154325_AddSoftDelete'
)
BEGIN
    ALTER TABLE [Reuniones] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707154325_AddSoftDelete'
)
BEGIN
    ALTER TABLE [Expedientes] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707154325_AddSoftDelete'
)
BEGIN
    ALTER TABLE [Contactos] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707154325_AddSoftDelete'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260707154325_AddSoftDelete', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260708165625_AddSpGenerarCodigoMovimiento'
)
BEGIN

                CREATE OR ALTER PROCEDURE SP_GenerarCodigoMovimiento
                    @Institucion VARCHAR(50),
                    @Movimiento VARCHAR(50),
                    @NuevoCodigo VARCHAR(100) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    BEGIN TRANSACTION;
                    
                    DECLARE @ActualValor INT = 0;
                    
                    -- UPDLOCK para evitar concurrencia
                    SELECT @ActualValor = UltimoValor
                    FROM Prefijos WITH (UPDLOCK, HOLDLOCK)
                    WHERE PrefijoInstitucion = @Institucion AND PrefijoMovimiento = @Movimiento;
                    
                    IF @@ROWCOUNT = 0
                    BEGIN
                        SET @ActualValor = 1;
                        SET @NuevoCodigo = @Institucion + '-' + @Movimiento + '-1';
                        
                        INSERT INTO Prefijos (PrefijoInstitucion, PrefijoMovimiento, UltimoValor, UltimoCodigo)
                        VALUES (@Institucion, @Movimiento, @ActualValor, @NuevoCodigo);
                    END
                    ELSE
                    BEGIN
                        SET @ActualValor = @ActualValor + 1;
                        SET @NuevoCodigo = @Institucion + '-' + @Movimiento + '-' + CAST(@ActualValor AS VARCHAR(20));
                        
                        UPDATE Prefijos
                        SET UltimoValor = @ActualValor,
                            UltimoCodigo = @NuevoCodigo
                        WHERE PrefijoInstitucion = @Institucion AND PrefijoMovimiento = @Movimiento;
                    END
                    
                    COMMIT TRANSACTION;
                END
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260708165625_AddSpGenerarCodigoMovimiento'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260708165625_AddSpGenerarCodigoMovimiento', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260708204449_FixPendingModelChanges'
)
BEGIN
    DROP INDEX [IX_ReunionInstituciones_InstitucionId] ON [ReunionInstituciones];
    DROP INDEX [IX_ReunionInstituciones_ReunionId_InstitucionId] ON [ReunionInstituciones];
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ReunionInstituciones]') AND [c].[name] = N'InstitucionId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [ReunionInstituciones] DROP CONSTRAINT [' + @var0 + '];');
    EXEC(N'UPDATE [ReunionInstituciones] SET [InstitucionId] = N'''' WHERE [InstitucionId] IS NULL');
    ALTER TABLE [ReunionInstituciones] ALTER COLUMN [InstitucionId] nvarchar(120) NOT NULL;
    ALTER TABLE [ReunionInstituciones] ADD DEFAULT N'' FOR [InstitucionId];
    CREATE INDEX [IX_ReunionInstituciones_InstitucionId] ON [ReunionInstituciones] ([InstitucionId]);
    CREATE UNIQUE INDEX [IX_ReunionInstituciones_ReunionId_InstitucionId] ON [ReunionInstituciones] ([ReunionId], [InstitucionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260708204449_FixPendingModelChanges'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260708204449_FixPendingModelChanges', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''1'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''10'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''11'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''12'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''13'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''14'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''15'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''16'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''17'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''18'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''19'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''2'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''20'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''21'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''22'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''23'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''24'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''3'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''4'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''5'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''6'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''7'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''8'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'DELETE FROM [Instituciones]
    WHERE [Id] = N''9'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    EXEC(N'UPDATE [Instituciones] SET [Nombre] = N''Dirección de Gestión por Resultados''
    WHERE [Id] = N''DIGER'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'CreatedAt', N'CreatedBy', N'Descripcion', N'InfoExtra', N'LogoUrl', N'Nombre', N'NombreCorto', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Instituciones]'))
        SET IDENTITY_INSERT [Instituciones] ON;
    EXEC(N'INSERT INTO [Instituciones] ([Id], [Activo], [CreatedAt], [CreatedBy], [Descripcion], [InfoExtra], [LogoUrl], [Nombre], [NombreCorto], [UpdatedAt], [UpdatedBy])
    VALUES (N''BANHPROVI'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Banco Hondureño para la Producción y la Vivienda'', NULL, NULL, NULL),
    (N''CANATURH'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Cámara Nacional de Turismo de Honduras'', NULL, NULL, NULL),
    (N''CNBS'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Comisión Nacional de Bancos y Seguros'', NULL, NULL, NULL),
    (N''CONATEL'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Comisión Nacional de Telecomunicaciones'', NULL, NULL, NULL),
    (N''CONSUCOOP'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Consejo Nacional Supervisor de Cooperativas'', NULL, NULL, NULL),
    (N''CONVIVIENDA'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Comisión Nacional de Vivienda y Asentamientos Humanos'', NULL, NULL, NULL),
    (N''COPECO'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Secretaría de Estado en los Despachos de Gestión de Riesgos y Contingencias Nacionales'', NULL, NULL, NULL),
    (N''FOSOVI'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Fondo Social de Vivienda'', NULL, NULL, NULL),
    (N''IHADFA'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Instituto Hondureño para la Prevención del Alcoholismo, Drogadicción y Farmacodependencia'', NULL, NULL, NULL),
    (N''IHCINE'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Instituto Hondureño de Cinematografía'', NULL, NULL, NULL),
    (N''IHT'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Instituto Hondureño de Turismo'', NULL, NULL, NULL),
    (N''IHTT'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Instituto Hondureño del Transporte Terrestre'', NULL, NULL, NULL),
    (N''INPREMA'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Instituto Nacional de Previsión del Magisterio'', NULL, NULL, NULL),
    (N''INPREUNAH'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Instituto de Previsión de la Universidad Nacional Autónoma de Honduras'', NULL, NULL, NULL),
    (N''IP'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Instituto de la Propiedad'', NULL, NULL, NULL),
    (N''SAG'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Secretaría de Agricultura y Ganadería'', NULL, NULL, NULL),
    (N''SECAPPH'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Secretaría de las Culturas, las Artes y los Patrimonios de los Pueblos de Honduras'', NULL, NULL, NULL),
    (N''SEN'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Secretaría de Energía'', NULL, NULL, NULL),
    (N''SENASA'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Servicio Nacional de Sanidad e Inocuidad Agroalimentaria'', NULL, NULL, NULL),
    (N''SERNA'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Secretaría de Recursos Naturales y Ambiente'', NULL, NULL, NULL),
    (N''SESAL'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Secretaría de Salud'', NULL, NULL, NULL),
    (N''SGJD'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Secretaría de Gobernación, Justicia y Descentralización'', NULL, NULL, NULL),
    (N''SIT'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Secretaría de Infraestructura y Transporte'', NULL, NULL, NULL),
    (N''SRECI'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, N''Secretaría de Relaciones Exteriores y Cooperación Internacional'', NULL, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'CreatedAt', N'CreatedBy', N'Descripcion', N'InfoExtra', N'LogoUrl', N'Nombre', N'NombreCorto', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Instituciones]'))
        SET IDENTITY_INSERT [Instituciones] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709163652_UpdateInstitucionesSeed'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260709163652_UpdateInstitucionesSeed', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709205323_AddCertificadoThumbprint'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [CertificadoThumbprint] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709205323_AddCertificadoThumbprint'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260709205323_AddCertificadoThumbprint', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709221010_FixContactoActivoDefault'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Contactos]') AND [c].[name] = N'Activo');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Contactos] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Contactos] ADD DEFAULT CAST(1 AS bit) FOR [Activo];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709221010_FixContactoActivoDefault'
)
BEGIN
    UPDATE Contactos SET Activo = 1 WHERE Activo = 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709221010_FixContactoActivoDefault'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260709221010_FixContactoActivoDefault', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709221250_AddReunionSatisfaccionCalificacion'
)
BEGIN
    ALTER TABLE [Reuniones] ADD [SatisfaccionCalificacion] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709221250_AddReunionSatisfaccionCalificacion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260709221250_AddReunionSatisfaccionCalificacion', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709221700_SwapExpedienteSeccionEstadoOrden'
)
BEGIN
    UPDATE ExpedienteSecciones SET Seccion = -1 WHERE Seccion = 5;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709221700_SwapExpedienteSeccionEstadoOrden'
)
BEGIN
    UPDATE ExpedienteSecciones SET Seccion = 5  WHERE Seccion = 4;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709221700_SwapExpedienteSeccionEstadoOrden'
)
BEGIN
    UPDATE ExpedienteSecciones SET Seccion = 4  WHERE Seccion = -1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709221700_SwapExpedienteSeccionEstadoOrden'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260709221700_SwapExpedienteSeccionEstadoOrden', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709222307_AddPlantillaTramite'
)
BEGIN
    ALTER TABLE [TramiteRequisitos] ADD [EsPersonalizado] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709222307_AddPlantillaTramite'
)
BEGIN
    ALTER TABLE [TramiteRequisitos] ADD [PlantillaOrigenId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709222307_AddPlantillaTramite'
)
BEGIN
    ALTER TABLE [FundamentosLegales] ADD [EsPersonalizado] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709222307_AddPlantillaTramite'
)
BEGIN
    ALTER TABLE [FundamentosLegales] ADD [PlantillaOrigenId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709222307_AddPlantillaTramite'
)
BEGIN
    CREATE TABLE [PlantillasTramite] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(300) NOT NULL,
        [Activa] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PlantillasTramite] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709222307_AddPlantillaTramite'
)
BEGIN
    CREATE TABLE [PlantillaFundamentosLegales] (
        [Id] int NOT NULL IDENTITY,
        [PlantillaId] int NOT NULL,
        [Orden] int NOT NULL,
        [Instrumento] nvarchar(400) NOT NULL,
        [Articulos] nvarchar(300) NULL,
        [Obs] nvarchar(1000) NULL,
        CONSTRAINT [PK_PlantillaFundamentosLegales] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlantillaFundamentosLegales_PlantillasTramite_PlantillaId] FOREIGN KEY ([PlantillaId]) REFERENCES [PlantillasTramite] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709222307_AddPlantillaTramite'
)
BEGIN
    CREATE TABLE [PlantillaRequisitos] (
        [Id] int NOT NULL IDENTITY,
        [PlantillaId] int NOT NULL,
        [Orden] int NOT NULL,
        [Requisito] nvarchar(500) NOT NULL,
        [Obs] nvarchar(2000) NULL,
        CONSTRAINT [PK_PlantillaRequisitos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlantillaRequisitos_PlantillasTramite_PlantillaId] FOREIGN KEY ([PlantillaId]) REFERENCES [PlantillasTramite] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709222307_AddPlantillaTramite'
)
BEGIN
    CREATE INDEX [IX_PlantillaFundamentosLegales_PlantillaId] ON [PlantillaFundamentosLegales] ([PlantillaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709222307_AddPlantillaTramite'
)
BEGIN
    CREATE INDEX [IX_PlantillaRequisitos_PlantillaId] ON [PlantillaRequisitos] ([PlantillaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709222307_AddPlantillaTramite'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlantillasTramite_Nombre] ON [PlantillasTramite] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709222307_AddPlantillaTramite'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260709222307_AddPlantillaTramite', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717202416_AddReunionHilo'
)
BEGIN
    ALTER TABLE [Reuniones] ADD [HiloId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717202416_AddReunionHilo'
)
BEGIN
    CREATE INDEX [IX_Reuniones_HiloId] ON [Reuniones] ([HiloId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717202416_AddReunionHilo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717202416_AddReunionHilo', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260720161207_AddNotificaciones'
)
BEGIN
    CREATE TABLE [Notificaciones] (
        [Id] int NOT NULL IDENTITY,
        [DestinatarioId] uniqueidentifier NOT NULL,
        [Tipo] nvarchar(30) NOT NULL,
        [Titulo] nvarchar(200) NOT NULL,
        [Url] nvarchar(500) NULL,
        [Leida] bit NOT NULL,
        [FechaCreacion] datetime2 NOT NULL,
        [FechaLectura] datetime2 NULL,
        CONSTRAINT [PK_Notificaciones] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260720161207_AddNotificaciones'
)
BEGIN
    CREATE INDEX [IX_Notificaciones_DestinatarioId_Leida] ON [Notificaciones] ([DestinatarioId], [Leida]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260720161207_AddNotificaciones'
)
BEGIN
    CREATE INDEX [IX_Notificaciones_FechaCreacion] ON [Notificaciones] ([FechaCreacion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260720161207_AddNotificaciones'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260720161207_AddNotificaciones', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151629_AddChatSoporte'
)
BEGIN
    CREATE TABLE [ChatSesiones] (
        [Id] int NOT NULL IDENTITY,
        [UsuarioId] uniqueidentifier NOT NULL,
        [UsuarioNombre] nvarchar(120) NOT NULL,
        [TecnicoId] uniqueidentifier NULL,
        [TecnicoNombre] nvarchar(120) NULL,
        [TemaId] int NULL,
        [TemaNombre] nvarchar(80) NULL,
        [TicketId] int NULL,
        [Estado] nvarchar(20) NOT NULL,
        [Calificacion] tinyint NULL,
        [Inicio] datetime2 NOT NULL,
        [Cierre] datetime2 NULL,
        CONSTRAINT [PK_ChatSesiones] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151629_AddChatSoporte'
)
BEGIN
    CREATE TABLE [ChatMensajes] (
        [Id] int NOT NULL IDENTITY,
        [SesionId] int NOT NULL,
        [Texto] nvarchar(2000) NOT NULL,
        [EsDelTecnico] bit NOT NULL,
        [EsSistema] bit NOT NULL,
        [AutorNombre] nvarchar(120) NOT NULL,
        [Enviado] datetime2 NOT NULL,
        [Leido] bit NOT NULL,
        CONSTRAINT [PK_ChatMensajes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatMensajes_ChatSesiones_SesionId] FOREIGN KEY ([SesionId]) REFERENCES [ChatSesiones] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151629_AddChatSoporte'
)
BEGIN
    CREATE INDEX [IX_ChatMensajes_SesionId_Leido] ON [ChatMensajes] ([SesionId], [Leido]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151629_AddChatSoporte'
)
BEGIN
    CREATE INDEX [IX_ChatSesiones_Inicio] ON [ChatSesiones] ([Inicio]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151629_AddChatSoporte'
)
BEGIN
    CREATE INDEX [IX_ChatSesiones_TecnicoId_Estado] ON [ChatSesiones] ([TecnicoId], [Estado]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151629_AddChatSoporte'
)
BEGIN
    CREATE INDEX [IX_ChatSesiones_UsuarioId] ON [ChatSesiones] ([UsuarioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151629_AddChatSoporte'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260721151629_AddChatSoporte', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721152706_AddActivoToAreaAndUnidad'
)
BEGIN
    ALTER TABLE [Unidades] ADD [Activo] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721152706_AddActivoToAreaAndUnidad'
)
BEGIN
    ALTER TABLE [Areas] ADD [Activo] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721152706_AddActivoToAreaAndUnidad'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260721152706_AddActivoToAreaAndUnidad', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195522_AddComentariosCompromisos'
)
BEGIN
    CREATE TABLE [ComentariosCompromisos] (
        [Id] int NOT NULL IDENTITY,
        [AcuerdoReunionId] int NOT NULL,
        [Comentario] nvarchar(4000) NULL,
        [ArchivoNombre] nvarchar(255) NULL,
        [ArchivoUrl] nvarchar(1000) NULL,
        [ArchivoTamano] bigint NULL,
        [CreadoPor] nvarchar(200) NOT NULL,
        [CreadoPorRol] nvarchar(100) NULL,
        [CreadoEl] datetime2 NOT NULL,
        CONSTRAINT [PK_ComentariosCompromisos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ComentariosCompromisos_AcuerdosReunion_AcuerdoReunionId] FOREIGN KEY ([AcuerdoReunionId]) REFERENCES [AcuerdosReunion] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195522_AddComentariosCompromisos'
)
BEGIN
    CREATE INDEX [IX_ComentariosCompromisos_AcuerdoReunionId] ON [ComentariosCompromisos] ([AcuerdoReunionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195522_AddComentariosCompromisos'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260721195522_AddComentariosCompromisos', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE TABLE [ExpedienteEtapaCronogramas] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [TramiteIndex] int NOT NULL,
        [EtapaNum] nvarchar(5) NOT NULL,
        [FechaInicio] date NULL,
        [FechaFin] date NULL,
        [FechaRealFin] date NULL,
        [Responsable] nvarchar(150) NULL,
        [Observacion] nvarchar(1000) NULL,
        CONSTRAINT [PK_ExpedienteEtapaCronogramas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpedienteEtapaCronogramas_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE TABLE [Levantamientos] (
        [Id] int NOT NULL IDENTITY,
        [Institucion] nvarchar(120) NOT NULL,
        [Encargado] nvarchar(150) NOT NULL,
        [Correo] nvarchar(200) NULL,
        [Celular] nvarchar(40) NULL,
        [Estado] nvarchar(30) NOT NULL,
        [ObsEstado] nvarchar(1000) NULL,
        [MigradaSOL] bit NOT NULL,
        [Limitante] bit NOT NULL,
        [LimitanteObs] nvarchar(1000) NULL,
        [Personal] bit NOT NULL,
        [PersonalObs] nvarchar(1000) NULL,
        [RequiereAcompanamiento] bit NOT NULL,
        [Habilidad] bit NOT NULL,
        [HabilidadObs] nvarchar(1000) NULL,
        [ObsGenerales] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Levantamientos] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE TABLE [LevantamientoDocumentos] (
        [Id] int NOT NULL IDENTITY,
        [LevantamientoId] int NOT NULL,
        [Nombre] nvarchar(300) NOT NULL,
        [Tipo] nvarchar(80) NULL,
        [Url] nvarchar(600) NOT NULL,
        [FechaDocumento] date NULL,
        [FechaRegistro] datetime2 NOT NULL,
        CONSTRAINT [PK_LevantamientoDocumentos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LevantamientoDocumentos_Levantamientos_LevantamientoId] FOREIGN KEY ([LevantamientoId]) REFERENCES [Levantamientos] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE TABLE [LevantamientoEquipo] (
        [Id] int NOT NULL IDENTITY,
        [LevantamientoId] int NOT NULL,
        [Funcion] nvarchar(150) NOT NULL,
        [Nombre] nvarchar(150) NOT NULL,
        [Contacto] nvarchar(200) NULL,
        [Orden] int NOT NULL,
        CONSTRAINT [PK_LevantamientoEquipo] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LevantamientoEquipo_Levantamientos_LevantamientoId] FOREIGN KEY ([LevantamientoId]) REFERENCES [Levantamientos] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE TABLE [LevantamientoTramites] (
        [Id] int NOT NULL IDENTITY,
        [LevantamientoId] int NOT NULL,
        [NombreTramite] nvarchar(400) NOT NULL,
        [Orden] int NOT NULL,
        [ActaFirmada] bit NOT NULL,
        [RequiereMejoras] bit NOT NULL,
        [TieneInstructivo] bit NOT NULL,
        [Socializado] bit NOT NULL,
        [Observaciones] nvarchar(2000) NULL,
        CONSTRAINT [PK_LevantamientoTramites] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LevantamientoTramites_Levantamientos_LevantamientoId] FOREIGN KEY ([LevantamientoId]) REFERENCES [Levantamientos] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExpedienteEtapaCronogramas_ExpedienteId_TramiteIndex_EtapaNum] ON [ExpedienteEtapaCronogramas] ([ExpedienteId], [TramiteIndex], [EtapaNum]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE INDEX [IX_LevantamientoDocumentos_LevantamientoId] ON [LevantamientoDocumentos] ([LevantamientoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE INDEX [IX_LevantamientoEquipo_LevantamientoId] ON [LevantamientoEquipo] ([LevantamientoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE INDEX [IX_Levantamientos_CreatedAt] ON [Levantamientos] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE INDEX [IX_Levantamientos_Estado] ON [Levantamientos] ([Estado]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE INDEX [IX_Levantamientos_Institucion] ON [Levantamientos] ([Institucion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE INDEX [IX_LevantamientoTramites_LevantamientoId_Orden] ON [LevantamientoTramites] ([LevantamientoId], [Orden]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722173346_AddLevantamientosAndCronograma', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722182710_AddPlanTrabajo'
)
BEGIN
    CREATE TABLE [PlanTrabajo] (
        [Id] int NOT NULL IDENTITY,
        [IsDeleted] bit NOT NULL,
        [InstitucionId] nvarchar(120) NOT NULL,
        [Institucion] nvarchar(200) NOT NULL,
        [Anio] int NOT NULL,
        [Estado] nvarchar(30) NOT NULL,
        [Observaciones] nvarchar(2000) NULL,
        [AprobadoPorId] uniqueidentifier NULL,
        [FechaAprobacion] date NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PlanTrabajo] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722182710_AddPlanTrabajo'
)
BEGIN
    CREATE TABLE [PlanTrabajoMetas] (
        [Id] int NOT NULL IDENTITY,
        [PlanTrabajoId] int NOT NULL,
        [Orden] int NOT NULL,
        [NombreTramite] nvarchar(300) NOT NULL,
        [FechaEstimadaInicio] date NULL,
        [FechaEstimadaFin] date NULL,
        [FechaRealFin] date NULL,
        [Responsable] nvarchar(200) NULL,
        [Estado] nvarchar(30) NOT NULL,
        [Observaciones] nvarchar(2000) NULL,
        [ExpedienteId] int NULL,
        CONSTRAINT [PK_PlanTrabajoMetas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanTrabajoMetas_PlanTrabajo_PlanTrabajoId] FOREIGN KEY ([PlanTrabajoId]) REFERENCES [PlanTrabajo] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722182710_AddPlanTrabajo'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlanTrabajo_InstitucionId_Anio] ON [PlanTrabajo] ([InstitucionId], [Anio]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722182710_AddPlanTrabajo'
)
BEGIN
    CREATE INDEX [IX_PlanTrabajoMetas_PlanTrabajoId_Orden] ON [PlanTrabajoMetas] ([PlanTrabajoId], [Orden]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722182710_AddPlanTrabajo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722182710_AddPlanTrabajo', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722194801_AddMetaTramiteIndex'
)
BEGIN
    ALTER TABLE [PlanTrabajoMetas] ADD [ExpedienteTramiteIndex] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722194801_AddMetaTramiteIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722194801_AddMetaTramiteIndex', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722200241_AddMetaResponsableUsuario'
)
BEGIN
    ALTER TABLE [PlanTrabajoMetas] ADD [ResponsableId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722200241_AddMetaResponsableUsuario'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722200241_AddMetaResponsableUsuario', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722201107_AddExpedienteAnalistaUsuario'
)
BEGIN
    ALTER TABLE [Expedientes] ADD [AnalistaId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722201107_AddExpedienteAnalistaUsuario'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722201107_AddExpedienteAnalistaUsuario', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722203223_AddAsistenteInstitucionCatalogo'
)
BEGIN
    ALTER TABLE [Asistentes] ADD [InstitucionId] nvarchar(120) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722203223_AddAsistenteInstitucionCatalogo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722203223_AddAsistenteInstitucionCatalogo', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722203942_AddAcuerdoResponsableContacto'
)
BEGIN
    ALTER TABLE [AcuerdosReunion] ADD [ResponsableContactoId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722203942_AddAcuerdoResponsableContacto'
)
BEGIN
    CREATE INDEX [IX_AcuerdosReunion_ResponsableContactoId] ON [AcuerdosReunion] ([ResponsableContactoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722203942_AddAcuerdoResponsableContacto'
)
BEGIN
    ALTER TABLE [AcuerdosReunion] ADD CONSTRAINT [FK_AcuerdosReunion_Contactos_ResponsableContactoId] FOREIGN KEY ([ResponsableContactoId]) REFERENCES [Contactos] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722203942_AddAcuerdoResponsableContacto'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722203942_AddAcuerdoResponsableContacto', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723161230_AddPasswordResetTokenToUsuario'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [PasswordResetToken] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723161230_AddPasswordResetTokenToUsuario'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [PasswordResetTokenExpiration] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723161230_AddPasswordResetTokenToUsuario'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723161230_AddPasswordResetTokenToUsuario', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723170218_AddAreaToContacto'
)
BEGIN
    ALTER TABLE [Contactos] ADD [Area] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723170218_AddAreaToContacto'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723170218_AddAreaToContacto', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724153537_AddRecursosTable'
)
BEGIN
    CREATE TABLE [Recursos] (
        [Id] int NOT NULL IDENTITY,
        [IsDeleted] bit NOT NULL,
        [Titulo] nvarchar(max) NOT NULL,
        [Descripcion] nvarchar(max) NULL,
        [Categoria] nvarchar(max) NOT NULL,
        [ArchivoNombre] nvarchar(max) NOT NULL,
        [ArchivoUrl] nvarchar(max) NOT NULL,
        [ArchivoTamano] bigint NOT NULL,
        [DescargasCount] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Recursos] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724153537_AddRecursosTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260724153537_AddRecursosTable', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727195213_AddReunionExpedienteYContraparte'
)
BEGIN
    ALTER TABLE [Reuniones] ADD [ExpedienteCodigo] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727195213_AddReunionExpedienteYContraparte'
)
BEGIN
    ALTER TABLE [Reuniones] ADD [ExpedienteId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727195213_AddReunionExpedienteYContraparte'
)
BEGIN
    ALTER TABLE [Expedientes] ADD [ContraparteUsuarioId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727195213_AddReunionExpedienteYContraparte'
)
BEGIN
    ALTER TABLE [Expedientes] ADD [ContraparteUsuarioNombre] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727195213_AddReunionExpedienteYContraparte'
)
BEGIN
    ALTER TABLE [Expedientes] ADD [FechaLimiteEntrega] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727195213_AddReunionExpedienteYContraparte'
)
BEGIN
    ALTER TABLE [AcuerdosReunion] ADD [ExpedienteId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727195213_AddReunionExpedienteYContraparte'
)
BEGIN
    ALTER TABLE [AcuerdosReunion] ADD [TramiteIndex] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727195213_AddReunionExpedienteYContraparte'
)
BEGIN
    ALTER TABLE [AcuerdosReunion] ADD [TramiteNombre] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727195213_AddReunionExpedienteYContraparte'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260727195213_AddReunionExpedienteYContraparte', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731224946_AddNotasSeguimientoExpediente'
)
BEGIN
    CREATE TABLE [NotasSeguimientoExpediente] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [Texto] nvarchar(1000) NOT NULL,
        [CreadoPor] nvarchar(150) NOT NULL,
        [CreadoEl] datetime2 NOT NULL,
        CONSTRAINT [PK_NotasSeguimientoExpediente] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotasSeguimientoExpediente_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731224946_AddNotasSeguimientoExpediente'
)
BEGIN
    CREATE INDEX [IX_NotasSeguimientoExpediente_ExpedienteId_CreadoEl] ON [NotasSeguimientoExpediente] ([ExpedienteId], [CreadoEl] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731224946_AddNotasSeguimientoExpediente'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260731224946_AddNotasSeguimientoExpediente', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE TABLE [TramitesSiger] (
        [Id] int NOT NULL IDENTITY,
        [IdSiger] int NOT NULL,
        [Codigo] nvarchar(20) NOT NULL,
        [Nombre] nvarchar(600) NOT NULL,
        [Institucion] nvarchar(200) NOT NULL,
        [Sigla] nvarchar(30) NULL,
        [Dependencia] nvarchar(400) NULL,
        [Descripcion] nvarchar(4000) NULL,
        [Objetivo] nvarchar(4000) NULL,
        [DirigidoA] nvarchar(500) NULL,
        [EstadoSiger] nvarchar(30) NULL,
        [Publicado] bit NOT NULL,
        [DisponibleEnLinea] bit NOT NULL,
        [EnPlanDigitalizacion] bit NOT NULL,
        [VigenciaDocumento] nvarchar(60) NULL,
        [Temporalidad] nvarchar(60) NULL,
        [DiagramaUrl] nvarchar(600) NULL,
        [EnlacePrincipal] nvarchar(600) NULL,
        [ObservacionesDiger] nvarchar(4000) NULL,
        [FechaIngreso] datetime2 NULL,
        [UltimaModificacion] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TramitesSiger] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE TABLE [EnlacesSiger] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [Numero] int NOT NULL,
        [Url] nvarchar(600) NOT NULL,
        [Tipo] nvarchar(60) NULL,
        CONSTRAINT [PK_EnlacesSiger] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EnlacesSiger_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE TABLE [EntregablesSiger] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [Numero] int NOT NULL,
        [Entregable] nvarchar(600) NOT NULL,
        [Formato] nvarchar(60) NULL,
        [Presentacion] nvarchar(60) NULL,
        CONSTRAINT [PK_EntregablesSiger] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EntregablesSiger_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE TABLE [LugaresAtencionSiger] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [Numero] int NOT NULL,
        [Lugar] nvarchar(600) NOT NULL,
        [Ciudad] nvarchar(100) NULL,
        [Direccion] nvarchar(400) NULL,
        [Telefonos] nvarchar(200) NULL,
        CONSTRAINT [PK_LugaresAtencionSiger] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LugaresAtencionSiger_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE TABLE [PasosSiger] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [NumeroPaso] int NOT NULL,
        [Descripcion] nvarchar(2000) NOT NULL,
        [LugarDependencia] nvarchar(400) NULL,
        [SalidaResultado] nvarchar(600) NULL,
        [TiempoRegistrado] nvarchar(60) NULL,
        CONSTRAINT [PK_PasosSiger] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PasosSiger_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE TABLE [RequisitosSiger] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [Numero] int NOT NULL,
        [Requisito] nvarchar(1000) NOT NULL,
        [Tipo] nvarchar(60) NULL,
        [DocumentoSoporte] nvarchar(400) NULL,
        [Formato] nvarchar(60) NULL,
        CONSTRAINT [PK_RequisitosSiger] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RequisitosSiger_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE TABLE [TareasDigitalizacionSiger] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [NumeroTarea] int NOT NULL,
        [Descripcion] nvarchar(400) NOT NULL,
        [Estado] nvarchar(30) NULL,
        [FechaCumplimiento] datetime2 NULL,
        CONSTRAINT [PK_TareasDigitalizacionSiger] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TareasDigitalizacionSiger_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_EnlacesSiger_TramiteSigerId_Numero] ON [EnlacesSiger] ([TramiteSigerId], [Numero]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_EntregablesSiger_TramiteSigerId_Numero] ON [EntregablesSiger] ([TramiteSigerId], [Numero]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_LugaresAtencionSiger_TramiteSigerId_Numero] ON [LugaresAtencionSiger] ([TramiteSigerId], [Numero]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_PasosSiger_TramiteSigerId_NumeroPaso] ON [PasosSiger] ([TramiteSigerId], [NumeroPaso]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_RequisitosSiger_TramiteSigerId_Numero] ON [RequisitosSiger] ([TramiteSigerId], [Numero]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_TareasDigitalizacionSiger_TramiteSigerId_NumeroTarea] ON [TareasDigitalizacionSiger] ([TramiteSigerId], [NumeroTarea]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TramitesSiger_Codigo] ON [TramitesSiger] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_TramitesSiger_DisponibleEnLinea] ON [TramitesSiger] ([DisponibleEnLinea]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_TramitesSiger_EnPlanDigitalizacion] ON [TramitesSiger] ([EnPlanDigitalizacion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_TramitesSiger_EstadoSiger] ON [TramitesSiger] ([EstadoSiger]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TramitesSiger_IdSiger] ON [TramitesSiger] ([IdSiger]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_TramitesSiger_Institucion] ON [TramitesSiger] ([Institucion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_TramitesSiger_Publicado] ON [TramitesSiger] ([Publicado]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_TramitesSiger_Sigla] ON [TramitesSiger] ([Sigla]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801022203_AddInventarioSiger', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801023442_AjustarLongitudesSiger'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TramitesSiger]') AND [c].[name] = N'DirigidoA');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [TramitesSiger] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [TramitesSiger] ALTER COLUMN [DirigidoA] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801023442_AjustarLongitudesSiger'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RequisitosSiger]') AND [c].[name] = N'Requisito');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [RequisitosSiger] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [RequisitosSiger] ALTER COLUMN [Requisito] nvarchar(2000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801023442_AjustarLongitudesSiger'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RequisitosSiger]') AND [c].[name] = N'DocumentoSoporte');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [RequisitosSiger] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [RequisitosSiger] ALTER COLUMN [DocumentoSoporte] nvarchar(600) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801023442_AjustarLongitudesSiger'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PasosSiger]') AND [c].[name] = N'SalidaResultado');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [PasosSiger] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [PasosSiger] ALTER COLUMN [SalidaResultado] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801023442_AjustarLongitudesSiger'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LugaresAtencionSiger]') AND [c].[name] = N'Lugar');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [LugaresAtencionSiger] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [LugaresAtencionSiger] ALTER COLUMN [Lugar] nvarchar(1000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801023442_AjustarLongitudesSiger'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EntregablesSiger]') AND [c].[name] = N'Entregable');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [EntregablesSiger] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [EntregablesSiger] ALTER COLUMN [Entregable] nvarchar(1000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801023442_AjustarLongitudesSiger'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801023442_AjustarLongitudesSiger', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801024207_AjustarFormatoRequisito'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RequisitosSiger]') AND [c].[name] = N'Formato');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [RequisitosSiger] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [RequisitosSiger] ALTER COLUMN [Formato] nvarchar(600) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801024207_AjustarFormatoRequisito'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801024207_AjustarFormatoRequisito', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801024942_AjustarDocSoporte'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RequisitosSiger]') AND [c].[name] = N'Tipo');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [RequisitosSiger] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [RequisitosSiger] ALTER COLUMN [Tipo] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801024942_AjustarDocSoporte'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RequisitosSiger]') AND [c].[name] = N'DocumentoSoporte');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [RequisitosSiger] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [RequisitosSiger] ALTER COLUMN [DocumentoSoporte] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801024942_AjustarDocSoporte'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801024942_AjustarDocSoporte', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LugaresAtencionSiger]') AND [c].[name] = N'Telefonos');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [LugaresAtencionSiger] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [LugaresAtencionSiger] ALTER COLUMN [Telefonos] nvarchar(400) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    DECLARE @var12 sysname;
    SELECT @var12 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LugaresAtencionSiger]') AND [c].[name] = N'Direccion');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [LugaresAtencionSiger] DROP CONSTRAINT [' + @var12 + '];');
    ALTER TABLE [LugaresAtencionSiger] ALTER COLUMN [Direccion] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    DECLARE @var13 sysname;
    SELECT @var13 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LugaresAtencionSiger]') AND [c].[name] = N'Ciudad');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [LugaresAtencionSiger] DROP CONSTRAINT [' + @var13 + '];');
    ALTER TABLE [LugaresAtencionSiger] ALTER COLUMN [Ciudad] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    DECLARE @var14 sysname;
    SELECT @var14 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EntregablesSiger]') AND [c].[name] = N'Presentacion');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [EntregablesSiger] DROP CONSTRAINT [' + @var14 + '];');
    ALTER TABLE [EntregablesSiger] ALTER COLUMN [Presentacion] nvarchar(600) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    DECLARE @var15 sysname;
    SELECT @var15 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EntregablesSiger]') AND [c].[name] = N'Formato');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [EntregablesSiger] DROP CONSTRAINT [' + @var15 + '];');
    ALTER TABLE [EntregablesSiger] ALTER COLUMN [Formato] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    DECLARE @var16 sysname;
    SELECT @var16 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EnlacesSiger]') AND [c].[name] = N'Url');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [EnlacesSiger] DROP CONSTRAINT [' + @var16 + '];');
    ALTER TABLE [EnlacesSiger] ALTER COLUMN [Url] nvarchar(1000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    DECLARE @var17 sysname;
    SELECT @var17 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EnlacesSiger]') AND [c].[name] = N'Tipo');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [EnlacesSiger] DROP CONSTRAINT [' + @var17 + '];');
    ALTER TABLE [EnlacesSiger] ALTER COLUMN [Tipo] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801030023_AmpliarCamposSiger', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801031904_AmpliarCamposSiger2'
)
BEGIN
    DECLARE @var18 sysname;
    SELECT @var18 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TareasDigitalizacionSiger]') AND [c].[name] = N'Estado');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [TareasDigitalizacionSiger] DROP CONSTRAINT [' + @var18 + '];');
    ALTER TABLE [TareasDigitalizacionSiger] ALTER COLUMN [Estado] nvarchar(60) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801031904_AmpliarCamposSiger2'
)
BEGIN
    DECLARE @var19 sysname;
    SELECT @var19 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TareasDigitalizacionSiger]') AND [c].[name] = N'Descripcion');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [TareasDigitalizacionSiger] DROP CONSTRAINT [' + @var19 + '];');
    ALTER TABLE [TareasDigitalizacionSiger] ALTER COLUMN [Descripcion] nvarchar(1000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801031904_AmpliarCamposSiger2'
)
BEGIN
    DECLARE @var20 sysname;
    SELECT @var20 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LugaresAtencionSiger]') AND [c].[name] = N'Telefonos');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [LugaresAtencionSiger] DROP CONSTRAINT [' + @var20 + '];');
    ALTER TABLE [LugaresAtencionSiger] ALTER COLUMN [Telefonos] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801031904_AmpliarCamposSiger2'
)
BEGIN
    DECLARE @var21 sysname;
    SELECT @var21 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LugaresAtencionSiger]') AND [c].[name] = N'Direccion');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [LugaresAtencionSiger] DROP CONSTRAINT [' + @var21 + '];');
    ALTER TABLE [LugaresAtencionSiger] ALTER COLUMN [Direccion] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801031904_AmpliarCamposSiger2'
)
BEGIN
    DECLARE @var22 sysname;
    SELECT @var22 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LugaresAtencionSiger]') AND [c].[name] = N'Ciudad');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [LugaresAtencionSiger] DROP CONSTRAINT [' + @var22 + '];');
    ALTER TABLE [LugaresAtencionSiger] ALTER COLUMN [Ciudad] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801031904_AmpliarCamposSiger2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801031904_AmpliarCamposSiger2', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801041054_LinkSigerInstitucion'
)
BEGIN
    ALTER TABLE [TramitesSiger] ADD [InstitucionId] nvarchar(120) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801041054_LinkSigerInstitucion'
)
BEGIN

                    INSERT INTO [Instituciones] ([Id], [Nombre], [Activo], [CreatedAt])
                    SELECT
                        norm.NormSigla,
                        norm.Institucion,
                        1,
                        GETUTCDATE()
                    FROM (
                        SELECT DISTINCT
                            UPPER(TRANSLATE(ts.[Sigla], N'áéíóúÁÉÍÓÚñÑ', N'aeiouAEIOUnN')) AS NormSigla,
                            ts.[Institucion]
                        FROM [TramitesSiger] ts
                        WHERE ts.[Sigla] IS NOT NULL AND ts.[Sigla] <> ''
                    ) norm
                    WHERE norm.NormSigla NOT IN (SELECT [Id] FROM [Instituciones])
                      AND norm.NormSigla IS NOT NULL;
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801041054_LinkSigerInstitucion'
)
BEGIN

                    UPDATE [TramitesSiger]
                    SET [InstitucionId] = UPPER(TRANSLATE([Sigla], N'áéíóúÁÉÍÓÚñÑ', N'aeiouAEIOUnN'))
                    WHERE [Sigla] IS NOT NULL AND [Sigla] <> ''
                      AND UPPER(TRANSLATE([Sigla], N'áéíóúÁÉÍÓÚñÑ', N'aeiouAEIOUnN'))
                          IN (SELECT [Id] FROM [Instituciones]);
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801041054_LinkSigerInstitucion'
)
BEGIN
    CREATE INDEX [IX_TramitesSiger_InstitucionId] ON [TramitesSiger] ([InstitucionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801041054_LinkSigerInstitucion'
)
BEGIN
    ALTER TABLE [TramitesSiger] ADD CONSTRAINT [FK_TramitesSiger_Instituciones_InstitucionId] FOREIGN KEY ([InstitucionId]) REFERENCES [Instituciones] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801041054_LinkSigerInstitucion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801041054_LinkSigerInstitucion', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803023243_LinkExpedienteTramiteSiger'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [TramiteSigerId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803023243_LinkExpedienteTramiteSiger'
)
BEGIN
    CREATE INDEX [IX_ExpedienteTramites_TramiteSigerId] ON [ExpedienteTramites] ([TramiteSigerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803023243_LinkExpedienteTramiteSiger'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD CONSTRAINT [FK_ExpedienteTramites_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803023243_LinkExpedienteTramiteSiger'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260803023243_LinkExpedienteTramiteSiger', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803204352_AddTramiteFechaCreacionYEstado'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [EstadoTramite] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803204352_AddTramiteFechaCreacionYEstado'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [FechaCreacion] date NOT NULL DEFAULT '0001-01-01';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803204352_AddTramiteFechaCreacionYEstado'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260803204352_AddTramiteFechaCreacionYEstado', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805150429_AuditoriaExpedientes'
)
BEGIN
    ALTER TABLE [Expedientes] ADD [FechaHoraValidacionDiger] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805150429_AuditoriaExpedientes'
)
BEGIN
    ALTER TABLE [Expedientes] ADD [FechaHoraValidacionInst] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805150429_AuditoriaExpedientes'
)
BEGIN
    ALTER TABLE [Expedientes] ADD [ValidadoDigerUsuarioId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805150429_AuditoriaExpedientes'
)
BEGIN
    ALTER TABLE [Expedientes] ADD [ValidadoInstUsuarioId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805150429_AuditoriaExpedientes'
)
BEGIN
    CREATE TABLE [BitacoraExpediente] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [Tipo] nvarchar(30) NOT NULL,
        [Detalle] nvarchar(500) NOT NULL,
        [Actor] nvarchar(150) NOT NULL,
        [Fecha] datetime2 NOT NULL,
        CONSTRAINT [PK_BitacoraExpediente] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BitacoraExpediente_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805150429_AuditoriaExpedientes'
)
BEGIN
    CREATE INDEX [IX_BitacoraExpediente_ExpedienteId_Fecha] ON [BitacoraExpediente] ([ExpedienteId], [Fecha] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805150429_AuditoriaExpedientes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260805150429_AuditoriaExpedientes', N'9.0.0');
END;

COMMIT;
GO

