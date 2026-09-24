/* ─────────────────────────────────────────────────────────────────────────────
   Afiche del QR de asistencia: modal, orientación, compartir, descargar e imprimir.
   Lo inicia Reuniones/Asistencia.cshtml con AficheQr.iniciar({...}).

   Las funciones que los onclick de la página llaman quedan en window, como el resto
   del portal. La lógica vive acá porque la descarga en PNG ya no cabe en la vista.
   ───────────────────────────────────────────────────────────────────────────── */
(function () {
    'use strict';

    var cfg = { url: '', titulo: '', css: '' };
    var ORIENT_CLAVE = 'afiche-qr-orientacion';

    // Medidas del afiche en píxeles CSS: A4 a 96 ppp, las mismas que fija afiche-qr.css.
    var A4_VERTICAL = { ancho: 794, alto: 1123 };
    var A4_APAISADO = { ancho: 1123, alto: 794 };

    // Factor de la descarga: 3 × 96 ppp ≈ 288 ppp, resolución de impresión.
    var ESCALA_PNG = 3;

    function afiche()      { return document.getElementById('afiche'); }
    function esApaisado()  { var a = afiche(); return !!a && a.classList.contains('afiche--apaisado'); }
    function medidas()     { return esApaisado() ? A4_APAISADO : A4_VERTICAL; }

    function escHtml(s) {
        return String(s).replace(/[&<>"]/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c];
        });
    }

    /* ── Avisos en el propio botón ─────────────────────────────────────────── */
    function avisar(ev, texto) {
        var b = ev && (ev.currentTarget || ev.target);
        if (!b || !b.dataset) return;
        if (b.dataset.rotulo === undefined) b.dataset.rotulo = b.textContent;
        b.textContent = texto;
        clearTimeout(b._aviso);
        b._aviso = setTimeout(function () { b.textContent = b.dataset.rotulo; }, 2200);
    }

    /* ── Modal ─────────────────────────────────────────────────────────────── */
    function abrirModalQR() {
        document.getElementById('qr-modal-overlay').classList.add('open');
        ajustarTitulo();
    }

    function cerrarModalQR() {
        document.getElementById('qr-modal-overlay').classList.remove('open');
    }

    /* ── Orientación: vertical para imprimir, horizontal para proyectar ────── */
    function orientarAfiche(cual) {
        var a = afiche();
        if (!a) return;

        var apaisado = cual === 'apaisado';
        a.classList.toggle('afiche--apaisado', apaisado);
        a.classList.toggle('afiche--vertical', !apaisado);

        document.querySelectorAll('.afq-orient-b').forEach(function (b) {
            var activo = b.dataset.orient === cual;
            b.classList.toggle('is-on', activo);
            b.setAttribute('aria-pressed', activo ? 'true' : 'false');
        });

        try { localStorage.setItem(ORIENT_CLAVE, cual); } catch (e) {}
        ajustarTitulo();
    }

    /* El titular es el nombre de la reunión y puede ser largo: se reduce hasta caber en
       las líneas que el afiche reserva. Se cuenta en líneas, no en píxeles, porque la
       vista previa va con `zoom` y ahí las alturas en px no son comparables. */
    function ajustarTitulo() {
        var a = afiche();
        var t = document.getElementById('aficheTitulo');
        if (!a || !t) return;

        a.style.removeProperty('--afq-titulo');
        var est  = getComputedStyle(a);
        var tope = parseInt(est.getPropertyValue('--afq-titulo-max-lineas'), 10);
        var base = parseFloat(est.getPropertyValue('--afq-titulo'));
        if (!tope || !base) return;

        for (var px = base; px >= 22; px -= 2) {
            a.style.setProperty('--afq-titulo', px + 'px');
            var alto = parseFloat(getComputedStyle(t).lineHeight);
            if (!alto) return;
            if (Math.round(t.scrollHeight / alto) <= tope) return;
        }
    }

    /* ── Portapapeles ──────────────────────────────────────────────────────── */
    /* navigator.clipboard no existe cuando el portal se sirve por http:// en la red
       interna, así que hay un segundo camino con execCommand. */
    async function alPortapapeles(texto) {
        try {
            if (navigator.clipboard && window.isSecureContext) {
                await navigator.clipboard.writeText(texto);
                return true;
            }
        } catch (e) {}

        try {
            var caja = document.createElement('textarea');
            caja.value = texto;
            caja.setAttribute('readonly', '');
            caja.style.cssText = 'position:fixed;top:0;left:-9999px';
            document.body.appendChild(caja);
            caja.select();
            caja.setSelectionRange(0, texto.length);
            var ok = document.execCommand('copy');
            document.body.removeChild(caja);
            return ok;
        } catch (e) { return false; }
    }

    function copiar(ev) {
        alPortapapeles(cfg.url).then(function (ok) {
            avisar(ev, ok ? '¡Copiado!' : 'No se pudo copiar');
        });
    }

    async function compartirEnlace(ev) {
        if (navigator.share) {
            try {
                await navigator.share({
                    title: cfg.titulo,
                    text: 'Registro de asistencia · ' + cfg.titulo,
                    url: cfg.url
                });
                return;
            } catch (e) {
                // Cerrar la hoja de compartir no es un error: no hay nada que avisar.
                if (e && e.name === 'AbortError') return;
            }
        }

        var ok = await alPortapapeles(cfg.url);
        avisar(ev, ok ? 'Enlace copiado' : 'No se pudo compartir');
    }

    /* ── Descarga del afiche en PNG ────────────────────────────────────────── */

    function nombreArchivo() {
        var s = String(cfg.titulo).normalize('NFD').replace(/[^ -~]/g, '')
            .toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 60);
        return 'afiche-' + (s || 'reunion') + (esApaisado() ? '-horizontal' : '-vertical') + '.png';
    }

    function cargarImagen(src) {
        return new Promise(function (resolver, rechazar) {
            var img = new Image();
            img.onload  = function () { resolver(img); };
            img.onerror = function () { rechazar(new Error('no se pudo cargar la imagen')); };
            img.src = src;
        });
    }

    var _css = null;
    async function hojaDelAfiche() {
        if (_css === null) _css = await (await fetch(cfg.css)).text();
        return _css;
    }

    /* El logo llega en negro sobre transparente y en tamaño enorme. En el afiche lo
       blanquea un filtro CSS, pero los filtros no son fiables dentro de un SVG que se
       rasteriza, y meter el archivo original como data-URI infla el marcado varios
       cientos de kB. Así que acá se reduce al tamaño que ocupa y se le cambia el color
       con «source-in»: mantiene el alfa y pinta todo lo opaco de blanco, que es
       exactamente lo que hacía brightness(0) invert(1). */
    async function logoBlancoDataUri(src, altoCss) {
        var img = await cargarImagen(src);
        var alto  = Math.round(altoCss * ESCALA_PNG);
        var ancho = Math.round(alto * (img.naturalWidth / img.naturalHeight));

        var lienzo = document.createElement('canvas');
        lienzo.width  = ancho;
        lienzo.height = alto;

        var ctx = lienzo.getContext('2d');
        ctx.drawImage(img, 0, 0, ancho, alto);
        ctx.globalCompositeOperation = 'source-in';
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, ancho, alto);

        return lienzo.toDataURL('image/png');
    }

    /* Dibuja el afiche que se está viendo —con su orientación y su titular ya ajustado—
       y devuelve el PNG. Se envuelve el nodo clonado en un <foreignObject>: dentro de un
       SVG que se usa como imagen no se cargan hojas ni archivos externos, así que el CSS
       va incrustado y el logo convertido a data-URI. El SVG se declara del tamaño final,
       con viewBox en las medidas A4, para que el navegador lo rasterice a esa resolución
       en vez de ampliar un mapa de bits. */
    async function aficheComoBlob() {
        var a = afiche();
        if (!a) throw new Error('no hay afiche');

        var m   = medidas();
        var css = await hojaDelAfiche();

        var clon = a.cloneNode(true);
        clon.removeAttribute('id');

        var logo = clon.querySelector('.afiche-logo');
        if (logo) {
            var alturaLogo = parseFloat(getComputedStyle(a).getPropertyValue('--afq-logo')) || 54;
            logo.setAttribute('src', await logoBlancoDataUri(a.querySelector('.afiche-logo').src, alturaLogo));
            logo.style.filter = 'none';
        }

        var svg =
            '<svg xmlns="http://www.w3.org/2000/svg"'
          + ' width="' + (m.ancho * ESCALA_PNG) + '" height="' + (m.alto * ESCALA_PNG) + '"'
          + ' viewBox="0 0 ' + m.ancho + ' ' + m.alto + '">'
          + '<foreignObject x="0" y="0" width="' + m.ancho + '" height="' + m.alto + '">'
          + '<div xmlns="http://www.w3.org/1999/xhtml">'
          + '<style><![CDATA[' + css + ']]></style>'
          + new XMLSerializer().serializeToString(clon)
          + '</div></foreignObject></svg>';

        var img = await cargarImagen('data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg));

        var lienzo = document.createElement('canvas');
        lienzo.width  = m.ancho * ESCALA_PNG;
        lienzo.height = m.alto * ESCALA_PNG;

        var ctx = lienzo.getContext('2d');
        ctx.drawImage(img, 0, 0, lienzo.width, lienzo.height);

        return await new Promise(function (resolver, rechazar) {
            lienzo.toBlob(function (blob) {
                blob ? resolver(blob) : rechazar(new Error('el lienzo no produjo PNG'));
            }, 'image/png');
        });
    }

    async function descargarAfiche(ev) {
        avisar(ev, 'Generando…');
        try {
            var blob = await aficheComoBlob();
            var url  = URL.createObjectURL(blob);
            var a    = document.createElement('a');

            a.href = url;
            a.download = nombreArchivo();
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            setTimeout(function () { URL.revokeObjectURL(url); }, 10000);

            avisar(ev, 'Afiche descargado');
        } catch (e) {
            // Si el navegador no logra rasterizar el afiche, imprimir sigue dando el mismo
            // resultado en PDF: se dice eso en vez de dejar el botón sin respuesta.
            console.error('[AficheQr] no se pudo generar el PNG', e);
            avisar(ev, 'No se pudo: use Imprimir');
        }
    }

    /* ── Impresión ─────────────────────────────────────────────────────────── */
    /* Imprime el afiche que se está viendo: clona ese mismo nodo en un iframe que carga
       la hoja afiche-qr.css. Sin .afiche-vista alrededor, el afiche sale a escala 1, o sea
       A4 exacto — por eso no hay un segundo diseño para papel. */
    function imprimirAfiche(ev) {
        var a = afiche();
        if (!a) { avisar(ev, 'Afiche no disponible'); return; }
        ajustarTitulo();

        var ifr = document.createElement('iframe');
        ifr.setAttribute('aria-hidden', 'true');
        ifr.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0';
        document.body.appendChild(ifr);

        var doc = ifr.contentWindow.document;
        doc.open();
        doc.write('<!DOCTYPE html><html lang="es"><head><meta charset="utf-8">'
            + '<title>' + escHtml('Afiche QR — ' + cfg.titulo) + '<\/title>'
            + '<link rel="stylesheet" href="' + cfg.css + '">'
            + '<style>html,body{margin:0;padding:0;background:#fff}'
            + '@page{size:A4 ' + (esApaisado() ? 'landscape' : 'portrait') + ';margin:0}<\/style>'
            + '<\/head><body>' + a.outerHTML + '<\/body><\/html>');
        doc.close();

        var lanzado = false;
        function lanzar() {
            if (lanzado) return;
            lanzado = true;
            try { ifr.contentWindow.focus(); ifr.contentWindow.print(); } catch (e) {}
            setTimeout(function () { if (ifr.parentNode) ifr.parentNode.removeChild(ifr); }, 2000);
        }

        // No se imprime hasta que el QR y el logo estén listos: un afiche impreso sin
        // logo o sin código es un afiche echado a perder.
        var esperas = [new Promise(function (r) { ifr.onload = r; setTimeout(r, 1500); })];

        doc.querySelectorAll('img').forEach(function (img) {
            if (!img.complete) esperas.push(new Promise(function (r) { img.onload = img.onerror = r; }));
        });

        Promise.all(esperas).then(lanzar);
        setTimeout(lanzar, 4500);   // red de seguridad: el diálogo siempre se abre
    }

    /* ── Arranque ──────────────────────────────────────────────────────────── */
    function iniciar(opciones) {
        cfg = Object.assign(cfg, opciones || {});

        var guardada = 'vertical';
        try { if (localStorage.getItem(ORIENT_CLAVE) === 'apaisado') guardada = 'apaisado'; } catch (e) {}
        if (guardada === 'apaisado') orientarAfiche('apaisado');

        if (document.fonts && document.fonts.ready) document.fonts.ready.then(ajustarTitulo);
        else ajustarTitulo();

        window.addEventListener('resize', ajustarTitulo);
        document.addEventListener('keydown', function (ev) {
            if (ev.key === 'Escape') cerrarModalQR();
        });
    }

    window.AficheQr = { iniciar: iniciar };

    // Los onclick de la vista llaman estas por nombre.
    window.abrirModalQR    = abrirModalQR;
    window.cerrarModalQR   = cerrarModalQR;
    window.orientarAfiche  = orientarAfiche;
    window.copiar          = copiar;
    window.compartirEnlace = compartirEnlace;
    window.descargarAfiche = descargarAfiche;
    window.imprimirAfiche  = imprimirAfiche;
})();
