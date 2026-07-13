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

/* ── Sidebar tipo hamburguesa (todos los tamaños) + popover de usuario ── */
var SIDEBAR_KEY = 'diger-sidebar-open';

function toggleSidebar(open) {
    var sb = document.getElementById('appSidebar');
    var bd = document.getElementById('sidebarBackdrop');
    if (!sb) return;
    sb.classList.toggle('open', open);
    if (bd) bd.classList.toggle('open', open);
    try { localStorage.setItem(SIDEBAR_KEY, open ? 'true' : 'false'); } catch (e) { }
}

function toggleUserPanel() {
    var p = document.getElementById('sideUserPanel');
    if (p) p.classList.toggle('open');
}

/* Restaura el último estado (abierto/cerrado) sin animar el primer render */
(function () {
    var sb = document.getElementById('appSidebar');
    if (!sb) return;
    var open;
    try { open = localStorage.getItem(SIDEBAR_KEY) === 'true'; } catch (e) { open = false; }
    if (open) {
        var bd = document.getElementById('sidebarBackdrop');
        sb.classList.add('no-anim', 'open');
        if (bd) bd.classList.add('open');
        requestAnimationFrame(function () {
            requestAnimationFrame(function () { sb.classList.remove('no-anim'); });
        });
    }
})();

document.addEventListener('click', function (e) {
    var panel = document.getElementById('sideUserPanel');
    if (panel && panel.classList.contains('open') && !e.target.closest('.side-user')) {
        panel.classList.remove('open');
    }
});

document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
        toggleSidebar(false);
        var panel = document.getElementById('sideUserPanel');
        if (panel) panel.classList.remove('open');
    }
});

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
