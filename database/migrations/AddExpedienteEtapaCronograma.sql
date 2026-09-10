-- Migración: cronograma de actividades por etapa (fechas comprometidas por trámite/expediente)
-- Idempotente: se puede ejecutar múltiples veces sin efecto secundario.
IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE MigrationId = '20260722100000_AddExpedienteEtapaCronograma'
)
BEGIN
    CREATE TABLE [ExpedienteEtapaCronogramas] (
        [Id]           INT            NOT NULL IDENTITY(1,1),
        [ExpedienteId] INT            NOT NULL,
        [TramiteIndex] INT            NOT NULL,
        [EtapaNum]     NVARCHAR(5)    NOT NULL,
        [FechaInicio]  DATE           NULL,
        [FechaFin]     DATE           NULL,
        [FechaRealFin] DATE           NULL,
        [Responsable]  NVARCHAR(150)  NULL,
        [Observacion]  NVARCHAR(1000) NULL,
        CONSTRAINT [PK_ExpedienteEtapaCronogramas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpedienteEtapaCronogramas_Expedientes]
            FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes]([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_EtapaCronogramas_Exp_Tram_Eta]
        ON [ExpedienteEtapaCronogramas] ([ExpedienteId], [TramiteIndex], [EtapaNum]);

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260722100000_AddExpedienteEtapaCronograma', '9.0.0');
END
