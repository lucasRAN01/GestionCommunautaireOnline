// Mises à jour du site : le service worker garde l'ancienne version en cache
// tant que tous les onglets ne sont pas fermés. Dès qu'une nouvelle version
// est téléchargée, un bandeau propose de l'installer tout de suite.
(function () {
    if (!('serviceWorker' in navigator)) return;

    var reloading = false;
    navigator.serviceWorker.addEventListener('controllerchange', function () {
        if (reloading) return;
        reloading = true;
        window.location.reload();
    });

    function offer(worker) {
        // Première visite : rien à remplacer, la version installée est déjà la bonne.
        if (!worker || !navigator.serviceWorker.controller) return;
        if (document.getElementById('gu-update')) return;
        var bar = document.createElement('div');
        bar.id = 'gu-update';
        bar.className = 'gu-update';
        bar.setAttribute('role', 'status');
        var text = document.createElement('span');
        text.textContent = 'Nouvelle version disponible.';
        var button = document.createElement('button');
        button.type = 'button';
        button.textContent = 'Mettre à jour';
        button.addEventListener('click', function () {
            button.disabled = true;
            button.textContent = 'Mise à jour…';
            worker.postMessage('skipWaiting');
        });
        bar.appendChild(text);
        bar.appendChild(button);
        document.body.appendChild(bar);
    }

    function watch(reg) {
        if (reg.waiting) offer(reg.waiting);
        reg.addEventListener('updatefound', function () {
            var worker = reg.installing;
            if (!worker) return;
            worker.addEventListener('statechange', function () {
                if (worker.state === 'installed') offer(worker);
            });
        });
        // Une page restée ouverte longtemps vérifie aussi les mises à jour.
        setInterval(function () { reg.update().catch(function () { }); }, 30 * 60 * 1000);
    }

    navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' })
        .then(watch)
        .catch(function () { });
})();
