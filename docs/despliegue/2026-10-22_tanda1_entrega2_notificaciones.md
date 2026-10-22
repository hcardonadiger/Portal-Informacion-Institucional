# Tanda 1, entrega 2 (notificaciones y correo) — notas de despliegue

Rama `Jamil-GestionGD`, sobre la entrega 1. **No cambia la base de datos**: no tiene migraciones
ni scripts. Va después de la entrega 1 (`2026-10-22_tanda1_entrega1_seguridad.md`); su paso de
`Correo__UrlBase` sigue siendo obligatorio.

## Qué cambia y por qué

| Antes | Ahora |
|---|---|
| Los recordatorios automáticos usaban la fecha UTC. Desde las 6:00 p. m. daban por vencido lo que vence ese día, y avisaban de la reunión de pasado mañana como si fuera la de mañana. Lo mismo pasaba con las etapas de cronograma de Expedientes y con el cronograma de la ficha del proyecto. | «Hoy» y «mañana» son los de Honduras (`Notificaciones:ZonaHoraria`, `America/Tegucigalpa` por defecto). |
| Los enlaces de los correos de recordatorio eran relativos y no abrían desde el programa de correo. | Son absolutos, armados con `Correo:UrlBase`. |
| Los asuntos decían «DIGER» escrito a mano, y la firma de la recuperación de contraseña decía «Dirección de Gestión de Riesgos». | Salen de la sección `Institucion` de la configuración. |
| Un servidor de correo que no responde dejaba la página esperando. | El envío se corta a los 15 segundos. |
| La pantalla del recordatorio decía «enviado» aunque el correo fallara. | Dice que el aviso quedó en la campana y que el correo no salió. Si todo sale bien, se ve igual que antes. |
| Nada avisaba si faltaba la configuración de correo. | Al arrancar queda una advertencia en el registro si falta `Correo:UrlBase` o el servidor de correo. |

## Configuración

Todo es opcional salvo `Correo__UrlBase`, que viene de la entrega 1. No cambia el servidor, el
usuario ni la contraseña del correo.

| Variable | Valor por defecto | Para qué |
|---|---|---|
| `Notificaciones__ZonaHoraria` | `America/Tegucigalpa` | Zona de «hoy». Acepta también `Central America Standard Time`. Si el servidor no la reconoce, se usa UTC-6 fijo y queda una advertencia en el registro. |
| `Smtp__TimeoutSegundos` | `15` | Tiempo máximo de un envío. |

## Pasos manuales en IIS (los ejecuta Jamil)

Los recordatorios automáticos corren dentro del sitio. Con la configuración por defecto de IIS, el
sitio se apaga cuando nadie lo usa durante 20 minutos, y los avisos no se generan hasta que alguien
vuelve a entrar. Estos pasos lo dejan siempre activo.

Reemplace `<pool>` por el nombre del grupo de aplicaciones del portal y `<sitio>` por el del
sitio. Abra PowerShell como administrador en el servidor.

1. **Comprobar si Application Initialization está instalado.** Hace falta para que el sitio arranque
   solo.
   - En Windows Server:
     ```powershell
     Get-WindowsFeature Web-AppInit
     ```
     Debe decir `Installed` en *Install State*.
   - Si el servidor es un Windows de escritorio:
     ```powershell
     Get-WindowsOptionalFeature -Online -FeatureName IIS-ApplicationInit
     ```
     Debe decir `State : Enabled`.
   - **Si no está instalado, no lo instale.** Instalarlo es un cambio en producción y lo decide la
     jefatura. En ese caso, haga igual los pasos 2 y 3: el grupo queda siempre activo, aunque el
     portal termina de cargar con la primera visita.
2. **El grupo de aplicaciones siempre activo** (`startMode=AlwaysRunning`):
   ```powershell
   Import-Module WebAdministration
   Set-ItemProperty "IIS:\AppPools\<pool>" -Name startMode -Value AlwaysRunning
   ```
   En el Administrador de IIS: *Grupos de aplicaciones* → el grupo → *Configuración avanzada* →
   *Modo de inicio* = `AlwaysRunning`.
3. **El sitio con precarga** (`preloadEnabled`):
   ```powershell
   Set-ItemProperty "IIS:\Sites\<sitio>" -Name applicationDefaults.preloadEnabled -Value True
   ```
   En el Administrador de IIS: el sitio → *Configuración avanzada* → *Precarga habilitada* = `True`.
4. **Recomendado: que el grupo no se apague por inactividad.** Aunque el grupo esté en
   `AlwaysRunning`, IIS lo apaga a los 20 minutos sin uso. Ponerlo en cero lo evita:
   ```powershell
   Set-ItemProperty "IIS:\AppPools\<pool>" -Name processModel.idleTimeout -Value "00:00:00"
   ```
   En el Administrador de IIS: *Configuración avanzada* del grupo → *Tiempo de espera de inactividad
   (minutos)* = `0`.

Para comprobar los tres valores:
```powershell
Get-ItemProperty "IIS:\AppPools\<pool>" -Name startMode
Get-ItemProperty "IIS:\AppPools\<pool>" -Name processModel.idleTimeout
Get-ItemProperty "IIS:\Sites\<sitio>" -Name applicationDefaults.preloadEnabled
```

**Reversión de los pasos de IIS:** `startMode` = `OnDemand`, `preloadEnabled` = `False` e
`idleTimeout` = `00:20:00`.

## Publicación

Igual que la entrega 1:
1. respaldar `App_Data\uploads`;
2. comprobar que el perfil **no** tenga «Quitar archivos adicionales en el destino»;
3. publicar.

**Reversión:** volver a publicar la compilación anterior. Las variables nuevas pueden quedarse: la
versión anterior las ignora.

## Verificación después de publicar

1. **Registro al arrancar.** En el Visor de eventos (*Aplicación*) no debe aparecer «Correo:UrlBase
   no está configurada» ni «El servidor de correo no está configurado». Si aparece
   «Notificaciones:ZonaHoraria … no existe en este servidor», el portal funciona igual con UTC-6
   fijo. Para quitar la advertencia, ponga `Notificaciones__ZonaHoraria` =
   `Central America Standard Time`.
2. **Recordatorio con el correo bien.** Enviar un recordatorio de un ticket. La pantalla debe decir
   lo mismo que antes. En el correo que llega:
   - el enlace empieza con la dirección de `Correo__UrlBase` y abre el ticket;
   - el asunto termina en «— DIGER», tomado de la configuración.
3. **Recuperación de contraseña.** La firma del correo dice «Dirección de Gestión por Resultados
   (DIGER)».
4. **Recordatorios automáticos.** Al día siguiente, la campana de quien tiene compromisos por vencer
   muestra el aviso. Con el sitio siempre activo, el servicio corre a los 2 minutos de arrancar y
   luego cada 4 horas.

## Otro cambio visible

**El editor de expedientes** antes no mostraba ningún mensaje después de una acción. Ahora muestra
la advertencia y los errores del recordatorio, y también los errores de vincular y desvincular
proyectos, que antes se perdían. El mensaje de éxito sigue sin mostrarse en el editor, como antes.

## Riesgos

- **Si Application Initialization no está instalado,** el portal termina de arrancar con la
  primera visita después de cada reinicio del grupo, y los avisos automáticos se retrasan hasta
  entonces.
- **Los avisos automáticos ya generados** con la fecha UTC no se corrigen hacia atrás. Solo cambia
  lo que se genere desde la publicación.
