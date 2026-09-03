# Pruebas sin tocar las bases reales

Para probar el aplicativo entrando, guardando y publicando de verdad, sin que quede una sola
fila de basura en las bases que después hay que fusionar con la de TEST.

## Lo primero: el sistema ya arranca en una copia

Desde el 2 de septiembre de 2026, los tres `appsettings.Development.json` apuntan al
**sandbox**, no a las bases reales:

| Sistema | Base a la que se conecta |
|---|---|
| GestionGD (portal interno) | `DigerTramitesEstado_Unificada_Sandbox` |
| API pública | `DigerTramitesEstado_Unificada_Sandbox` |
| HondurasSimple (portal ciudadano) | `VentanillaDigital_Net_Sandbox` |

Vale igual si arranca desde Visual Studio, desde `dotnet run` o desde `Iniciar-Todo.ps1`.
Encender el sistema **no puede** escribir en la base real: para eso habría que descomentar a
mano la otra línea del archivo, y que cueste un gesto consciente es justamente lo que evita
el accidente.

## Los dos entornos, y cuándo se usa cada uno

**Sandbox** (`_Sandbox`) — el del día a día, y el que usa quien prueba. Es una copia
permanente, sembrada para que haya contenido que mirar. Se ensucia sin culpa; cuando estorbe,
se rehace.

```
scripts\pruebas\Refrescar-Sandbox.ps1            crea el sandbox y lo siembra
scripts\pruebas\Refrescar-Sandbox.ps1 -Rehacer   lo tira y lo copia de nuevo
scripts\Iniciar-Todo.ps1                         levanta los tres sistemas
```

**Desechable** (`_E2E`) — para cuando hace falta *demostrar* que las bases reales no se
tocaron: toma una huella antes, otra después, y las compara.

```
scripts\pruebas\Nuevo-EntornoPruebas.ps1      copia las reales y toma su huella
scripts\pruebas\Iniciar-EntornoPruebas.ps1    levanta los tres en puertos aparte
scripts\pruebas\Verificar-BasesReales.ps1     dice si las reales cambiaron
scripts\pruebas\Borrar-EntornoPruebas.ps1     destruye copias y archivos subidos
```

## Por qué hace falta sembrar el sandbox

La base real es un volcado crudo de SIGER: 1057 trámites, **ninguno publicado**, ninguno con
categoría, modalidad, costo ni tiempo. HondurasSimple solo enseña lo que GestionGD publica,
así que contra una copia tal cual el catálogo sale **vacío** y no hay nada que probar.

El sembrado publica los trámites que tienen con qué llenar una ficha —institución, pasos y
requisitos reales— y les pone categoría, modalidad, costo y tiempo **de relleno**. Ese
relleno es inventado y está ahí para que las pantallas tengan variedad que filtrar y ordenar.
Lo que se prueba es el comportamiento de la pantalla, no la exactitud del dato.

Resultado del sembrado: **460 publicados** en 19 instituciones, con las cuatro modalidades
representadas y un resto a propósito sin modalidad, sin costo y sin tiempo, porque hay
comportamientos que solo se ven cuando el dato falta.

De esos 460, HondurasSimple replica **54**: los de `INPREMA`, `IHTT` y `CONSUCOOP`, que es el
corte del piloto. Eso lo decide el propio portal ciudadano, no el sembrado.

## Direcciones

| Sistema | Con `Iniciar-Todo.ps1` (sandbox) | Con `Iniciar-EntornoPruebas.ps1` (desechable) |
|---|---|---|
| GestionGD | https://localhost:49175 | https://localhost:49185 |
| API pública | https://localhost:7199/swagger | http://localhost:5299/swagger |
| HondurasSimple | https://localhost:7180 | https://localhost:7280 |

Chrome avisa la primera vez de que el certificado no es de fiar. Se arregla de una vez con
`dotnet dev-certs https --trust`, aceptando el cuadro que sale.

## Por qué no puede tocar la base real

Cuatro barreras, no una:

1. **Sufijo obligatorio.** Ningún guion crea, sobrescribe ni borra una base cuyo nombre no
   termine en `_E2E` o `_Sandbox`. La comprobación corre otra vez justo antes de cada
   `RESTORE` y de cada `DROP`, no solo al empezar. El guion de sembrado hace lo mismo por su
   cuenta: si la base no termina en `_Sandbox`, se detiene sin tocar nada.
2. **Lista de intocables.** `DigerTramitesEstado_Unificada`, `VentanillaDigital_Net`,
   `GestionGD_TEST`, `TramitesEstado_Prod`, `master` y las demás están negadas por nombre,
   aunque alguien las renombrara terminándolas en `_Sandbox`.
3. **Development ya no apunta a las reales.** Es la barrera que cubre el arranque normal,
   el que nadie recuerda revisar.
4. **El respaldo es `COPY_ONLY`.** Copiar la base real no le altera ni su cadena de respaldos.

Y para no quedarse en la promesa, el entorno desechable toma una huella de las bases reales
antes de empezar y la vuelve a tomar al terminar. La huella son dos medidas: filas por tabla,
que ve las altas y las bajas; y el contador de escrituras del propio motor (`user_updates`),
que ve además las modificaciones. Si las dos coinciden, no se escribió nada.

## La comprobación a ojo

