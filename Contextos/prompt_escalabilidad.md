# Contexto y Prompt de Implementación: Escalabilidad Multi-Institución y Jerarquía

## Objetivo Principal
El objetivo de esta fase de desarrollo es refactorizar y escalar el aplicativo "DIGER Trámites Estado" para soportar múltiples instituciones, con una estructura jerárquica de Áreas y Unidades. El sistema debe garantizar que los usuarios solo accedan a la información correspondiente a su nivel de jerarquía y permisos, manteniendo el código limpio, optimizado y utilizando las mejores prácticas de .NET y Entity Framework Core (EF Core).

---

## 1. Nueva Estructura de Base de Datos ✅ [COMPLETADO EN DB SCRIPT Y DOMAIN]

> [!CAUTION]
> **TAREA INICIAL PENDIENTE (Bugs de Compilación):** ✅ [COMPLETADO - 0 Errores]
> Todos los errores de compilación en `Application`, `Web`, y `Tests` derivados del cambio de tipos (`InstitucionId` a `string`, `UsuarioId` a `Guid`) han sido corregidos de manera exitosa. El proyecto compila correctamente.

> [!IMPORTANT]
> **TAREA DE SEGURIDAD PENDIENTE (Global Query Filters):**
> Tampoco se implementaron todavía los filtros globales de EF Core en `AppDbContext.cs`. Debes implementarlos para que el RLS (Row-Level Security) evalúe la jerarquía Institución/Área/Unidad.

Se deben crear nuevas tablas para soportar la jerarquía y la asignación de usuarios. **Todas las tablas (excepto `Prefijos`) deben incluir los siguientes campos de Auditoría:**
*   `UsuarioCreo` (NVARCHAR)
*   `UsuarioModifico` (NVARCHAR, nullable)
*   `FechaCreacion` (DATETIME2)
*   `FechaModificacion` (DATETIME2, nullable)

### 1.1 Tablas de Jerarquía Organizacional
Para mantener la integridad referencial, estas tablas estarán relacionadas mediante Foreign Keys (Claves Foráneas).
*   **`Institucion`**:
    *   Campos: `Id` (VARCHAR PK), `Nombre`, `Descripcion`, `NombreCorto`, `LogoUrl`, Información extra.
*   **`Area`**:
    *   Campos: `Id` (VARCHAR PK), `InstitucionId` (FK a Institucion), `Nombre`, `Descripcion`, `NombreCorto`, `LogoUrl` (Nullable, si es nulo hereda el de Institución en UI).
*   **`Unidad`**:
    *   Campos: `Id` (VARCHAR PK), `AreaId` (FK a Area), `Nombre`, `Descripcion`, `NombreCorto`, `LogoUrl` (Nullable, si es nulo hereda el del Área/Institución).

### 1.2 Tablas de Usuarios y Seguridad
*   **`Usuarios`**:
    *   Campos: `Id` (UNIQUEIDENTIFIER o VARCHAR PK), `Nombre`, `Correo`, `Telefono`, `ContrasenaHash`, etc.
*   **`AsignacionesUsuario` (Reemplaza a UsuariosIAU)**:
    *   Esta tabla conecta al usuario con su ubicación en la estructura y define su rol explícito.
    *   Campos: `UsuarioId` (FK), `InstitucionId` (FK), `AreaId` (FK, nullable), `UnidadId` (FK, nullable), `Rol` (VARCHAR o Enum).
    *   **Roles Permitidos**: `JefeInstitucion`, `JefeArea`, `JefeUnidad`, `Empleado`, `Consultor`.

### 1.3 Tabla de Movimientos y Generación de Códigos
*   **`Movimientos`**:
    *   Campos: `Id` (VARCHAR PK), `Nombre`, `Descripcion`.
*   **`Prefijos`** (NO lleva campos de auditoría):
    *   Campos: `PrefijoInstitucion` (VARCHAR), `PrefijoMovimiento` (VARCHAR), `UltimoValor` (INT), `UltimoCodigo` (VARCHAR).
    *   *Lógica*: Se creará un **Stored Procedure** en SQL Server que recibirá la Institución y el Movimiento, incrementará `UltimoValor` de forma segura (para evitar colisiones) y retornará el nuevo código generado (Ej: `DIGER-TIC-150`).

