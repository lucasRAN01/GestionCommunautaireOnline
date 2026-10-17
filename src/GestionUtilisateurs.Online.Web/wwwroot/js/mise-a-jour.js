// Mises à jour du site : le service worker garde l'ancienne version en cache
// tant que tous les onglets ne sont pas fermés. La mise à jour est obligatoire :
// au démarrage elle s'installe aussitôt ; en cours d'utilisation, un écran
// bloque l'application jusqu'à la mise à jour (automatique après un délai).
(function () {
    if (!('serviceWorker' in navigator)) return;

    var DELAI_AUTO = 15;
    var TEXTES = {
        fr: { titre: 'Nouvelle version disponible', texte: "Mettez à jour pour continuer à utiliser l'application.", bouton: 'Mettre à jour', auto: 'Mise à jour automatique dans {0} s', encours: 'Mise à jour…' },
        en: { titre: 'New version available', texte: 'Update to keep using the app.', bouton: 'Update', auto: 'Updating automatically in {0} s', encours: 'Updating…' },
        mg: { titre: 'Misy dikan-teny vaovao', texte: 'Havaozy mba hanohizana ny fampiasana ny rindranasa.', bouton: 'Havaozy', auto: 'Havaozina ho azy afaka {0} s', encours: 'Fanavaozana…' }
    };

    function textes() {
        var code = null;
        try { code = localStorage.getItem('gu2:langue'); } catch (e) { }
        return TEXTES[code] || TEXTES.fr;
    }

    var reloading = false;
    function recharger() {
        if (reloading) return;
        reloading = true;
        window.location.reload();
    }
    navigator.serviceWorker.addEventListener('controllerchange', recharger);

    var appliquee = false;
    function appliquer(worker) {
        if (appliquee) return;
        appliquee = true;
        // Le rechargement qui suit garde un écran de chargement court.
        try { sessionStorage.setItem('gu2:chargement-court', '1'); } catch (e) { }
        worker.postMessage('skipWaiting');
        // Sécurité : si le changement de version ne se signale pas, on recharge quand même.
        setTimeout(recharger, 8000);
    }

    function demarrageEnCours() {
        var s = document.getElementById('app-splash');
        return !!s && !s.classList.contains('splash-hide');
    }

    function imposer(worker) {
        // Première visite : rien à remplacer, la version installée est déjà la bonne.
        if (!worker || !navigator.serviceWorker.controller) return;
        // Pendant l'écran de chargement, personne n'utilise encore l'application.
        if (demarrageEnCours()) { appliquer(worker); return; }
        if (document.getElementById('gu-update')) return;

        var t = textes();
        var ecran = document.createElement('div');
        ecran.id = 'gu-update';
        ecran.className = 'gu-update';
        ecran.setAttribute('role', 'alertdialog');
        ecran.setAttribute('aria-modal', 'true');
        ecran.setAttribute('aria-labelledby', 'gu-update-titre');

        var carte = document.createElement('div');
        carte.className = 'gu-update-card';
        var titre = document.createElement('h2');
        titre.id = 'gu-update-titre';
        titre.textContent = t.titre;
        var texte = document.createElement('p');
        texte.textContent = t.texte;
        var bouton = document.createElement('button');
        bouton.type = 'button';
        bouton.textContent = t.bouton;
        var compte = document.createElement('p');
        compte.className = 'gu-update-auto';

        var reste = DELAI_AUTO;
        var minuteur = null;
        function lancer() {
            if (minuteur) clearInterval(minuteur);
            bouton.disabled = true;
            bouton.textContent = t.encours;
            compte.textContent = '';
            appliquer(worker);
        }
        function afficherReste() { compte.textContent = t.auto.replace('{0}', reste); }
        afficherReste();
        minuteur = setInterval(function () {
            reste--;
            if (reste <= 0) lancer(); else afficherReste();
        }, 1000);
        bouton.addEventListener('click', lancer);

        carte.appendChild(titre);
        carte.appendChild(texte);
        carte.appendChild(bouton);
        carte.appendChild(compte);
        ecran.appendChild(carte);
        document.body.appendChild(ecran);
        // L'écran bloque tout : ni la souris ni le clavier n'atteignent l'application derrière.
        Array.prototype.forEach.call(document.body.children, function (el) {
            if (el !== ecran) el.setAttribute('inert', '');
        });
        bouton.focus();
    }

    function watch(reg) {
        if (reg.waiting) imposer(reg.waiting);
        reg.addEventListener('updatefound', function () {
            var worker = reg.installing;
            if (!worker) return;
            worker.addEventListener('statechange', function () {
                if (worker.state === 'installed') imposer(worker);
            });
        });
        function verifier() { reg.update().catch(function () { }); }
        // Une page restée ouverte longtemps vérifie aussi les mises à jour,
        // ainsi qu'une application installée qu'on rouvre depuis l'arrière-plan.
        setInterval(verifier, 30 * 60 * 1000);
        document.addEventListener('visibilitychange', function () {
            if (document.visibilityState === 'visible') verifier();
        });
    }

    navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' })
        .then(watch)
        .catch(function () { });
})();
