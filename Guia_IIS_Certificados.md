# Guía de Configuración de IIS para Autenticación por Certificados

Esta guía describe cómo configurar tu servidor IIS (Internet Information Services) para soportar el inicio de sesión con Certificados Digitales en el portal **DIGER Trámites Estado**.

Dado que Kestrel no maneja directamente el tráfico cuando la aplicación está hosteada detrás de IIS, toda la configuración de requerimiento de certificados debe hacerse en el **Administrador de IIS (IIS Manager)**.

## Arquitectura de la Solución en IIS

Para lograr la mejor experiencia de usuario (donde el certificado no se le pide al usuario en todas las páginas, sino sólo cuando da clic en "Iniciar sesión con Certificado"), debemos utilizar **dos bindings (enlaces) separados**:

1. **El portal principal:** `tramites.diger.gob.hn` (donde **NO** se requiere certificado).
2. **El endpoint de autenticación:** `cert.tramites.diger.gob.hn` (donde **SÍ** se requiere certificado).

Ambos dominios deben apuntar a la misma carpeta física donde está alojada tu aplicación en IIS.

---

## Paso 1: Configurar el Sitio Principal

Este paso asume que ya tienes tu sitio web creado en IIS apuntando a los archivos publicados de ASP.NET Core.

1. Abre **IIS Manager**.
2. Selecciona tu sitio web (ej. `DigerTramites`).
3. En el panel central, haz doble clic en **SSL Settings** (Configuración SSL).
4. Asegúrate de que:
   - **Require SSL** (Requerir SSL) esté **marcado**.
   - En **Client certificates** (Certificados de cliente), esté seleccionado **Ignore** (Ignorar).
5. En el panel derecho, haz clic en **Apply** (Aplicar).

---

## Paso 2: Crear el Binding para Certificados (`cert.*`)

Ahora le diremos a IIS que escuche en el subdominio de certificados y lo obligaremos a solicitar la firma al navegador.

1. Selecciona tu sitio web en IIS Manager.
2. En el panel derecho, haz clic en **Bindings...** (Enlaces...).
3. Haz clic en **Add...** (Agregar...).
   - **Type:** `https`
   - **Host name:** `cert.tramites.diger.gob.hn` *(o el subdominio/puerto que vayas a usar)*
   - Selecciona el certificado SSL/TLS del servidor en la lista desplegable.
   - Haz clic en **OK**.

### 2.1 Habilitar Certificados de Cliente SOLO para ese binding
A partir de IIS 8, puedes requerir certificados de cliente específicamente para un Hostname (Server Name Indication - SNI) o modificando el `web.config` por locations. Sin embargo, la forma más limpia en IIS In-Process para un subdominio es configurar el enlace completo a nivel de línea de comandos de IIS (netsh) si es un puerto, o habilitando TLS renegotiation. 

Para hacerlo más sencillo usando un segundo Sitio IIS:
Si el binding único no te permite poner `Require` solo a ese hostname (porque en la UI afecta a todo el sitio), **crea un Sitio IIS gemelo**:
1. Crea un nuevo Sitio Web en IIS llamado `DigerTramites_Cert`.
2. Apúntalo exactamente a la **misma ruta física** que el sitio principal.
3. Agrégale el binding HTTPS con Hostname `cert.tramites.diger.gob.hn`.
4. Ve a **SSL Settings** de este *nuevo* sitio.
5. Selecciona **Require** o **Accept** (Aceptar) Client certificates.
   > **Nota:** `Accept` permite que la conexión se establezca incluso si el usuario no envía certificado, permitiéndote mostrar un error amable en la web ("No se detectó certificado"). `Require` cortará la conexión a nivel TCP/IP si el usuario cancela la ventana de selección de certificado, mostrando el feo error `ERR_BAD_SSL_CLIENT_AUTH_CERT` del navegador. **Recomendamos usar `Accept`.**
6. Clic en **Apply**.

---

## Paso 3: Forwarding del Certificado (Solo para Out-Of-Process o Proxies)

Si tu IIS está configurado para ejecutar ASP.NET Core en modo **Out-Of-Process** (o si tienes IIS configurado como Reverse Proxy usando ARR hacia Kestrel), debes indicarle a IIS que le envíe el certificado a ASP.NET Core.

1. Abre el **Configuration Editor** (Editor de Configuración) a nivel del servidor IIS.
2. Navega a `system.webServer/proxy`.
3. Asegúrate de que la propiedad `preserveClientCertificate` esté en `True`.
4. IIS enviará el certificado codificado en Base64 en el encabezado HTTP `X-ARR-ClientCert`.
5. *(El código de la aplicación ya fue modificado para leer este encabezado mediante `app.UseCertificateForwarding();`)*.

---

## Paso 4: Validaciones de Red y Firewall (Muy Importante)

Cuando un usuario presenta un certificado digital (ej. un Token USB, Bit4Id), el **Servidor Windows** (donde está IIS) intentará validar si ese certificado ha sido revocado. Para hacerlo, Windows descargará la Lista de Revocación de Certificados (CRL) de la CA (Autoridad Certificadora) que emitió el token.

**Requisito de Firewall:**
Tu servidor IIS **debe tener salida a Internet (puerto 80 y 443)** hacia las URLs de las Autoridades Certificadoras (las rutas CRL vienen dentro del certificado del usuario).
Si el servidor no tiene Internet, IIS rechazará inmediatamente el certificado por no poder validar su estado de revocación.

---

## Resumen del Flujo
1. El usuario entra a `tramites.diger.gob.hn` (Ignora cert).
2. Da clic en "Ingresar con Certificado".
3. Es redirigido a `cert.tramites.diger.gob.hn/Cuenta/LoginCertificado`.
4. IIS (`cert.*`) tiene *Accept Client Certificates*. Le pide al navegador el token.
5. El navegador envía el token. IIS valida el CRL.
6. ASP.NET Core lee el certificado (In-Process directo, o vía `X-ARR-ClientCert` Out-Of-Process).
7. La pantalla de la UI muestra "Validando...".
8. Se hace la petición POST asíncrona validando la caducidad y el Thumbprint.
9. ASP.NET Core emite la cookie cifrada al dominio/localhost.
10. Se redirige al usuario de vuelta al sistema.