### 1.4 Modificación a Tablas Existentes
*   Tablas transaccionales como `Reuniones`, `Tickets`, `Tramite`, `Contactos`, `Levantamientos`, etc., deben ser modificadas para incluir las FKs: `InstitucionId`, `AreaId`, `UnidadId`.

## 2. Reglas de Negocio y Permisos (Roles) ✅ [COMPLETADO]

El acceso a la información estará dictado por el campo `Rol` en la tabla `AsignacionesUsuario`:

1.  **Jefe de Institución**: Puede ver y gestionar toda la información de todas las Áreas y Unidades de su Institución.
2.  **Jefe de Área**: Puede ver y gestionar toda la información de todas las Unidades que pertenecen a su Área.
3.  **Jefe de Unidad**: Gestiona a los empleados y datos de su Unidad específica.
4.  **Empleado de Unidad**: Visualiza y gestiona únicamente sus propios datos o los datos asignados explícitamente a su Unidad (dependiendo de la entidad).
5.  **Consultor**: Rol de solo lectura (Read-Only). Podrá ingresar a pantallas específicas según sus permisos, pero no tendrá acceso a botones de guardado, edición o eliminación.

---

## 3. Estado de Navegación y UI (Combobox de Cambio de Contexto)

Para los usuarios con rol de Jefe (`JefeInstitucion` y `JefeArea`), el sistema debe proveer una funcionalidad en la Interfaz de Usuario (ej. Navbar) que les permita cambiar su "Contexto Visual":
*   **JefeInstitucion** tendrá un Combobox para seleccionar qué `Area` y/o `Unidad` desea revisar.
*   **JefeArea** tendrá un Combobox para seleccionar qué `Unidad` desea revisar.
*   **Gestión del Estado**: La selección de este Combobox **DEBE** guardarse en los **Claims de Autenticación** del usuario o refrescar la sesión, de forma que el backend sepa en todo momento qué contexto de datos debe devolver.
*   **Creación de Datos**: Cuando un usuario crea un nuevo registro, se le asignará automáticamente la Institución, Área y Unidad activa. Si el usuario es Jefe y tiene seleccionado un contexto específico en su Combobox, el nuevo registro se guardará con la Institución/Área/Unidad de ese contexto seleccionado.

---

## 4. Instrucciones Técnicas y Patrones para la IA Implementadora

Al implementar estos cambios en el código C#, se DEBEN seguir estrictamente las siguientes directrices:

1.  **Auditoría Automatizada en EF Core**:
    *   No llenar los campos de auditoría (`UsuarioCreo`, `FechaCreacion`, etc.) manualmente en los controladores o servicios.
    *   **Obligatorio:** Sobrescribir el método `SaveChangesAsync()` en el `DbContext`. Se debe usar el `IHttpContextAccessor` para obtener el ID del usuario actual de los claims, e inyectar las fechas en UTC para las entidades que implementen una interfaz (ej. `IAuditableEntity`).

2.  **Seguridad por Nivel de Fila (Row-Level Security)**:
    *   > [!IMPORTANT]
> **TAREA DE SEGURIDAD PENDIENTE (Global Query Filters):**
> [COMPLETADO - 06/07/2026] Se implementaron los filtros globales en AppDbContext usando `ActiveInstitucionId`, `ActiveAreaId` y `ActiveUnidadId` evaluando la jerarquía según el Rol. También se agregó un Combobox en el Layout para cambiar de contexto leyendo los Claims de la cookie.

3.  **Stored Procedure para Códigos Concurrentes**:
    *   Crear un script SQL (migration) que defina el `SP_GenerarCodigoMovimiento`. Usar transacciones y `UPDLOCK` a nivel de base de datos para asegurar que dos peticiones simultáneas no obtengan el mismo código de la tabla `Prefijos`.

4.  **Inyección Automática en Inserciones**:
    *   Similar a la auditoría, cuando se agregue una entidad al contexto (`EntityState.Added`), el sistema debe asignar automáticamente la `InstitucionId`, `AreaId` y `UnidadId` basándose en la configuración activa del usuario (su asignación real o la de su combobox si es jefe).

5.  **Rol Consultor (Read-Only)**:
    *   Implementar directivas en Razor Pages/Views (ej. un TagHelper o comprobación de claims) para ocultar controles de edición si el `Rol == Consultor`. A nivel de backend, usar atributos de autorización o validación en los `OnPost` para rechazar operaciones de mutación de este rol.
