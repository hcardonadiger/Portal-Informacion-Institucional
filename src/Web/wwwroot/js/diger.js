/* ── Radio pills — activar clase "on" al seleccionar ──────────────────── */
document.addEventListener('change', function (e) {
    if (e.target.type !== 'radio') return;
    var group = e.target.closest('.rgroup');
    if (!group) return;
    group.querySelectorAll('.rpill').forEach(function (p) { p.classList.remove('on'); });
    e.target.closest('.rpill').classList.add('on');

    // Mostrar/ocultar wraps condicionales
    var toggle   = e.target.dataset.toggle;
    var showVal  = e.target.dataset.showValue;
    if (toggle) {
        var wrap = document.getElementById(toggle);
        if (wrap) wrap.style.display = e.target.value === showVal ? 'block' : 'none';
    }
});

/* ── Agregar fila al equipo dinámicamente ─────────────────────────────── */
var _rowCount = document.querySelectorAll('.equipo-row').length;

document.addEventListener('click', function (e) {
    if (e.target.id !== 'btn-add-row') return;
    var container = document.getElementById('equipo');
    if (!container) return;

    var i   = _rowCount++;
    var div = document.createElement('div');
    div.className = 'row3 equipo-row';
    div.style.marginBottom = '8px';
    div.innerHTML =
        '<input type="text" name="Paso1.EquipoFuncion[' + i + ']" placeholder="Función" class="field-input">' +
        '<input type="text" name="Paso1.EquipoNombre['  + i + ']" placeholder="Nombre completo" class="field-input">' +
        '<input type="text" name="Paso1.EquipoContacto['+ i + ']" placeholder="correo o celular" class="field-input">';
    container.appendChild(div);
});

/* ── Secciones del menú lateral (acordeón) — 2026-10-22 ──────────────────
   Una sola sección abierta a la vez. El estado ES el aria-expanded del título:
   el CSS contrae por ese atributo, así que lo que se ve y lo que oye un lector
   de pantalla no pueden separarse.

   La sección de la página la abre el servidor. Este archivo solo atiende el
   clic y, en las páginas que no pertenecen a ninguna sección —la ayuda, el
   perfil—, repone la última que eligió el usuario.

   Antes los grupos se cerraban con cualquier clic fuera, con Escape y al
   ocultar el cajón: eran desplegables de una barra horizontal que ya no existe.
   En el cajón eso contraería la sección cada vez que alguien toca la página. */
var CLAVE_SECCION = 'diger-menu-seccion';

// localStorage puede no estar —modo privado, almacenamiento bloqueado— o lanzar al
// tocarlo. Sin él solo se pierde el recuerdo: el menú sigue funcionando.
function leerSeccionGuardada() {
    try { return localStorage.getItem(CLAVE_SECCION); } catch (e) { return null; }
}

function guardarSeccion(id) {
    try {
        if (id) localStorage.setItem(CLAVE_SECCION, id);
        else localStorage.removeItem(CLAVE_SECCION);
    } catch (e) { }
}

function toggleNavGroup(btn) {
    var g = btn.closest('.nav-group');
    if (!g) return;
    var abrir = btn.getAttribute('aria-expanded') !== 'true';
    document.querySelectorAll('#mainNav .nav-group-btn[aria-expanded="true"]').forEach(function (b) {
        if (b !== btn) b.setAttribute('aria-expanded', 'false');
    });
    btn.setAttribute('aria-expanded', abrir ? 'true' : 'false');
    // Cerrar también es elegir: quien contrae su sección no la quiere abierta en la
    // próxima página sin sección.
    guardarSeccion(abrir ? g.getAttribute('data-seccion') : null);
}

// Corre al cargar el script, que va al final del <body>: el menú ya está en el DOM y
// así no espera a DOMContentLoaded para abrirse.
(function reponerSeccion() {
    var nav = document.getElementById('mainNav');
    if (!nav || nav.querySelector('.nav-group-btn[aria-expanded="true"]')) return;
    var id = leerSeccionGuardada();
    if (!id) return;
    // Se compara en vez de armar un selector con el valor guardado: lo que venga de
    // localStorage no se mete en un querySelector. Si la sección ya no se ve —le
    // quitaron el permiso— no se encuentra y todo queda contraído.
    var grupos = nav.querySelectorAll('.nav-group[data-seccion]');
    for (var i = 0; i < grupos.length; i++) {
        if (grupos[i].getAttribute('data-seccion') === id) {
            var btn = grupos[i].querySelector('.nav-group-btn');
            if (btn) btn.setAttribute('aria-expanded', 'true');
            return;
        }
    }
})();

