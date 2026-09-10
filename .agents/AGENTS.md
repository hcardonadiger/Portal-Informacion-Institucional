# Reglas del Proyecto (Antigravity Rules)

## Registro Automático de Cambios en Base de Datos (SQL & Migraciones)
- **OBLIGATORIO Y PERMANENTE**: Cada vez que se cree, modifique o aplique un cambio en la base de datos (migración EF Core, alter de tablas, nuevos campos, procedimientos almacenados o scripts de inicialización):
  1. Guardar y anexar el script SQL correspondiente en el archivo [script_cambios_bd.md](file:///c:/DIGER/Aplicativos/Diger.TramitesEstado/Contextos/script_cambios_bd.md).
  2. Cada entrada en `script_cambios_bd.md` debe incluir:
     - Fecha y breve descripción del cambio.
     - Nombre de la migración de EF Core (si aplica).
     - El script SQL directo (`ALTER TABLE`, `CREATE INDEX`, `ADD COLUMN`, etc.) ejecutable en SQL Server para que otros desarrolladores puedan aplicarlo manualmente en sus entornos.
