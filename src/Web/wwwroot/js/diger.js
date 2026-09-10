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

/* ── Navegación de cabecera: grupos desplegables + menú de usuario ────── */
function closeAllNavGroups(except) {
    document.querySelectorAll('.nav-group.open').forEach(function (g) {
        if (g !== except) g.classList.remove('open');
    });
}

function closeUserPanel() {
    var p = document.getElementById('sideUserPanel');
    if (p) p.classList.remove('open');
}

function toggleNavGroup(btn) {
    var g = btn.closest('.nav-group');
    if (!g) return;
    var willOpen = !g.classList.contains('open');
    closeAllNavGroups(g);
    closeUserPanel();
    g.classList.toggle('open', willOpen);
}

function toggleUserPanel() {
    var p = document.getElementById('sideUserPanel');
    if (!p) return;
    closeAllNavGroups();
    p.classList.toggle('open');
}

/* Menú principal en pantallas pequeñas (hamburguesa) */
function toggleMainNav(open) {
    var nav = document.getElementById('mainNav');
    var bd = document.getElementById('navBackdrop');
    if (!nav) return;
    if (typeof open === 'undefined') open = !nav.classList.contains('open');
    nav.classList.toggle('open', open);
    if (bd) bd.classList.toggle('open', open);
    if (!open) closeAllNavGroups();
}

document.addEventListener('click', function (e) {
    if (!e.target.closest('.nav-group')) closeAllNavGroups();
    var panel = document.getElementById('sideUserPanel');
    if (panel && panel.classList.contains('open') && !e.target.closest('.user-menu')) {
        panel.classList.remove('open');
    }
});

document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
        closeAllNavGroups();
        closeUserPanel();
        toggleMainNav(false);
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