/* ── Menú de usuario ───────────────────────────────────────────────────── */
function closeUserPanel() {
    var p = document.getElementById('sideUserPanel');
    if (p) p.classList.remove('open');
}

function toggleUserPanel() {
    var p = document.getElementById('sideUserPanel');
    if (!p) return;
    p.classList.toggle('open');
}

/* ── Conmutador de tema ───────────────────────────────────────────────────
   Este JS NO decide el tema: lo decide el servidor leyendo la cookie, y la
   clase ya viene puesta en el <body> del HTML. Esto solo registra la elección
   y adelanta el efecto para que el botón responda sin recargar.

   La diferencia importa. El modo oscuro que se eliminó el 2026-09-24 ponía el
   tema desde aquí, antes de las hojas de estilo, y cuando el JS no corría el
   registro público de asistencia salía oscuro. Si este archivo no carga, el
   botón no responde y nada más: ninguna página se pinta del color equivocado.

   La cookie no lleva HttpOnly a propósito —la escribe el navegador— y no
   guarda nada sensible: dice "claro" u "oscuro". Lax basta porque solo se lee
   al navegar dentro del portal. */
function cambiarTema() {
    var b = document.body;
    var aOscuro = !b.classList.contains('tema-oscuro');

    b.classList.toggle('tema-oscuro', aOscuro);
    b.classList.toggle('tema-claro', !aOscuro);

    // Un año: es una preferencia, no una sesión.
    document.cookie = 'diger_tema=' + (aOscuro ? 'oscuro' : 'claro')
        + ';path=/;max-age=31536000;samesite=lax';

    // Los <select> y las barras de desplazamiento los pinta el navegador a
    // partir de esto, no del CSS. Sin actualizarlo, al conmutar quedan del
    // tema anterior hasta la siguiente carga.
    var meta = document.querySelector('meta[name="color-scheme"]');
    if (meta) meta.setAttribute('content', aOscuro ? 'dark' : 'light');

    var btn = document.getElementById('temaBtn');
    if (btn) {
        var t = aOscuro ? 'Cambiar a tema claro' : 'Cambiar a tema oscuro';
        btn.setAttribute('title', t);
        btn.setAttribute('aria-label', t);
        btn.setAttribute('aria-pressed', aOscuro ? 'true' : 'false');
    }
}

/* El cajón arranca donde TERMINA la cabecera. Se mide en vez de leer el token
   porque el responsive le cambia el alto, y una medida siempre acierta.

   Hasta el 2026-10-19 esto además corregía un desfase: encima de la cabecera
   iba una franja de gobierno que no era pegajosa, de modo que sin scroll la
   cabecera empezaba 28px más abajo y el cajón, anclado al token, le comía el
   borde inferior a la hamburguesa —justo el botón que vuelve a cerrarlo—. Al
   bajar, la franja se iba y el valor volvía a coincidir solo.

   El 2026-10-19 se quitó esa franja y con ella el listener de scroll, creyendo
   que la cabecera ya empezaba siempre en el borde superior. No era así: encima
   sigue la cinta de entorno («Entorno de pruebas · base …», 22px), que tampoco
   es pegajosa. Medido al cargar, sin scroll, el borde daba 100px; al bajar, la
   cabecera subía a 78 y el cajón se quedaba en 100. Eran los 22px vacíos entre
   la cabecera y el menú, con la página asomando por el hueco.

   Por eso el scroll vuelve, pero barato: un cuadro de animación por lote de
   eventos, y solo con el cajón abierto. El valor deja de cambiar en cuanto la
   cinta sale de la pantalla. */
function ajustarTopDelCajon() {
    var h = document.querySelector('.app-header');
    if (!h) return;
    var y = Math.round(h.getBoundingClientRect().bottom) + 'px';
    var raiz = document.documentElement.style;
    if (raiz.getPropertyValue('--diger-nav-top') !== y) raiz.setProperty('--diger-nav-top', y);
}

