Quiero que pruebes, viéndolo en Chrome, un cambio que acabamos de hacer en el portal
interno (GestionGD). Separamos en dos lo que antes era una sola sección: **SIGER** quedó
de pura consulta, y todo el trabajo que escribe —conciliación, publicado, editar fichas,
completar fichas— se mudó a una sección aparte llamada **Honduras Simple**.

Lo que necesito saber es si esa separación aguanta: que quien solo tenga el inventario no
pueda tocar nada, que quien haga el trabajo no se quede sin nada, y que lo que se publica
siga llegando al portal ciudadano.

## Preparar

1. Abrí PowerShell en `C:\Users\jgarcia\Documents\Portal-Informacion-Institucional`.
2. Corré `scripts\pruebas\Refrescar-Sandbox.ps1` y después `scripts\Iniciar-Todo.ps1`.
3. Entrá a https://localhost:49175 y mirá la cinta de arriba. Debe decir **Entorno de
   pruebas** y el nombre de la base debe terminar en **_Sandbox**.
   **Si no dice eso, parás y me avisás.** No sigas.

Si al entrar te rebota el usuario, corré esto y volvé a intentar:

```
sqlcmd -S "LP-GD-JAGM\SQLEXPRESS" -U sa -P admin123 -C -I ^
       -d DigerTramitesEstado_Unificada_Sandbox ^
       -i scripts\pruebas\sql\usuarios-cowork.sql
```

## Los tres usuarios

Contraseña para los tres: `Pruebas#2026`

| Correo | Quién es |
|---|---|
| `inventario.cowork@diger.gob.hn` | El de fuera. Solo debería alcanzar el inventario. |
| `operador.cowork@diger.gob.hn` | El que hace el trabajo. **No es administrador.** |
| `admin.cowork@diger.gob.hn` | Solo para abrir y cerrar permisos. |

Usá **una ventana de incógnito por usuario**, así no andás cerrando sesión todo el rato y
podés saltar de una a otra.

## Probar

Seguí `docs\flujo-de-pruebas-separacion-honduras-simple.html` de arriba abajo, los 20
pasos, en orden. Ábrelo en Chrome; tiene casillas para ir marcando.

**Antes de reportar nada, leé la sección «Lo que ya sabemos y no hay que reportar».** Hay
cosas que parecen fallos y no lo son; si me las reportás perdemos los dos el tiempo.

Si te queda poco tiempo, hay dos que no podés saltarte:

- **Paso 7** — el operador abre las siete pantallas sin ser administrador.
- **Paso 15** — al de fuera se le cierra Honduras Simple y el inventario le sigue
  funcionando.

Podés escribir, publicar y borrar lo que haga falta: es una copia desechable, no toca
ninguna base real.

## Cómo quiero el reporte

Al terminar, mandame **dos cosas**.

**Primero, la tabla completa** — los 20 pasos, aunque casi todos pasen:

```
Paso 1   OK
Paso 2   OK
Paso 3   FALLA
Paso 4   OK
...
Paso 9   NO PROBADO  (no me cargaba la pantalla)
```

Necesito ver también los que pasaron. Un reporte que solo trae los fallos no me dice si
probaste 20 pasos o 4.

**Después, una ficha por cada FALLA**, con esta forma exacta:

```
PASO:       7
USUARIO:    operador.cowork@diger.gob.hn
DIRECCIÓN:  https://localhost:49175/HondurasSimple/Conciliacion
ESPERABA:   Que abriera la pantalla de conciliación con su lista.
PASÓ:       Me mandó a "Acceso denegado".
CAPTURA:    [adjuntá la imagen]
```

Cuatro cosas más sobre esto:

- **La dirección, copiada de la barra del navegador**, tal cual, con todo lo que traiga
  detrás del signo de interrogación. Es el dato que más me sirve y el que siempre se
  pierde.
- **La captura, de la pantalla entera**, sin recortar. Que se vea el menú de la izquierda y
  la barra de direcciones. Un recorte del error solo no me dice con qué usuario estabas.
- **No averigües la causa ni intentes arreglarlo.** No toques código, ni `appsettings.json`,
  ni la base. Solo decime qué viste. De la causa me encargo yo.
- **Si algo te pareció raro pero no es un paso del documento**, mandámelo igual al final,
  bajo el título «Cosas raras que no venían en el documento». Ahí no hace falta formato.

Si te topás con algo que **bloquea** el resto del recorrido —no podés entrar, no levanta,
la cinta dice otra base— no sigas peleando: mandame eso solo y esperá.

## Lo que no hay que tocar

No edites ningún `appsettings.json`. No corras nada contra `DigerTramitesEstado_Unificada`
ni contra `VentanillaDigital_Net`: esas dos son las de verdad.

Si dejaste el sandbox hecho un desastre, no pasa nada, es para eso — pero avisame antes de
rehacerlo, porque eso borra también los tres usuarios de prueba.
