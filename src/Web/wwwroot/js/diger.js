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

/* ── Confirmar antes de enviar (data-confirm="mensaje" en el botón) ──── */
document.addEventListener('submit', function (e) {
    var btn = e.submitter;
    if (!btn) return;
    var msg = btn.dataset.confirm;
    if (msg) {
        if (!confirm(msg)) e.preventDefault();
        return;
    }
    if (btn.classList.contains('hist-del')) {
        if (!confirm('¿Eliminar este levantamiento? Esta acción no se puede deshacer.')) {
            e.preventDefault();
        }
    }
});