var _topDelCajonPendiente = false;
window.addEventListener('scroll', function () {
    if (_topDelCajonPendiente || !cajonAbierto()) return;
    _topDelCajonPendiente = true;
    requestAnimationFrame(function () {
        _topDelCajonPendiente = false;
        ajustarTopDelCajon();
    });
}, { passive: true });

function cajonAbierto() {
    var nav = document.getElementById('mainNav');
    return !!(nav && nav.classList.contains('open'));
}

/* ── ¿El cajón es PASAJERO o está ANCLADO? ────────────────────────────────

   Es la única pregunta de la que cuelga todo el comportamiento nuevo, así que
   vive en un solo lugar y se contesta igual en los tres sitios que la hacen:
   el velo, Escape y el clic fuera.

   PASAJERO (lo de siempre): el cajón flota sobre la página, tiende un velo y
   se va con Escape o con un clic en cualquier parte. Es lo correcto cuando no
   hay ancho que ceder —abajo de 900px el contenido quedaría en una tira— y es
   lo que pasa mientras el menú no esté anclado.

   ANCLADO: el contenido se corrió a su derecha, no hay nada tapado y por lo
   tanto nada que cerrar de apuro. Solo la hamburguesa lo oculta. Que Escape no
   lo cierre es deliberado: tecleando Escape para salir de un desplegable
   cualquiera se habría desanclado el menú sin pedirlo.

   Los 900px son el mismo punto de ruptura que ya usa la cabecera para callar
   el nombre y el rol del usuario; el CSS anula el corrimiento en ese mismo
   ancho, de modo que no hay forma de que JS y CSS discrepen de lado. */
function cajonEsPasajero() {
    var shell = document.querySelector('.app-shell');
    return !shell || !shell.classList.contains('nav-anclado') || window.innerWidth <= 900;
}

/* El velo se pinta solo cuando el cajón TAPA algo. Anclado no tapa: el
   contenido se corrió. Y era precisamente el velo lo que limitaba el uso de
   los módulos —intercepta todo clic sobre la página, así que con el menú
   abierto no se podía trabajar—.

   Se sincroniza en vez de fijarse al abrir porque el ancho cambia sin que
   nadie toque el botón: girar una tableta pasa de anclado a pasajero y el velo
   tiene que aparecer, o el cajón se queda tapando contenido sin forma de
   cerrarlo con un clic fuera. */
function sincronizarVelo() {
    var bd = document.getElementById('navBackdrop');
    if (!bd) return;
    bd.classList.toggle('open', cajonAbierto() && cajonEsPasajero());
}

window.addEventListener('resize', function () {
    if (cajonAbierto()) {
        ajustarTopDelCajon();
        sincronizarVelo();
    }
});

/* El cajón anclado llega abierto desde el servidor, así que al cargar hay dos
   cosas que poner al día y que ningún clic va a disparar: la medida del borde
   inferior real de la cabecera —el CSS trae el token como respaldo, pero la
   medida acierta también cuando el responsive le cambia el alto— y el velo,
   que depende del ancho y el servidor no lo conoce. */
document.addEventListener('DOMContentLoaded', function () {
    if (cajonAbierto()) {
        ajustarTopDelCajon();
        sincronizarVelo();
    }
});

/* Menú principal: se ancla y se oculta con el mismo botón.
 *
 * La cookie no lleva HttpOnly a propósito —la escribe el navegador— y no
 * guarda nada sensible: dice "anclado" u "oculto". Un año, porque es una
 * preferencia y no una sesión. El servidor la lee en _Layout.cshtml y pinta la
 * clase, así que el anclaje sobrevive a la recarga del próximo módulo sin que
 * el contenido dé un salto. Si este archivo no carga, el botón no responde y
 * nada más: ninguna página se pinta corrida de más ni de menos. */