Arriba de cada pantalla hay una cinta. En pruebas es verde y dice el nombre de la base:

    Entorno de pruebas · base DigerTramitesEstado_Unificada_Sandbox

Si dice `_Sandbox` o `_E2E`, es una copia. Si alguna vez sale roja diciendo
**PRODUCCIÓN — datos reales**, hay que cerrar y avisar.

## Cosas que conviene saber

- Los usuarios y contraseñas son los mismos que en la base real: la copia es idéntica.
- La clave de la API la manda *user-secrets* si está puesta; da igual, ambos lados usan la
  misma. El valor de `appsettings.Development.json` solo entra si no hay user-secrets.
- HondurasSimple sincroniza cada 10 segundos el ciclo ligero y cada **5 minutos** el pesado.
  Las **bajas** solo se resuelven en el pesado: quitar una publicación tarda hasta 5 minutos
  en verse.
- El sembrado no se repite solo. `Refrescar-Sandbox.ps1` lo salta si ya hay trámites
  publicados, para no pisar lo que se haya publicado a mano durante las pruebas. Con
  `-SoloSembrar` se fuerza.

## Los dos recorridos de prueba

Hay dos, y prueban cosas distintas. No se solapan.

| Documento | Qué prueba | Dónde |
|---|---|---|
| `docs\flujo-de-pruebas-separacion-honduras-simple.html` | **El portal interno**: que SIGER quedara de pura consulta y que el trabajo de Honduras Simple viva detrás de su propia puerta. Termina comprobando que lo publicado llega al portal ciudadano. 20 pasos. | Este repositorio |
| `honduras-agil\docs\flujo-de-pruebas-hondurassimple.html` | **El portal ciudadano** por dentro: portada, catálogo, fichas, búsqueda, accesibilidad. 22 pasos. | Repositorio `honduras-agil` |

El de la separación necesita **tres usuarios**, que existen solo en el sandbox y comparten la
contraseña `Pruebas#2026`:

| Correo | Rol | Para qué |
|---|---|---|
| `inventario.cowork@diger.gob.hn` | Consultor | El perfil de fuera: solo debe alcanzar el inventario |
| `operador.cowork@diger.gob.hn` | Jefe de Área | Hace el trabajo de Honduras Simple. **No es administrador** |
| `admin.cowork@diger.gob.hn` | Administrador | Solo para abrir y cerrar permisos |

Si se rehace el sandbox con `-Rehacer`, esos tres usuarios **se pierden** junto con la copia.
Se vuelven a crear con:

```
sqlcmd -S "LP-GD-JAGM\SQLEXPRESS" -U sa -P admin123 -C -I ^
       -d DigerTramitesEstado_Unificada_Sandbox ^
       -i scripts\pruebas\sql\usuarios-cowork.sql
```

Es idempotente, y se niega a correr contra cualquier base que no termine en `_Sandbox`.

## Encargo para Cowork

Texto para pegarle tal cual:

> Quiero que pruebes desde Chrome, viéndolo, el cambio que separó Honduras Simple del
> inventario SIGER en el portal interno.
>
> 1. Abrí PowerShell en `C:\Users\jgarcia\Documents\Portal-Informacion-Institucional`.
> 2. Corré `scripts\pruebas\Refrescar-Sandbox.ps1` y después `scripts\Iniciar-Todo.ps1`.
> 3. Entrá a https://localhost:49175 y confirmá que la cinta de arriba dice
>    **Entorno de pruebas** y que el nombre de la base termina en `_Sandbox`. Si no dice eso,
>    parás y avisás.
> 4. Seguí el documento `docs\flujo-de-pruebas-separacion-honduras-simple.html` de arriba
>    abajo, los 20 pasos. Antes de reportar nada, leé la sección
>    «Lo que ya sabemos y no hay que reportar»: hay cosas que parecen fallos y no lo son.
> 5. Usá los tres usuarios que dice el documento, cada uno en su ventana de incógnito. El
>    paso 7 y el paso 15 son los importantes; si tenés poco tiempo, esos dos no los saltes.
> 6. Podés escribir, publicar y borrar lo que haga falta: es una copia desechable.
> 7. Contame paso por paso cuál pasó y cuál no, con captura de lo que no.
>
> Si después sobra tiempo, hacé también el recorrido del portal ciudadano:
> `honduras-agil\docs\flujo-de-pruebas-hondurassimple.html`, https://localhost:7180.
>
> No edites ningún `appsettings.json`. No corras nada contra
> `DigerTramitesEstado_Unificada` ni `VentanillaDigital_Net`.

## Si algo sale mal

- **El catálogo de HondurasSimple sale vacío.** O no ha corrido la sincronización, o la API
  no responde. Abra https://localhost:7199/swagger para descartar lo segundo.
- **Un puerto quedó ocupado.** `Detener-EntornoPruebas.ps1` para el entorno desechable; para
  el normal, cierre las ventanas que abrió `Iniciar-Todo.ps1`.
- **No se puede borrar una copia porque hay conexiones.** Detenga primero; el `DROP` usa
  `SINGLE_USER WITH ROLLBACK IMMEDIATE`, pero SSMS abierto en esa base también cuenta.
- **`Verificar-BasesReales.ps1` marca diferencias.** Si tuvo SSMS o Visual Studio escribiendo
  en la base real mientras probaba, la diferencia puede ser suya y no de las pruebas.
