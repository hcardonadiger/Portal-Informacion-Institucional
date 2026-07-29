# Guía de Demo — Portal DIGER Trámites Estado
*(Preparada el 28-Jul-2026. Datos cargados por el script del 2026-07-28 en [script_cambios_bd.md](script_cambios_bd.md).)*

Hoja de referencia para la demostración: con qué cuenta entrar, qué muestra cada una y
qué NO esperar del sistema.

---

## 1. Credenciales

**Contraseña de todos los usuarios de demo: `Demo#2026`**
(La cuenta de administrador es la preexistente y conserva su contraseña: `Admin#2026`.)

| Usuario | Correo | Rol | Alcance |
|---|---|---|---|
| Admin Global | `admin@diger.gob.hn` | Administrador | Global — ve todo |
| **Ana Maradiaga** | `soporte.plataforma@diger.gob.hn` | Empleado *(soporte)* | DIGER, **sin unidad** |
| **Carlos Zelaya** | `mesa.ayuda@diger.gob.hn` | Empleado *(soporte)* | DIGER, **sin unidad** |
| Marlon Discua | `jefe.ditra@diger.gob.hn` | JefeUnidad | DIGER / Gobierno Digital / Digitalización de Trámites |
| Karla Núñez | `jefe.insitu@consucoop.gob.hn` | JefeUnidad | CONSUCOOP / Supervisión y Vigilancia / Supervisión In Situ |
| Óscar Banegas | `oscar.banegas@diger.gob.hn` | Empleado | DIGER / GOBDIG / DITRA |
| Lourdes Fajardo | `lourdes.fajardo@diger.gob.hn` | Empleado | DIGER / GOBDIG / DITRA |
| René Portillo | `rene.portillo@consucoop.gob.hn` | Empleado | CONSUCOOP / CSC-SUPV / CSC-SUPV-IS |
| Dilcia Herrera | `dilcia.herrera@consucoop.gob.hn` | Empleado | CONSUCOOP / CSC-SUPV / CSC-SUPV-IS |

---

## 2. Estructura organizacional

```
DIGER
└── GOBDIG · Gobierno Digital
    └── DITRA · Digitalización de Trámites
        ├── Marlon Discua   (JefeUnidad)
        ├── Óscar Banegas   (Empleado)
        └── Lourdes Fajardo (Empleado)

CONSUCOOP
├── CSC-SUPV · Supervisión y Vigilancia
│   ├── CSC-SUPV-IS · Supervisión In Situ
│   │   ├── Karla Núñez    (JefeUnidad)
│   │   ├── René Portillo  (Empleado)
│   │   └── Dilcia Herrera (Empleado)
│   └── CSC-SUPV-ES · Supervisión Extra Situ
└── CSC-REG · Registro y Autorizaciones
    ├── CSC-REG-COOP · Registro de Cooperativas
    └── CSC-REG-AUT  · Autorizaciones y Licencias
```

---

## 3. Los dos técnicos de soporte

Al ser rol `Empleado` sin jefatura, el portal los trata como **"técnico restringido"**: en
`/Tickets` no ven la lista completa, solo alternan entre dos vistas — **"Sus temas"** (los que
puede tomar) y **"Sus tickets"** (los que ya tiene asignados).

| | **Ana Maradiaga** | **Carlos Zelaya** |
|---|---|---|
| Perfil | Soporte de Plataforma | Mesa de Ayuda |
| Categorías | Plataforma SOL | Accesos y permisos · Formación y otros |
| Temas | Error en plataforma (SLA 8 h) · Configuración (48 h) · Datos (72 h) | Acceso (24 h) · Capacitación (72 h) · Otro (72 h) |
| Tickets que ve | 6 | 6 |

Los dos conjuntos son **disjuntos** y entre ambos cubren los 12 tickets. Es la forma más
directa de mostrar la segmentación por categoría: entrar con uno, luego con el otro, y ver
que las listas no se cruzan.

---

## 4. Guion sugerido

1. **Admin Global** → `/Usuarios` y `/Accesos`: el organigrama completo y la matriz rol × módulo.
2. **Admin Global** → `/Tableros`: KPIs sobre los 12 tickets, incluidos los vencidos de SLA.
3. **Ana Maradiaga** → `/Tickets`, vista *Sus temas*: solo aparecen los de Plataforma SOL.
   `TCK-2026-0007` está **vencido** (SLA de 8 h, abierto hace 3 días).
4. **Carlos Zelaya** → `/Tickets`: lista completamente distinta. `TCK-2026-0009` es **Crítica**
   y está vencida.
5. **Karla Núñez** (CONSUCOOP) vs. **Marlon Discua** (DIGER) → mismo módulo, datos distintos:
   demuestra el aislamiento institucional.
6. Filtro **"Solo vencidos"** en `/Tickets` para mostrar el control de SLA.

---

## 5. Lo que NO hay que mostrar (limitaciones conocidas)

- **Los técnicos de soporte ven los mismos módulos que cualquier empleado**
  (Tableros, Calendario, Expedientes, Reuniones, Contactos, Tickets). La tabla
  `RolModuloAccesos` asigna módulos **por rol**, y soporte comparte el rol `Empleado` con los
  empleados de unidad. Un menú propio para soporte requeriría un rol `Mantenimiento` nuevo.

- **Los empleados de unidad (Óscar, Lourdes, René, Dilcia) ven 0 tickets.** No es un error de
  los datos: el filtro global de `Ticket` para el rol `Empleado` es `t.UnidadId == _activeUnidad`,
  y `CrearTicketCommand` nunca asigna unidad a los tickets. Por eso los técnicos de soporte se
  crearon **sin unidad**. Úselos para mostrar Expedientes y Reuniones, no Tickets.

- **Usuarios preexistentes con nombres engañosos:** *"Jefe DIGER"*, *"Jefe TIC"* y *"Jefe DEV"*
  tienen en realidad rol `Empleado`; *"Consultor DIGER"* y *"Empleado DEV"* no tienen ninguna
  asignación y no pueden entrar a nada. Conviene no abrirlos durante la demostración.

---

## 6. Restablecer los datos

El script del 2026-07-28 en `script_cambios_bd.md` es **idempotente**: se puede volver a
ejecutar sin duplicar nada. Si necesita rehacer el estado desde cero, ejecútelo de nuevo.
