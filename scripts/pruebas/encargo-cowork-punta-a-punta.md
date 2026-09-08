Quiero que pruebes, viéndolo en Chrome, **la cadena completa**: que un trámite nazca en el
portal interno (GestionGD), lo completemos, lo publiquemos, y llegue solo hasta el portal
ciudadano (Honduras Simple) pasando por la API.

Son **tres sistemas**. La vez pasada probaste uno; ahora hay que tener los tres arriba al
mismo tiempo y saltar entre ellos. Lo que necesito saber es **en qué salto se rompe**, si
se rompe. Esa es toda la gracia del recorrido: está armado para que puedas decirlo sin
tener que averiguar la causa.

## Preparar

1. Abrí PowerShell en `C:\Users\jgarcia\Documents\Portal-Informacion-Institucional`.
2. Corré `scripts\pruebas\Refrescar-Sandbox.ps1` y después `scripts\Iniciar-Todo.ps1`.
3. Ese script levanta los tres y te abre las tres pestañas. Dejalas abiertas todo el rato.

| Qué | Dónde |
|---|---|
| Portal interno (GestionGD) | https://localhost:49175 |
| La API | https://localhost:7199/swagger |
| Portal ciudadano (Honduras Simple) | https://localhost:7180 |

**Mirá la cinta de arriba de cada portal antes de empezar.** El interno debe decir una base
terminada en `DigerTramitesEstado_Unificada_Sandbox` y el ciudadano en
`VentanillaDigital_Net_Sandbox`. **Si una de las dos no dice _Sandbox, parás y me avisás.**
No sigas: estarías escribiendo en datos que no son de prueba.

Si al entrar te rebota el usuario, corré esto y volvé a intentar:

```
sqlcmd -S "LP-GD-JAGM\SQLEXPRESS" -U sa -P admin123 -C -I ^
       -d DigerTramitesEstado_Unificada_Sandbox ^
       -i scripts\pruebas\sql\usuarios-cowork.sql
```

## Los usuarios

Contraseña: `Pruebas#2026`

| Correo | Quién es |
|---|---|
| `operador.cowork@diger.gob.hn` | Hace casi todo. Institución **CONSUCOOP**. **No es administrador.** |
| `admin.cowork@diger.gob.hn` | Solo si el operador se topa con una puerta cerrada. |

El **portal ciudadano no pide cuenta**: es público, se entra y ya.

La clave de la API es `clave-de-pruebas-local`. Se pega una sola vez, en el paso 2, con el
botón **Authorize** de Swagger.

## Probar

Seguí `docs\flujo-de-pruebas-punta-a-punta.html` de arriba abajo, los **22 pasos, en
orden**. Ábrelo en Chrome; tiene casillas para ir marcando.

**En orden de verdad, no salteado.** Este recorrido no es una lista de comprobaciones
sueltas: sigue **un solo trámite** por toda la cadena. El código que anotás en el paso 4 se
usa hasta el 19. Si empezás por la mitad, no hay trámite que seguir.

**Antes de reportar nada, leé la sección «Lo que ya sabemos y no hay que reportar».** Hay
tres cosas que parecen fallos y no lo son, y una de ellas te va a pasar seguro.

Si te queda poco tiempo, hay tres que no podés saltarte:

- **Paso 11** — el trámite publicado aparece solo en el portal ciudadano, sin reiniciar nada.
- **Paso 16** — un costo sin capturar nunca dice «gratuito».
- **Paso 20** — se apaga la API y el portal ciudadano sigue funcionando.

Podés escribir, publicar y despublicar lo que haga falta: es una copia desechable, no toca
ninguna base real.

## Dos cosas que este recorrido pide y el anterior no

**Anotá los tiempos.** Los pasos 11 y 19 preguntan cuánto tardó en aparecer y en
desaparecer el trámite. Anotá el minuto, aunque tarde poco. Un «tardó ocho minutos» es un
hallazgo, no una molestia — y sin el número no sirve de nada.

**Anotá el código del trámite.** El del paso 4, con la forma `603-019`. Va en cada reporte
que me mandes. Sin él no puedo mirar nada.

## Cómo quiero el reporte

Al terminar, mandame **dos cosas**.

**Primero, la tabla completa** — los 22 pasos, aunque casi todos pasen:

```
Paso 1   OK
Paso 2   OK
Paso 3   FALLA
...
Paso 11  OK   (tardó 25 segundos)
Paso 19  OK   (desapareció al minuto 4)
Paso 12  NO PROBADO  (no me cargaba la pantalla)
```

Necesito ver también los que pasaron. Un reporte que solo trae los fallos no me dice si
probaste 22 pasos o 4.

**Después, una ficha por cada FALLA**, con esta forma exacta:

```
PASO:       11
SISTEMA:    Portal ciudadano
CÓDIGO:     603-019
DIRECCIÓN:  https://localhost:7180/Tramites?Busqueda=inscripcion
ESPERABA:   Que el trámite apareciera en el catálogo en menos de un minuto.
PASÓ:       Esperé diez minutos y no salió. En la API sí sale (paso 9 dio 200).
CAPTURA:    [adjuntá la imagen]
```

La línea **SISTEMA** es nueva y es la más importante de todas: decime en cuál de los tres
lo viste. Y si el paso anterior de la cadena sí pasó, decilo también, como en el ejemplo —
eso es lo que sitúa el fallo en un salto concreto.

Lo demás, igual que la vez pasada:

- **La dirección, copiada de la barra del navegador**, tal cual, con todo lo que traiga
  detrás del signo de interrogación.
- **La captura, de la pantalla entera**, sin recortar.
- **No averigües la causa ni intentes arreglarlo.** No toques código, ni `appsettings.json`,
  ni la base. De la causa me encargo yo.
- **Si algo te pareció raro pero no es un paso del documento**, mandámelo al final bajo
  «Cosas raras que no venían en el documento».

Si algo **bloquea** el resto del recorrido —no levanta uno de los tres, la cinta dice otra
base, la API no responde nunca— no sigas peleando: mandame eso solo y esperá.

## Lo que no hay que tocar

No edites ningún `appsettings.json` de los tres proyectos. No corras nada contra
`DigerTramitesEstado_Unificada` ni contra `VentanillaDigital_Net`: esas dos son las de
verdad.

En el paso 20 hay que cerrar la ventana de la API, y en el 21 volver a levantarla con
`scripts\Iniciar-Todo.ps1 -Solo Api`. Eso sí está pedido y es parte de la prueba. No apagues
ninguno de los otros dos.

Si dejaste el sandbox hecho un desastre, no pasa nada, es para eso — pero avisame antes de
rehacerlo, porque eso borra también los usuarios de prueba.
