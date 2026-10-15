// Pont JS pour GestionUtilisateurs (MAUI Blazor Hybrid)
window.guTheme = {
    apply: function (theme, color, appName, logoDataUrl) {
        var root = document.documentElement;
        if (theme === 'dark') root.setAttribute('data-theme', 'dark');
        else root.removeAttribute('data-theme');
        root.style.setProperty('--accent', color || '#4f46e5');
        if (appName) document.title = appName;
        var meta = document.querySelector('meta[name="theme-color"]');
        if (meta) meta.setAttribute('content', color || '#4f46e5');
        if (logoDataUrl) {
            var icon = document.querySelector('link[rel="icon"]');
            if (icon) icon.href = logoDataUrl;
        }
    }
};

window.guNav = {
    _ref: null,
    _bound: false,
    // Retour par balayage vers la droite, partout dans l'application :
    // fling horizontal franc, hors champs de saisie, listes à défilement
    // horizontal, lecteurs média et fenêtres ouvertes (modales, menus, visionneuse).
    enable: function (dotnetRef) {
        window.guNav._ref = dotnetRef;
        if (window.guNav._bound) return;
        window.guNav._bound = true;
        var startX = 0, startY = 0, startT = 0, tracking = false;
        function blocked(target) {
            if (document.documentElement.classList.contains('modal-open')) return true;
            if (document.querySelector('.modal-overlay, .photo-overlay, .more-sheet')) return true;
            if (!target || !target.closest) return false;
            return !!target.closest('input, textarea, select, [contenteditable], ' +
                '.chips-row, .more-sheet-grid, .mpicker-list, video, audio');
        }
        document.addEventListener('touchstart', function (e) {
            if (e.touches.length !== 1) { tracking = false; return; }
            var t = e.touches[0];
            if (blocked(t.target)) { tracking = false; return; }
            startX = t.clientX;
            startY = t.clientY;
            startT = Date.now();
            tracking = true;
        }, { passive: true });
        document.addEventListener('touchend', function (e) {
            if (!tracking || !window.guNav._ref) return;
            tracking = false;
            var t = e.changedTouches[0];
            var dx = t.clientX - startX, dy = t.clientY - startY;
            var dt = Date.now() - startT;
            if (dt < 600 && dx > 90 && Math.abs(dy) < 70 && Math.abs(dx) > 2 * Math.abs(dy)) {
                try { window.guNav._ref.invokeMethodAsync('OnSwipeBack'); } catch (_) { }
            }
        }, { passive: true });
        document.addEventListener('touchcancel', function () { tracking = false; }, { passive: true });
    },
    disable: function () {
        window.guNav._ref = null;
    }
};

window.guDownload = {
    text: function (filename, content, mime) {
        var blob = new Blob([content], { type: mime || 'application/octet-stream' });
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = filename || 'fichier.txt';
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 4000);
    },
    dataUrl: function (filename, dataUrl) {
        var a = document.createElement('a');
        a.href = dataUrl;
        a.download = filename || 'fichier';
        document.body.appendChild(a);
        a.click();
        a.remove();
    }
};

window.guInfo = {
    localBytes: function () {
        try {
            var total = 0;
            for (var i = 0; i < localStorage.length; i++) {
                var k = localStorage.key(i);
                total += k.length + (localStorage.getItem(k) || '').length;
            }
            return total;
        } catch (e) { return 0; }
    }
};

window.guSession = {
    _ref: null,
    _bound: false,
    _last: 0,
    enable: function (dotnetRef) {
        window.guSession._ref = dotnetRef;
        if (!window.guSession._bound) {
            window.guSession._bound = true;
            var notify = function () {
                var now = Date.now();
                // Limite les appels C# : max 1 fois toutes les 20 secondes.
                if (now - window.guSession._last < 20000) return;
                window.guSession._last = now;
                if (window.guSession._ref) {
                    try { window.guSession._ref.invokeMethodAsync('OnUserActivity'); } catch (e) { }
                }
            };
            ['pointerdown', 'keydown', 'touchstart', 'wheel'].forEach(function (evt) {
                document.addEventListener(evt, notify, { passive: true });
            });
        }
    },
    disable: function () {
        window.guSession._ref = null;
    }
};

window.guModal = {
    _n: 0,
    lock: function () {
        window.guModal._n++;
        document.documentElement.classList.add('modal-open');
        document.body.classList.add('modal-open');
    },
    unlock: function () {
        window.guModal._n = Math.max(0, window.guModal._n - 1);
        if (window.guModal._n === 0) {
            document.documentElement.classList.remove('modal-open');
            document.body.classList.remove('modal-open');
        }
    }
};

window.guUi = {
    clearInput: function (id) {
        var el = document.getElementById(id);
        if (el) el.value = '';
    }
};

// Clavier à l'écran : garde toujours visible le champ en cours de saisie
// (scrollIntoView au focus puis à chaque frappe). Avec windowSoftInputMode
// adjustResize, la page est réduite et le champ reste au-dessus du clavier.
window.guScroll = {
    _init: false,
    _view: function () {
        return {
            vh: window.innerHeight || document.documentElement.clientHeight,
            kb: 0
        };
    },
    ensure: function (el) {
        if (!el || typeof el.scrollIntoView !== 'function') return;
        var v = window.guScroll._view();
        var r = el.getBoundingClientRect();
        if (r.top < 0 || r.bottom > v.vh - 8) {
            el.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    },
    init: function () {
        if (window.guScroll._init) return;
        window.guScroll._init = true;
        document.addEventListener('focusin', function (e) {
            var t = e.target;
            if (!t) return;
            var tag = t.tagName;
            if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || t.isContentEditable) {
                var self = t;
                setTimeout(function () { window.guScroll.ensure(self); }, 150);
            }
        }, true);
        document.addEventListener('input', function (e) {
            var t = e.target;
            if (!t) return;
            var tag = t.tagName;
            if (tag === 'INPUT' || tag === 'TEXTAREA' || t.isContentEditable) {
                window.guScroll.ensure(t);
            }
        }, true);
    }
};
if (window.guScroll) { window.guScroll.init(); }