# Pruebas sin tocar las bases reales

Para probar el aplicativo entrando, guardando y publicando de verdad, sin que quede una sola
fila de basura en las bases que después hay que fusionar con la de TEST.

## La idea

No se limpia después: se prueba sobre una copia y la copia se destruye entera.

Limpiar no alcanza. Una prueba no solo inserta filas: deja bitácora, historial, y sellos de
`UpdatedAt` y `UpdatedBy` en filas que ya existían. Eso no se deshace con un `DELETE`, y es
justo lo que ensuciaría la fusión con TEST.

## Los cuatro comandos

Desde `scripts\pruebas` en PowerShell:

| Comando | Qué hace |
|---|---|
| `.\Nuevo-EntornoPruebas.ps1` | Copia las bases reales a copias `_E2E` y guarda una huella de las reales |
| `.\Iniciar-EntornoPruebas.ps1` | Levanta las tres aplicaciones apuntadas a las copias |
| `.\Verificar-BasesReales.ps1` | Dice si las bases reales cambiaron (se puede correr cuando sea) |
| `.\Borrar-EntornoPruebas.ps1` | Baja todo, borra los archivos subidos y destruye las copias |

`Borrar` ya llama a `Detener` y a `Verificar` por dentro. Con esos cuatro alcanza.

## Direcciones mientras el entorno está arriba

| Sistema | Dirección |
|---|---|
| GestionGD (portal interno) | https://localhost:49185 |
| API pública | http://localhost:5299/swagger |
| HondurasÁgil (portal ciudadano) | https://localhost:7280 |

Son puertos distintos a los de desarrollo (49175 / 7199 / 7180) a propósito: los dos entornos
pueden estar arriba a la vez sin pisarse, y el puerto por sí solo dice dónde está uno parado.

## Por qué no puede tocar la base real

Cuatro barreras, no una:

1. **Sufijo obligatorio.** Ningún guion crea, sobrescribe ni borra una base cuyo nombre no
   termine en `_E2E`. La comprobación corre otra vez justo antes de cada `RESTORE` y de cada
   `DROP`, no solo al empezar.
2. **Lista de intocables.** `DigerTramitesEstado_Unificada`, `VentanillaDigital_Net`,
   `GestionGD_TEST`, `TramitesEstado_Prod`, `master` y las demás están negadas por nombre,
   aunque alguien las renombrara terminándolas en `_E2E`.
3. **Ningún appsettings se toca.** Las aplicaciones se apuntan a las copias solo por variables
   de entorno del proceso que se lanza, y esas mueren con el proceso. Un archivo de
   configuración editado, en cambio, sobrevive al olvido.
4. **El respaldo es `COPY_ONLY`.** Copiar la base real no le altera ni su cadena de respaldos.

Y para no quedarse en la promesa, `Nuevo-EntornoPruebas.ps1` toma una huella de las bases
reales antes de empezar, y `Borrar-EntornoPruebas.ps1` la vuelve a tomar al terminar y las
compara. La huella son dos medidas:

- filas por tabla, que ve las altas y las bajas;
- el contador de escrituras del propio motor (`user_updates`), que ve además las
  modificaciones, que el conteo de filas no distingue.

Si las dos coinciden, no se escribió nada. Si SQL Server se reinició en medio, el contador
vuelve a cero y deja de servir; el guion lo detecta y lo dice, en vez de dar un falso verde.

## La comprobación a ojo

Arriba de cada pantalla hay una cinta. En pruebas es verde y dice el nombre de la base:

    Entorno de pruebas · base DigerTramitesEstado_Unificada_E2E

Si dice `_E2E`, es la copia. Si alguna vez sale roja diciendo **PRODUCCIÓN — datos reales**,
hay que cerrar y avisar.

## Cosas que conviene saber antes de probar

- Los usuarios y contraseñas son los mismos que en la base real: la copia es idéntica.
- La copia se hace en el momento, así que refleja la base real tal como esté ese día.
- HondurasÁgil reconcilia con la API cada 60 minutos. Para no esperar una hora a que se vea
  un cambio hecho en GestionGD, arranque con `.\Iniciar-EntornoPruebas.ps1 -SincronizacionRapida`
  y el ciclo pesado pasa a un minuto.
- La salida de cada aplicación va a `.estado\log-portal.txt`, `log-api.txt` y `log-agil.txt`.
  Si algo no arranca, el motivo está ahí, no en una ventana que hay que estar mirando.
- El plan de pruebas con los 14 casos está en
  `honduras-agil\docs\plan-de-pruebas-manual.html`.

## Encargo para Cowork

Texto para pegarle tal cual:

> Quiero que pruebes el aplicativo sin tocar las bases reales.
>
> 1. Abrí PowerShell en `C:\Users\jgarcia\Documents\Portal-Informacion-Institucional\scripts\pruebas`.
> 2. Corré `.\Nuevo-EntornoPruebas.ps1 -Rehacer` y después
>    `.\Iniciar-EntornoPruebas.ps1 -SincronizacionRapida`.
> 3. Antes de tocar nada, entrá a https://localhost:49185 y confirmá que la cinta de arriba
>    dice **Entorno de pruebas** y que el nombre de la base termina en `_E2E`. Si no dice eso,
>    parás y avisás.
> 4. Corré los casos del plan `honduras-agil\docs\plan-de-pruebas-manual.html` contra
>    https://localhost:49185 y https://localhost:7280. Podés crear, editar, publicar y borrar
>    lo que haga falta: es una copia desechable.
> 5. Al terminar, corré `.\Borrar-EntornoPruebas.ps1` y pegame lo que imprime la parte de
>    "Comprobando que las bases reales están intactas".
> 6. Contame qué caso pasó y cuál no, con la evidencia.
>
> No edites ningún `appsettings.json`. No corras nada contra
> `DigerTramitesEstado_Unificada` ni `VentanillaDigital_Net`.

## Si algo sale mal

- **Un puerto quedó ocupado.** `.\Detener-EntornoPruebas.ps1` mata lo que esté escuchando en
  los puertos de pruebas.
- **No se puede borrar una copia porque hay conexiones.** Correr `Detener` primero; el `DROP`
  usa `SINGLE_USER WITH ROLLBACK IMMEDIATE`, pero SSMS abierto en esa base también cuenta.
- **`Verificar-BasesReales.ps1` marca diferencias.** Si tuvo SSMS o Visual Studio escribiendo
  en la base real mientras probaba, la diferencia puede ser suya y no de las pruebas. El
  detalle sale tabla por tabla.
