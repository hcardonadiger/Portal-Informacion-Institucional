-- ============================================================
-- Migración: AddChatSoporte
-- Fecha:     2026-07-21
-- Descripción: Tablas para el módulo de chat de soporte en
--              tiempo real (SignalR). Incluye sesiones y
--              mensajes, vinculables a temas y tickets.
-- Idempotente: solo ejecuta si la migración no está aplicada.
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151629_AddChatSoporte'
)
BEGIN

    BEGIN TRANSACTION;

    -- ── Sesiones de chat ───────────────────────────────────────
    CREATE TABLE [ChatSesiones] (
        [Id]           int              NOT NULL IDENTITY,
        [UsuarioId]    uniqueidentifier NOT NULL,
        [UsuarioNombre] nvarchar(120)   NOT NULL,
        [TecnicoId]    uniqueidentifier NULL,
        [TecnicoNombre] nvarchar(120)   NULL,
        [TemaId]       int              NULL,
        [TemaNombre]   nvarchar(80)     NULL,
        [TicketId]     int              NULL,
        [Estado]       nvarchar(20)     NOT NULL,
        [Calificacion] tinyint          NULL,
        [Inicio]       datetime2        NOT NULL,
        [Cierre]       datetime2        NULL,
        CONSTRAINT [PK_ChatSesiones] PRIMARY KEY ([Id])
    );

    -- ── Mensajes de cada sesión ────────────────────────────────
    CREATE TABLE [ChatMensajes] (
        [Id]           int           NOT NULL IDENTITY,
        [SesionId]     int           NOT NULL,
        [Texto]        nvarchar(2000) NOT NULL,
        [EsDelTecnico] bit           NOT NULL,
        [EsSistema]    bit           NOT NULL,
        [AutorNombre]  nvarchar(120) NOT NULL,
        [Enviado]      datetime2     NOT NULL,
        [Leido]        bit           NOT NULL,
        CONSTRAINT [PK_ChatMensajes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatMensajes_ChatSesiones_SesionId]
            FOREIGN KEY ([SesionId]) REFERENCES [ChatSesiones] ([Id])
            ON DELETE CASCADE
    );

    -- ── Índices ────────────────────────────────────────────────
    -- Mensajes no leídos por sesión (consulta de badge y marcar leídos)
    CREATE INDEX [IX_ChatMensajes_SesionId_Leido]
        ON [ChatMensajes] ([SesionId], [Leido]);

    -- Historial cronológico de sesiones
    CREATE INDEX [IX_ChatSesiones_Inicio]
        ON [ChatSesiones] ([Inicio]);

    -- Sesiones activas por técnico (cola del panel /Chat)
    CREATE INDEX [IX_ChatSesiones_TecnicoId_Estado]
        ON [ChatSesiones] ([TecnicoId], [Estado]);

    -- Sesión activa del usuario (widget flotante)
    CREATE INDEX [IX_ChatSesiones_UsuarioId]
        ON [ChatSesiones] ([UsuarioId]);

    -- ── Registro en historial de migraciones EF ───────────────
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260721151629_AddChatSoporte', N'9.0.0');

    COMMIT;

END
GO