function toggleMainNav(open) {
    var nav = document.getElementById('mainNav');
    var shell = document.querySelector('.app-shell');
    var btn = document.getElementById('navToggle');
    if (!nav) return;
    if (typeof open === 'undefined') open = !nav.classList.contains('open');

    var yaEstaba = nav.classList.contains('open');
    // Medir ANTES de mostrarlo: si se mide después, el cajón ya se dibujó una
    // vez en la posición vieja y se ve el salto.
    if (open) ajustarTopDelCajon();
    nav.classList.toggle('open', open);
    // El shell primero y el velo después: sincronizarVelo pregunta por esta
    // clase, así que leerla antes de moverla daría la respuesta vieja.
    if (shell) shell.classList.toggle('nav-anclado', open);
    sincronizarVelo();

    document.cookie = 'diger_menu=' + (open ? 'anclado' : 'oculto')
        + ';path=/;max-age=31536000;samesite=lax';

    // El botón tiene que DECIR si está abierto: con el menú en cajón a
    // cualquier ancho, un lector de pantalla no tiene otra forma de saberlo.
    // El rótulo nombra lo que el botón HACE, y anclar no es lo mismo que
    // abrir: dice que el menú se queda.
    if (btn) {
        btn.setAttribute('aria-expanded', open ? 'true' : 'false');
        btn.setAttribute('aria-label', open ? 'Ocultar menú' : 'Anclar menú');
    }

    // El foco sigue al menú y vuelve al botón al cerrar. Sin esto, abrir con
    // el teclado deja el foco detrás del cajón y hay que tabular a ciegas.
    // Solo si el estado cambió de verdad: toggleMainNav(false) se llama en
    // cada Escape y en cada clic fuera, y robaría el foco sin motivo.
    if (open && !yaEstaba) {
        var primero = nav.querySelector('button, [href]');
        if (primero) primero.focus();
    } else if (!open && yaEstaba && btn) {
        btn.focus();
    }
}

document.addEventListener('click', function (e) {
    var panel = document.getElementById('sideUserPanel');
    if (panel && panel.classList.contains('open') && !e.target.closest('.user-menu')) {
        panel.classList.remove('open');
    }
});

document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
        closeUserPanel();
        // Solo si el cajón es pasajero. Anclado, Escape lo habría desanclado cada
        // vez que alguien lo teclea para salir de un desplegable, y volver a
        // anclarlo sería trabajo que el usuario no pidió.
        if (cajonEsPasajero()) toggleMainNav(false);
    }
});

/* ── seg-upd: dropdown Actualizar (position:fixed para escapar overflow:hidden) ─ */
(function () {
    function posicionar(det) {
        var form    = det.querySelector('.seg-upd-form');
        var summary = det.querySelector('summary');
        if (!form || !summary) return;
        var r = summary.getBoundingClientRect();
        form.style.position = 'fixed';
        form.style.zIndex   = '9999';
        form.style.top      = (r.bottom + 6) + 'px';
        form.style.left     = 'auto';
        var right = Math.max(8, window.innerWidth - r.right);
        form.style.right = right + 'px';
    }

    document.addEventListener('toggle', function (e) {
        var det = e.target;
        if (!(det instanceof HTMLDetailsElement) || !det.classList.contains('seg-upd')) return;
        if (!det.open) return;
        document.querySelectorAll('details.seg-upd[open]').forEach(function (other) {
            if (other !== det) other.removeAttribute('open');
        });
        posicionar(det);
    }, true);

    window.addEventListener('scroll', function () {
        document.querySelectorAll('details.seg-upd[open]').forEach(posicionar);
    }, { passive: true, capture: true });

    window.addEventListener('resize', function () {
        document.querySelectorAll('details.seg-upd[open]').forEach(posicionar);
    });
})();

/* ── Barra de progreso global (QW1) ───────────────────────────────────── */
(function(){
    var bar = document.createElement('div');
    bar.className = 'dg-progress';
    document.body.appendChild(bar);

    window._dgProgress = {
        start: function(){
            bar.style.transition = 'none';
            bar.style.width = '0';
            bar.offsetWidth;
            bar.classList.add('active');
            bar.style.transition = 'width 8s cubic-bezier(.1,.7,.3,1)';
            bar.style.width = '85%';
        },
        done: function(){
            bar.style.transition = 'width .2s ease';
            bar.style.width = '100%';
            setTimeout(function(){ bar.classList.remove('active'); bar.style.width = '0'; }, 250);
        }
    };

    document.addEventListener('submit', function(e){
        var form = e.target;
        if(form.method && form.method.toLowerCase() === 'get') return;
        if(form.dataset.noProgress) return;
        window._dgProgress.start();
    });

    var _origSubmit = HTMLFormElement.prototype.submit;
    var selects = document.querySelectorAll('select[onchange*="this.form.submit"]');
    selects.forEach(function(sel){
        sel.addEventListener('change', function(){ window._dgProgress.start(); });
    });
})();

