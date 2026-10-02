/* ── Prevalidación: revisar antes de enviar y avisar junto al campo ──────────
   El servidor sigue siendo la autoridad y valida igual. El problema no es que
   valide: es CÓMO llega su rechazo. Los handlers dejan el mensaje en TempData y
   redirigen, así que la pantalla se vuelve a pintar desde la base y todo lo que
   la persona acababa de escribir se perdió —había que teclearlo de nuevo para
   descubrir, quizá, el siguiente error—.

   Esto corre las reglas que se pueden decidir sin consultar nada (una ventana
   que termina antes de empezar, un nombre vacío) ANTES de que salga el POST. Si
   alguna no se cumple no se envía nada: el campo queda marcado y el aviso
   aparece pegado al campo, en el mismo formulario o modal donde se trabaja.

   No reemplaza a la validación nativa del navegador —`required` y `maxlength`
   corren antes que esto—: agrega las reglas que el HTML no sabe expresar.

   Vive en su propio archivo y no en diger.js porque no es un comportamiento de
   la plantilla: es una utilidad que cada formulario engancha a sus reglas. */
(function () {
    'use strict';

    var secuencia = 0;

    /* Dónde cabe el aviso. `.field` y `.edt-campo` son bloques: el <p> entra adentro y queda
       debajo del control, que es donde se lo busca. Si el control vive en una fila flex —el
       nombre del entregable comparte línea con el botón de plegar— meterlo adentro lo pondría
       AL LADO, así que ahí el aviso va después de la fila. */
    function hueco(campo) {
        var caja = campo.closest('.field, .edt-campo');
        if (caja) return { nodo: caja, adentro: true };

        var padre = campo.parentNode;
        var esFila = padre && padre.nodeType === 1 && padre !== campo.form &&
                     /flex/.test(getComputedStyle(padre).display);
        return { nodo: esFila ? padre : campo, adentro: false };
    }

    function limpiarCampo(campo) {
        var id = campo.getAttribute('data-pv-msg');
        campo.classList.remove('pv-invalido');
        campo.removeAttribute('aria-invalid');
        campo.removeAttribute('data-pv-msg');

        var caja = campo.closest('.field, .edt-campo');
        if (caja) caja.classList.remove('pv-con-error');

        if (!id) return;
        var msg = document.getElementById(id);
        if (msg) msg.remove();

        // El aviso puede no ser la única descripción del campo: se saca solo la propia.
        var resto = (campo.getAttribute('aria-describedby') || '').split(' ')
            .filter(function (x) { return x && x !== id; }).join(' ');
        if (resto) campo.setAttribute('aria-describedby', resto);
        else campo.removeAttribute('aria-describedby');
    }

    function marcarCampo(campo, mensaje) {
        limpiarCampo(campo);

        var id  = 'pv-msg-' + (++secuencia);
        var msg = document.createElement('p');
        msg.className = 'pv-msg';
        msg.id = id;
        msg.textContent = mensaje;

        var donde = hueco(campo);
        if (donde.adentro) donde.nodo.appendChild(msg);
        else donde.nodo.insertAdjacentElement('afterend', msg);

        campo.classList.add('pv-invalido');
        campo.setAttribute('aria-invalid', 'true');
        campo.setAttribute('data-pv-msg', id);

        var desc = campo.getAttribute('aria-describedby');
        campo.setAttribute('aria-describedby', desc ? desc + ' ' + id : id);

        var caja = campo.closest('.field, .edt-campo');
        if (caja) caja.classList.add('pv-con-error');
    }

    /* Un campo marcado que no se ve no sirve de nada: el aviso queda escrito en una pestaña
       inactiva o dentro de un grupo plegado y la pantalla parece no responder. Se destapa todo
       lo que lo tiene tapado antes de enfocarlo. */
    function revelar(campo) {
        var panel = campo.closest('.tab-panel');
        if (panel && panel.hidden && typeof window.abrirPestana === 'function')
            window.abrirPestana(panel.getAttribute('data-tab'));

        var det = campo.closest('details');
        while (det) {
            det.open = true;
            det = det.parentNode && det.parentNode.closest ? det.parentNode.closest('details') : null;
        }

        var plegado = campo.closest('.plegado');
        while (plegado) {
            plegado.classList.remove('plegado');
            plegado = plegado.parentNode && plegado.parentNode.closest
                ? plegado.parentNode.closest('.plegado') : null;
        }
    }

    function irAlCampo(campo) {
        revelar(campo);
        // El layout de lo que se acaba de destapar todavía no existe: enfocar en el mismo tick
        // dejaría el scroll en el lugar equivocado.
        setTimeout(function () {
            campo.scrollIntoView({ block: 'center', behavior: 'smooth' });
            campo.focus({ preventScroll: true });
        }, 0);
    }

    /* Los campos que siguen marcados, en el orden en que aparecen en el formulario. Es la fuente
       del resumen: así nunca puede desincronizarse de lo que está pintado. */
    function marcados(form) {
        return [].map.call(form.querySelectorAll('[data-pv-msg]'), function (campo) {
            var msg = document.getElementById(campo.getAttribute('data-pv-msg'));
            return { campo: campo, mensaje: msg ? msg.textContent : '' };
        });
    }

    /* Resumen arriba del formulario. En el editor de proyecto hace falta de verdad: el botón de
       guardar es pegajoso y el campo que falla puede estar en otra pestaña, así que sin esto no
       habría nada que explicara por qué no se guardó. */
    function pintarResumen(form) {
        var caja = null;
        [].forEach.call(form.children, function (hijo) {
            if (!caja && hijo.classList && hijo.classList.contains('pv-resumen')) caja = hijo;
        });

        var pendientes = marcados(form);

        if (!pendientes.length) {
            if (caja) caja.remove();
            return;
        }

        if (!caja) {
            caja = document.createElement('div');
            caja.className = 'alert-error pv-resumen';
            caja.setAttribute('role', 'alert');
            form.insertAdjacentElement('afterbegin', caja);
        }

        caja.textContent = '';
        var titulo = document.createElement('strong');
        titulo.textContent = pendientes.length === 1
            ? 'Hay un dato que corregir antes de guardar:'
            : 'Hay ' + pendientes.length + ' datos que corregir antes de guardar:';
        caja.appendChild(titulo);

        var lista = document.createElement('ul');
        pendientes.forEach(function (p) {
            var li  = document.createElement('li');
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'pv-ir';
            btn.textContent = p.mensaje;
            btn.addEventListener('click', function () { irAlCampo(p.campo); });
            li.appendChild(btn);
            lista.appendChild(li);
        });
        caja.appendChild(lista);
    }

    function alCambiar(form, campo) {
        if (!campo || !campo.getAttribute) return;
        if (!campo.getAttribute('data-pv-msg')) return;
        limpiarCampo(campo);
        pintarResumen(form);
    }

    window._dgPrevalidar = {
        /* Engancha un formulario a sus reglas. Cada regla recibe el formulario y devuelve
           `{campo, mensaje}`, un arreglo de eso, o nada si todo está bien. */
        enlazar: function (form, reglas) {
            if (!form || !reglas || !reglas.length) return;

            form.addEventListener('submit', function (ev) {
                [].forEach.call(form.querySelectorAll('[data-pv-msg]'), limpiarCampo);

                var problemas = [];
                reglas.forEach(function (regla) {
                    var r = regla(form);
                    if (!r) return;
                    (Array.isArray(r) ? r : [r]).forEach(function (p) {
                        if (p && p.campo && p.mensaje) problemas.push(p);
                    });
                });

                if (!problemas.length) { pintarResumen(form); return; }

                // Se corta acá y no se deja burbujear: los listeners globales de `submit`
                // —la barra de progreso— darían por iniciado un envío que no va a ocurrir y la
                // barra quedaría clavada a media pantalla.
                ev.preventDefault();
                ev.stopPropagation();

                problemas.forEach(function (p) { marcarCampo(p.campo, p.mensaje); });
                pintarResumen(form);
                irAlCampo(problemas[0].campo);
            });

            // Corregir un campo le borra su marca en el momento, sin esperar otro envío. No se
            // vuelven a correr todas las reglas: evaluar a cada tecla marcaría de rojo lo que la
            // persona todavía está escribiendo.
            form.addEventListener('input',  function (ev) { alCambiar(form, ev.target); });
            form.addEventListener('change', function (ev) { alCambiar(form, ev.target); });
        },

        /* Regla de par de fechas: `hasta` no puede quedar antes que `desde`. Con una de las dos
           vacía no hay nada que comparar —la ventana abierta es un dato válido—. */
        rangoFechas: function (desde, hasta, mensaje) {
            if (!desde || !hasta || !desde.value || !hasta.value) return null;
            // Las dos vienen de <input type="date">, que siempre vale ISO: comparar como texto
            // es comparar cronológicamente, sin construir Date ni pasar por la zona horaria.
            if (hasta.value >= desde.value) return null;
            return { campo: hasta, mensaje: mensaje };
        },

        /* Regla de campo obligatorio para lo que el servidor rechaza pero el HTML no marca.
           Esos controles no llevan `required` a propósito: el navegador cancelaría el envío con
           su propia burbuja, que no dice de qué fila del árbol está hablando. */
        exigido: function (campo, mensaje) {
            if (!campo || campo.disabled) return null;
            return (campo.value || '').trim() ? null : { campo: campo, mensaje: mensaje };
        }
    };
})();
