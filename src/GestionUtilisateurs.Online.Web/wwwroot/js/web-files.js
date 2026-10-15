// Fichiers côté navigateur pour la version en ligne :
// sélection (galerie, appareil photo, documents), compression des photos,
// téléchargement et ouverture. Remplace les services natifs Android.
window.guWebFiles = (function () {
    // Les photos sont réduites avant l'envoi pour économiser le stockage
    // Supabase (1 Go gratuit) et le transfert mensuel (5 Go gratuits).
    var MAX_SIDE = 1600;
    var JPEG_QUALITY = 0.8;

    function readAsDataUrl(file) {
        return new Promise(function (resolve, reject) {
            var reader = new FileReader();
            reader.onload = function () { resolve(reader.result); };
            reader.onerror = function () { reject(reader.error); };
            reader.readAsDataURL(file);
        });
    }

    function loadImage(dataUrl) {
        return new Promise(function (resolve, reject) {
            var img = new Image();
            img.onload = function () { resolve(img); };
            img.onerror = reject;
            img.src = dataUrl;
        });
    }

    function compressible(type) {
        return /^image\/(jpeg|png|webp|heic|heif|bmp)$/i.test(type || '');
    }

    // Réduit une image (côté le plus long ≤ 1600 px) et la ré-encode en JPEG.
    // Garde l'original s'il est déjà plus léger que la version compressée.
    async function compressImage(file, dataUrl) {
        try {
            var img = await loadImage(dataUrl);
            var w = img.naturalWidth, h = img.naturalHeight;
            if (!w || !h) return null;
            var scale = Math.min(1, MAX_SIDE / Math.max(w, h));
            var canvas = document.createElement('canvas');
            canvas.width = Math.round(w * scale);
            canvas.height = Math.round(h * scale);
            var ctx = canvas.getContext('2d');
            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
            var out = canvas.toDataURL('image/jpeg', JPEG_QUALITY);
            var outSize = Math.round((out.length - out.indexOf(',') - 1) * 3 / 4);
            if (outSize >= file.size && scale === 1) return null;
            var name = (file.name || 'photo').replace(/\.[^.]+$/, '') + '.jpg';
            return { name: name, type: 'image/jpeg', size: outSize, dataUrl: out };
        } catch (e) {
            return null;
        }
    }

    function pickFile(accept, capture) {
        return new Promise(function (resolve) {
            var input = document.createElement('input');
            input.type = 'file';
            if (accept) input.accept = accept;
            if (capture) input.setAttribute('capture', capture);
            input.style.display = 'none';
            var done = false;
            function finish(file) {
                if (done) return;
                done = true;
                input.remove();
                resolve(file);
            }
            input.addEventListener('change', function () {
                finish(input.files && input.files.length ? input.files[0] : null);
            });
            // Annulation de la boîte de dialogue (navigateurs récents).
            input.addEventListener('cancel', function () { finish(null); });
            document.body.appendChild(input);
            input.click();
        });
    }

    function formatSize(bytes) {
        if (bytes < 1024) return bytes + ' o';
        if (bytes < 1024 * 1024) return Math.round(bytes / 1024) + ' Ko';
        return (Math.round(bytes / (1024 * 1024) * 10) / 10).toString().replace('.', ',') + ' Mo';
    }

    // Renvoie { name, type, size, dataUrl } ou { error } ou null (annulé).
    async function pick(accept, capture, maxBytes, label, compress) {
        var file = await pickFile(accept, capture);
        if (!file) return null;
        if (!file.size) return { error: 'Le fichier est vide.' };
        var dataUrl = await readAsDataUrl(file);
        var result = { name: file.name, type: file.type || '', size: file.size, dataUrl: dataUrl };
        if (compress && compressible(file.type)) {
            var small = await compressImage(file, dataUrl);
            if (small) result = small;
        }
        if (maxBytes && result.size > maxBytes) {
            return { error: (label || 'Fichier') + ' refusé (' + formatSize(result.size) + ') : taille maximale de ' + formatSize(maxBytes) + '.' };
        }
        return result;
    }

    function toBlob(base64, mime) {
        var bin = atob(base64);
        var bytes = new Uint8Array(bin.length);
        for (var i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
        return new Blob([bytes], { type: mime || 'application/octet-stream' });
    }

    function save(fileName, base64, mime) {
        var url = URL.createObjectURL(toBlob(base64, mime));
        var a = document.createElement('a');
        a.href = url;
        a.download = fileName || 'fichier';
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 10000);
        return true;
    }

    function open(fileName, base64, mime) {
        var url = URL.createObjectURL(toBlob(base64, mime));
        var win = window.open(url, '_blank', 'noopener');
        if (!win) return save(fileName, base64, mime);
        setTimeout(function () { URL.revokeObjectURL(url); }, 60000);
        return true;
    }

    async function share(files, title) {
        try {
            var list = files.map(function (f) { return new File([toBlob(f.base64, f.mime)], f.name, { type: f.mime }); });
            if (navigator.canShare && navigator.canShare({ files: list })) {
                await navigator.share({ files: list, title: title });
                return true;
            }
        } catch (e) {
            if (e && e.name === 'AbortError') return true;
        }
        files.forEach(function (f) { save(f.name, f.base64, f.mime); });
        return true;
    }

    return { pick: pick, save: save, open: open, share: share };
})();

// Impression en PDF (carte de membre) : @page à la taille exacte de la carte,
// marges à 0, pour éviter les en-têtes et pieds de page du navigateur.
window.guWebPrint = {
    printHtml: function (pagesHtml, fileName, pageWidthPt, pageHeightPt) {
        try {
            var w = window.open('', '_blank', 'width=900,height=700');
            if (!w) return false;
            var hasSize = pageWidthPt > 0 && pageHeightPt > 0;
            var pageRule = hasSize
                ? '@page{size:' + Math.round(pageWidthPt) + 'pt ' + Math.round(pageHeightPt) + 'pt;margin:0}'
                : '@page{size:auto;margin:0}';
            var css = '<style>' + pageRule +
                'html,body{margin:0 !important;padding:0 !important;background:#fff}' +
                'body{font-family:Segoe UI,system-ui,sans-serif;-webkit-print-color-adjust:exact;print-color-adjust:exact}' +
                '.print-page{margin:0;padding:0;page-break-after:always;overflow:hidden}' +
                '.print-page:last-child{page-break-after:auto}' +
                'img{max-width:100%}</style>';
            var body = '';
            for (var i = 0; i < pagesHtml.length; i++) body += '<div class="print-page">' + pagesHtml[i] + '</div>';
            var title = String(fileName || 'document').replace(/[<>&"]/g, '');
            w.document.write('<!DOCTYPE html><html lang="fr"><head><meta charset="utf-8"><title>' +
                title + '</title>' + css + '</head><body>' + body + '</body></html>');
            w.document.close();
            w.focus();
            setTimeout(function () { try { w.print(); } catch (e) { } }, 600);
            return true;
        } catch (e) { return false; }
    }
};