/* ── Debounce para filtros de texto (QW2) ─────────────────────────────── */
(function(){
    var timers = {};
    document.querySelectorAll('input[type="search"], .seg-filters input[type="text"]').forEach(function(inp){
        var form = inp.closest('form');
        if(!form) return;
        inp.addEventListener('input', function(){
            var key = inp.name || inp.id || 'default';
            clearTimeout(timers[key]);
            timers[key] = setTimeout(function(){
                if(typeof inp._dgFilter === 'function') inp._dgFilter();
            }, 300);
        });
    });

    window._dgDebounce = function(fn, ms){
        var t;
        return function(){
            var ctx = this, args = arguments;
            clearTimeout(t);
            t = setTimeout(function(){ fn.apply(ctx, args); }, ms || 300);
        };
    };
})();

/* ── Modal de confirmación estilizado (QW5) ───────────────────────────── */
(function(){
    var overlay = document.createElement('div');
    overlay.className = 'dg-confirm-overlay';
    overlay.innerHTML =
        '<div class="dg-confirm-box">'
        + '<div class="dg-confirm-icon">⚠</div>'
        + '<div class="dg-confirm-msg"></div>'
        + '<div class="dg-confirm-btns">'
          + '<button class="dg-confirm-cancel" type="button">Cancelar</button>'
          + '<button class="dg-confirm-ok" type="button">Confirmar</button>'
        + '</div>'
      + '</div>';
    document.body.appendChild(overlay);

    var msgEl = overlay.querySelector('.dg-confirm-msg');
    var okBtn = overlay.querySelector('.dg-confirm-ok');
    var cancelBtn = overlay.querySelector('.dg-confirm-cancel');
    var _resolve = null;
    var _pendingForm = null;
    var _pendingSubmitter = null;

    function showConfirm(msg){
        msgEl.textContent = msg;
        overlay.classList.add('open');
        okBtn.focus();
        return new Promise(function(resolve){ _resolve = resolve; });
    }

    function close(result){
        overlay.classList.remove('open');
        if(_resolve) _resolve(result);
        _resolve = null;
    }

    okBtn.addEventListener('click', function(){
        close(true);
        if(_pendingForm){
            var form = _pendingForm;
            var submitter = _pendingSubmitter;
            _pendingForm = null;
            _pendingSubmitter = null;
            if(submitter && submitter.name){
                var hidden = document.createElement('input');
                hidden.type = 'hidden';
                hidden.name = submitter.name;
                hidden.value = submitter.value || '';
                form.appendChild(hidden);
            }
            form.submit();
        }
    });
    cancelBtn.addEventListener('click', function(){ _pendingForm = null; _pendingSubmitter = null; close(false); });
    overlay.addEventListener('click', function(e){ if(e.target === overlay){ _pendingForm = null; _pendingSubmitter = null; close(false); }});
    document.addEventListener('keydown', function(e){
        if(e.key === 'Escape' && overlay.classList.contains('open')){ _pendingForm = null; _pendingSubmitter = null; close(false); }
    });

    document.addEventListener('submit', function(e){
        var btn = e.submitter;
        if(!btn) return;
        var msg = btn.dataset.confirm;
        if(!msg && btn.classList.contains('hist-del')){
            msg = '¿Eliminar este elemento? Esta acción no se puede deshacer.';
        }
        if(msg){
            e.preventDefault();
            _pendingForm = e.target;
            _pendingSubmitter = btn;
            showConfirm(msg);
        }
    });

    window._dgConfirm = showConfirm;
})();


/* ── Paginación de cliente para listas largas ──────────────────────────────
   Gobierna cualquier contenedor con data-pg="<id>" desde el control
   _TablaPaginada que lleva data-pg-for="<id>". Sirve igual a un <tbody> de
   <tr> que a un <div> de tarjetas: los ítems son los hijos directos.

   OCULTA CON hidden, NUNCA SACA DEL DOM. Las filas del tablero de trámites
   llevan onclick inline y las claves de sus modales; desmontarlas y
   reponerlas rompería esos modales. Además, ocultar deja el CSV y el
   "Ctrl+F" del navegador operando sobre lo que el servidor mandó.

   El tamaño elegido no se guarda: al recargar vuelve al de arranque. Fue una
   decisión explícita — todos ven lo mismo al entrar. */
