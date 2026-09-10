# Carga de proyectos desde Excel

Dos pasos: se genera una plantilla, la llenan las áreas, y de vuelta se convierte en un script SQL
que se revisa antes de correr contra la base.

```
Nueva-Plantilla-Proyectos.ps1  →  Plantilla_Importacion_Proyectos.xlsx
                                          ↓  (la llena el área)
Generar-SQL-Proyectos.ps1      →  ../carga_proyectos_<fecha>.sql
                                          ↓  (se revisa y se corre)
                                        base
```

## 1. Generar la plantilla

```powershell
.\Nueva-Plantilla-Proyectos.ps1
```

Lee los catálogos de la base y los deja como listas desplegables: instituciones, áreas, unidades y
los correos de los usuarios activos. **Regenérela antes de repartirla** — una plantilla vieja trae
usuarios que ya no están y produce filas que el importador rechaza.

Con autenticación SQL en vez de integrada:

```powershell
.\Nueva-Plantilla-Proyectos.ps1 -Usuario sa -Clave '***'
```

## 2. Convertir la plantilla llena en SQL

```powershell
.\Generar-SQL-Proyectos.ps1 -Archivo 'C:\ruta\llenada.xlsx' -Actor 'Su Nombre'
```

Valida primero. Si algo no cuadra **no escribe el .sql** y lista hoja, fila, columna y problema:

```
Hoja      Fila Columna       Problema
Proyectos    5 Estado        «EnMarcha» no es un valor válido. Use uno de: Planificado, ...
Proyectos    6 Ref           «P1» está repetida (ya se usó en la fila 5)
Hitos        5 Ref proyecto  «P9» no aparece en la hoja Proyectos
```

Revise el `.sql` y córralo:

```powershell
sqlcmd -S localhost -d DigerTramitesEstado -i ..\carga_proyectos_20260824_1543.sql
```

## Cosas que conviene saber

**La columna `Ref` no existe en la base.** Es un identificador que inventa quien llena la plantilla
(P1, P2…) y sirve solo para amarrar las hojas Hitos, Interesados y Riesgos con su proyecto. El
código real (`PRY-2026-27`) lo asigna el script, correlativo por año, igual que el portal.

**La fila de ejemplo lleva `Ref = EJEMPLO`** y el generador la ignora. No hace falta borrarla.

**La plantilla va protegida.** Editable solo la zona de captura; los encabezados quedan bloqueados
—el importador busca las columnas por nombre y por posición— y la estructura del libro también, que
es lo único que impide borrar la hoja Catalogos y con ella todas las listas desplegables. Sí se
puede agregar y borrar filas, ordenar, filtrar y ajustar anchos.

La protección **no lleva contraseña**: está para evitar el accidente, no para trabar a nadie. Quien
necesite reacomodar algo entra por Revisar > Desproteger hoja. Con `-ClaveProteccion '<clave>'` se
le pone una, pero entonces hay que acordarse de ella — una plantilla protegida con clave olvidada
no se recupera.

**Es idempotente.** Los proyectos se reconocen por `Nombre`, los hitos e interesados por proyecto +
nombre, y los riesgos por proyecto + descripción. Correr la misma carga dos veces actualiza; no
duplica. Lo que sí queda dos veces es la entrada en `BitacoraProyecto`, y es a propósito: son dos
cargas distintas y ambas ocurrieron.

**Los interesados tienen que ser usuarios del portal, y el registro les da acceso.** Quien figure
como interesado pasa a ver ese proyecto completo —ficha, hitos, bitácora, bloqueos, riesgos y
evidencia— aunque sea de otra institución, área o unidad. La hoja no es una lista de contactos: es
a quién le está abriendo el proyecto. Por eso la columna es un correo elegido de la lista, y alguien
sin cuenta (BID, PNUD, una cámara) no se puede registrar: primero hay que crearle el usuario.

**`Institución ejecutora` decide quién ve el proyecto.** Es el ancla del filtro de alcance: quien lo
ejecuta, no de quién trata. «SOL — CONSUCOOP» lo ejecuta DIGER. Poner otra institución ahí saca el
proyecto de la vista de DIGER. Área y unidad vacías = transversal, visible para toda la institución.

**El script escribe `Estado` directo**, sin pasar por `Proyecto.CambiarEstado`, así que no valida las
transiciones ni notifica al responsable. Para una carga inicial es lo que se quiere; para mover el
estado de un proyecto vivo, el portal.

## Dos requisitos de todo .sql que se corra con sqlcmd

Valen para el script generado —que ya los cumple— y para cualquier script suelto de `database/`.
SSMS los resuelve solo, así que el problema aparece únicamente por línea de comandos.

**`SET QUOTED_IDENTIFIER ON`, en su propio lote.** `Proyectos.Codigo` tiene un índice único filtrado
y SQL Server rechaza el INSERT si la opción viene apagada — como la deja `sqlcmd` por omisión
(Msg 1934). Tiene que ir seguida de `GO`: el lote se compila entero antes de ejecutarse, así que
puesta junto al INSERT no alcanza a aplicarse. Alternativa: `sqlcmd -I`.

**El archivo guardado en UTF-8 con BOM.** Sin BOM, sqlcmd lo lee como ANSI y los `—` y las tildes
llegan corrompidos. Esto es peor que un error, porque **rompe la idempotencia sin avisar**:
`N'SOL — CONSUCOOP'` deja de coincidir con la fila que ya existe, el `IF NOT EXISTS` da verdadero y
el script inserta un duplicado en lugar de actualizar. Medido: correr los 8 scripts de carga sin BOM
llevó el portafolio de 25 proyectos a 46, sin lanzar un solo error. Alternativa: `sqlcmd -f 65001`.

Los 8 scripts de `database/` que insertan en `Proyectos` ya tienen las dos cosas. Los otros 16 con
acentos, no.

## Requisitos

Excel instalado (los dos scripts lo manejan por COM) y `sqlcmd` en el PATH. Los `.ps1` también van
en **UTF-8 con BOM**, por la misma razón: Windows PowerShell 5.1 lee como ANSI un archivo sin BOM y
rompe todos los acentos del script.
