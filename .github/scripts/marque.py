#!/usr/bin/env python3
"""Nom et logo de l'application installée (écran d'accueil du téléphone).

Lit le nom et le logo réglés dans Paramètres › Apparence (fonction SQL
public_branding, script 0002) et les écrit dans le site avant sa publication :
manifest.webmanifest, icônes, favicon et titre de la page.

  marque.py empreinte            affiche l'empreinte du nom et du logo
  marque.py appliquer <wwwroot>  écrit le nom et le logo dans le site

Variables : SUPABASE_URL, SUPABASE_ANON_KEY. En cas d'échec (script SQL absent,
réseau…), le site garde le nom et l'icône par défaut : la publication continue.
"""
import base64
import hashlib
import html
import json
import os
import re
import subprocess
import sys
import tempfile
import urllib.request

URL = os.environ.get("SUPABASE_URL", "").rstrip("/")
CLE = os.environ.get("SUPABASE_ANON_KEY", "")
REF = re.compile(r"^gufile:([0-9a-f]{64}):(.+)$")


def requete(chemin, corps=None):
    req = urllib.request.Request(URL + chemin, data=corps, method="POST" if corps is not None else "GET")
    req.add_header("apikey", CLE)
    req.add_header("Authorization", "Bearer " + CLE)
    if corps is not None:
        req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req, timeout=30) as r:
        return r.read()


def lire_marque(strict=False):
    try:
        brut = requete("/rest/v1/rpc/public_branding", b"{}")
        marque = json.loads(brut or b"null") or {}
        return {"nom": (marque.get("nom") or "").strip(), "logo": marque.get("logo") or ""}
    except Exception as e:  # script 0002 absent, réseau…
        if strict:
            raise
        print(f"::warning::Nom et logo de l'application non lus ({e}) : valeurs par défaut.")
        return {"nom": "", "logo": ""}


def empreinte(marque):
    return hashlib.sha256(json.dumps(marque, sort_keys=True).encode()).hexdigest()[:16]


def nom_court(nom):
    if len(nom) <= 12:
        return nom
    premier = nom.split()[0]
    return premier if len(premier) <= 12 else nom[:12]


def lire_logo(logo):
    m = REF.match(logo)
    if m:
        return requete(f"/storage/v1/object/authenticated/fichiers/f/{m.group(1)}")
    if logo.startswith("data:image/") and "," in logo:
        return base64.b64decode(logo.split(",", 1)[1])
    return None


def icone(source, cible, taille, marge=0.8):
    # Fond blanc et marge : l'icône « maskable » peut être rognée en cercle.
    interieur = int(taille * marge)
    subprocess.run(["convert", source + "[0]", "-background", "white", "-alpha", "remove", "-alpha", "off",
                    "-resize", f"{interieur}x{interieur}", "-gravity", "center",
                    "-extent", f"{taille}x{taille}", "-depth", "8", cible], check=True)


def versionner_icones(www, version):
    # Les icônes portent l'empreinte du nom et du logo : un téléphone dont
    # l'ancienne version du site est encore en cache télécharge quand même
    # la nouvelle icône à l'installation.
    chemin = os.path.join(www, "manifest.webmanifest")
    with open(chemin, encoding="utf-8") as f:
        manifeste = json.load(f)
    for i in manifeste.get("icons", []):
        i["src"] = i["src"].split("?")[0] + "?v=" + version
    with open(chemin, "w", encoding="utf-8") as f:
        json.dump(manifeste, f, ensure_ascii=False, indent=2)
    chemin = os.path.join(www, "index.html")
    with open(chemin, encoding="utf-8") as f:
        page = f.read()
    page = re.sub(r'href="(manifest\.webmanifest|favicon\.png|icon-\d+\.png)"', rf'href="\1?v={version}"', page)
    with open(chemin, "w", encoding="utf-8") as f:
        f.write(page)


def appliquer(www, marque):
    version = empreinte(marque)
    with open(os.path.join(www, "marque.txt"), "w") as f:
        f.write(version + "\n")
    versionner_icones(www, version)
    nom = marque["nom"]
    if nom:
        chemin = os.path.join(www, "manifest.webmanifest")
        with open(chemin, encoding="utf-8") as f:
            manifeste = json.load(f)
        manifeste["name"] = nom
        manifeste["short_name"] = nom_court(nom)
        manifeste["description"] = nom
        with open(chemin, "w", encoding="utf-8") as f:
            json.dump(manifeste, f, ensure_ascii=False, indent=2)
        chemin = os.path.join(www, "index.html")
        with open(chemin, encoding="utf-8") as f:
            page = f.read()
        page = re.sub(r"<title>.*?</title>", "<title>" + html.escape(nom) + "</title>", page, count=1)
        with open(chemin, "w", encoding="utf-8") as f:
            f.write(page)
        print(f"Nom de l'application : {nom} (sous l'icône : {nom_court(nom)})")
    try:
        donnees = lire_logo(marque["logo"])
    except Exception as e:
        print(f"::warning::Logo non lu ({e}) : icône par défaut.")
        donnees = None
    if donnees:
        with tempfile.NamedTemporaryFile(suffix=".img", delete=False) as t:
            t.write(donnees)
        try:
            icone(t.name, os.path.join(www, "icon-512.png"), 512)
            icone(t.name, os.path.join(www, "icon-192.png"), 192)
            icone(t.name, os.path.join(www, "favicon.png"), 64, marge=1.0)
            print("Logo de l'application : icônes générées.")
        except Exception as e:
            print(f"::warning::Logo illisible ({e}) : icône par défaut.")
        finally:
            os.unlink(t.name)


if __name__ == "__main__":
    action = sys.argv[1] if len(sys.argv) > 1 else ""
    if not URL or not CLE:
        sys.exit("SUPABASE_URL et SUPABASE_ANON_KEY sont nécessaires.")
    if action == "empreinte":
        # En cas d'échec, pas d'empreinte : la vérification ne relance rien.
        try:
            print(empreinte(lire_marque(strict=True)))
        except Exception as e:
            sys.exit(f"Nom et logo non lus : {e}")
    elif action == "appliquer" and len(sys.argv) > 2:
        appliquer(sys.argv[2], lire_marque())
    else:
        sys.exit(__doc__)
