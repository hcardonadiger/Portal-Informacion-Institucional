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

/* ── Mobile Navbar Toggle & Dropdowns ── */
document.addEventListener('click', function(e) {
    // Hamburger toggle
    var toggler = e.target.closest('#navbarToggler');
    if (toggler) {
        var menu = document.getElementById('navbarMenu');
        if (menu) menu.classList.toggle('open');
        return;
    }

    // Dropdown toggle on mobile
    var navLink = e.target.closest('.nav-item.has-dropdown > .nav-link');
    if (navLink) {
        if (window.innerWidth <= 900) {
            e.preventDefault();
            var parent = navLink.closest('.nav-item');
            parent.classList.toggle('open');
        }
        return;
    }

    // Close menu if clicking outside on mobile
    var menu = document.getElementById('navbarMenu');
    if (menu && menu.classList.contains('open') && !e.target.closest('.main-navbar')) {
        menu.classList.remove('open');
    }
});

document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
        var menu = document.getElementById('navbarMenu');
        if (menu) menu.classList.remove('open');
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
