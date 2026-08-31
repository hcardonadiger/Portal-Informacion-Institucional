/*
================================================================================
  Vínculo opcional entre proyectos y tickets de soporte.
  Migración 20260831174631_VincularTicketsAProyectos
================================================================================

  CÓMO SE USA
  -----------
  Ejecútelo una vez contra la base del ambiente. No modifica ni borra datos: solo
  crea una tabla y la anota en el historial de EF. Es idempotente.

      sqlcmd -S <servidor> -d <base> -U <usuario> -P <clave> -C -I ^
             -i database\vinculos_proyecto_ticket.sql

  El -I (QUOTED_IDENTIFIER ON) es obligatorio: las tablas del portal llevan
  índices filtrados y sin él SQL Server rechaza la escritura con el error 1934.

  REQUISITO PREVIO
  ----------------
  Aplique antes vinculos_proyecto_reunion_expediente.sql. No es que este guion
  dependa de aquellas tablas —no las toca— sino que las tres son la misma
  funcionalidad y saltarse una deja la pestaña de vínculos a medias.

  QUÉ HACE
  --------
  Crea ProyectoTickets. Es la tercera pata de la misma relación: OPCIONAL POR LOS
  DOS LADOS y de muchos a muchos. Un ticket puede no pertenecer a ningún proyecto
  —la mayoría no pertenece— y puede alimentar a varios.

  NO REEMPLAZA A Tickets.ExpedienteId
  -----------------------------------
  Aquella columna dice de qué EXPEDIENTE habla el ticket; esta tabla dice a qué
  PROYECTO contribuye. Un ticket sobre el expediente de una institución puede
  alimentar el proyecto transversal sin que las dos cosas sean la misma.

  EL DESAJUSTE DE ALCANCE, QUE HAY QUE CONOCER
  --------------------------------------------
  Igual que con reuniones y expedientes: el vínculo se ancla en el PROYECTO, cuya
  institución es la que EJECUTA, mientras que la del ticket es la que reporta. El
  vínculo cruza instituciones por construcción, así que ver el vínculo no implica
  poder abrir el ticket. La ficha muestra lo que la persona alcanza y DICE
  CUÁNTOS quedan fuera; un conteo no revela ni número, ni título, ni institución.

  QUIÉN PUEDE VINCULAR
  --------------------
  Se exige Proyectos.Editar —no Tickets.Editar— desde los dos extremos: la acción
  escribe en la ficha y en la bitácora del proyecto. Con la clave de tickets,
  cualquiera de soporte podría escribir en la bitácora de cualquier proyecto.
  Este guion no otorga ni cambia permisos; el catálogo se sincroniza solo al
  arrancar la aplicación.

  BORRADO EN CASCADA
  ------------------
  Borrar el proyecto o el ticket se lleva el vínculo. Tickets es borrado lógico
  (IsDeleted), así que la cascada solo entra si alguien lo borra de verdad en la
  base — y ahí el vínculo colgando no sirve de nada. Quitar el vínculo desde la
  ficha o desde el ticket no toca ninguno de los dos extremos.

================================================================================
*/

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

-- ── Guardas previas ────────────────────────────────────────────────────────
IF OBJECT_ID('__EFMigrationsHistory') IS NULL
BEGIN
    RAISERROR('Esta base no tiene __EFMigrationsHistory: no es una base del portal creada por EF. No se hizo nada.', 16, 1);
    RETURN;
END

IF OBJECT_ID('Proyectos') IS NULL OR OBJECT_ID('Tickets') IS NULL
BEGIN
    RAISERROR('Faltan Proyectos o Tickets. No se hizo nada.', 16, 1);
    RETURN;
END

IF EXISTS (SELECT 1 FROM [__EFMigrationsHistory]
           WHERE [MigrationId] = N'20260831174631_VincularTicketsAProyectos')
BEGIN
    PRINT 'La migración ya estaba aplicada en esta base. No se hizo nada.';
    RETURN;
END

-- ── Migración (guion idempotente generado por EF Core) ─────────────────────
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831174631_VincularTicketsAProyectos'
)
BEGIN
    CREATE TABLE [ProyectoTickets] (
        [Id] int NOT NULL IDENTITY,
        [ProyectoId] int NOT NULL,
        [TicketId] int NOT NULL,
        [Nota] nvarchar(400) NULL,
        [VinculadoPor] nvarchar(200) NOT NULL,
        [VinculadoEn] datetime2 NOT NULL,
        CONSTRAINT [PK_ProyectoTickets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProyectoTickets_Proyectos_ProyectoId] FOREIGN KEY ([ProyectoId]) REFERENCES [Proyectos] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProyectoTickets_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831174631_VincularTicketsAProyectos'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProyectoTickets_ProyectoId_TicketId] ON [ProyectoTickets] ([ProyectoId], [TicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831174631_VincularTicketsAProyectos'
)
BEGIN
    CREATE INDEX [IX_ProyectoTickets_TicketId] ON [ProyectoTickets] ([TicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831174631_VincularTicketsAProyectos'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260831174631_VincularTicketsAProyectos', N'9.0.0');
END;

COMMIT;

-- ── Comprobación ───────────────────────────────────────────────────────────
SELECT 'ProyectoTickets: ' +
       CASE WHEN OBJECT_ID('ProyectoTickets') IS NOT NULL THEN 'creada' ELSE 'NO' END
     + ' | índice único: ' + CAST((SELECT COUNT(*) FROM sys.indexes
         WHERE object_id = OBJECT_ID('ProyectoTickets') AND is_unique = 1 AND type > 0) AS varchar)
     + ' | FK: ' + CAST((SELECT COUNT(*) FROM sys.foreign_keys
         WHERE parent_object_id = OBJECT_ID('ProyectoTickets')) AS varchar)
       AS Resultado;

PRINT '';
PRINT '*** Vinculo de tickets con proyectos aplicado. ***';