(function () {
    'use strict';

    var VENTANA = 2;   // páginas mostradas a cada lado de la actual

    function itemsDe(cont) {
        return Array.prototype.filter.call(cont.children, function (el) {
            // Deja fuera la fila de "no hay resultados", que no es un ítem que paginar.
            return !el.hasAttribute('data-pg-omitir');
        });
    }

    function boton(txt, clase) {
        var b = document.createElement('button');
        b.type = 'button';
        b.className = 'pager-b' + (clase ? ' ' + clase : '');
        b.textContent = txt;
        return b;
    }

    function montar(ctrl) {
        var cont = document.querySelector('[data-pg="' + ctrl.dataset.pgFor + '"]');
        if (!cont) return;

        var items  = itemsDe(cont);
        var total  = items.length;
        var minimo = parseInt(ctrl.dataset.pgDefecto, 10) || 10;

        var info   = ctrl.querySelector('[data-pg-info]');
        var nav    = ctrl.querySelector('[data-pg-nav]');
        var selSz  = ctrl.querySelector('[data-pg-size]');

        var pagina = 1;
        var tam    = minimo;

        selSz.value = String(minimo);

        function pintar() {
            // 0 = "Todos". Con la lista vacía el tamaño no puede ser 0: dividir por cero
            // dejaría totalPaginas en Infinity y el bucle de botones no terminaría.
            var t       = tam === 0 ? Math.max(total, 1) : tam;
            var paginas = Math.max(1, Math.ceil(total / t));
            if (pagina > paginas) pagina = paginas;

            var desde = (pagina - 1) * t;
            var hasta = Math.min(desde + t, total);

            items.forEach(function (el, i) {
                if (i >= desde && i < hasta) el.removeAttribute('hidden');
                else                         el.setAttribute('hidden', '');
            });

            info.textContent = total === 0
                ? 'Sin registros'
                : (desde + 1) + '\u2013' + hasta + ' de ' + total;

            nav.textContent = '';
            if (paginas <= 1) return;

            var ini = Math.max(1, pagina - VENTANA);
            var fin = Math.min(paginas, pagina + VENTANA);

            var prev = boton('\u2190', pagina > 1 ? '' : 'off');
            prev.setAttribute('aria-label', 'Anterior');
            prev.addEventListener('click', function () { pagina--; pintar(); });
            nav.appendChild(prev);

            if (ini > 1) {
                nav.appendChild(irA(1));
                if (ini > 2) nav.appendChild(puntos());
            }
            for (var p = ini; p <= fin; p++) nav.appendChild(irA(p));
            if (fin < paginas) {
                if (fin < paginas - 1) nav.appendChild(puntos());
                nav.appendChild(irA(paginas));
            }

            var next = boton('\u2192', pagina < paginas ? '' : 'off');
            next.setAttribute('aria-label', 'Siguiente');
            next.addEventListener('click', function () { pagina++; pintar(); });
            nav.appendChild(next);

            function irA(p) {
                var b = boton(String(p), p === pagina ? 'on' : '');
                b.addEventListener('click', function () { pagina = p; pintar(); });
                return b;
            }
            function puntos() {
                var s = document.createElement('span');
                s.className = 'pager-dots';
                s.textContent = '\u2026';
                return s;
            }
        }

        selSz.addEventListener('change', function () {
            tam = parseInt(selSz.value, 10);
            // Volver a la primera: quedarse en la página 7 tras pasar de 10 a 100 deja al
            // usuario mirando una lista vacía sin entender por qué.
            pagina = 1;
            pintar();
        });

        // Sobre una lista que cabe entera en la primera página el control no aporta nada.
        if (total <= minimo) return;

        ctrl.removeAttribute('hidden');
        pintar();
    }

    // diger.js se carga hoy después de @RenderBody, así que las listas ya están. La guarda es
    // para que mover el <script> en el layout no rompa la paginación en silencio.
    function iniciar() {
        document.querySelectorAll('.pager-cli[data-pg-for]').forEach(montar);
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar);
    else iniciar();
})();
