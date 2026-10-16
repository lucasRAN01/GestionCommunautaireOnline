-- Écran de connexion : nom et logo de la communauté visibles avant la connexion.
-- Seuls ces deux réglages (et le fichier du logo) sont lisibles sans compte ;
-- tout le reste demande toujours un compte validé.
-- À exécuter une fois dans Supabase : SQL Editor > New query > coller > Run.

create or replace function public.public_branding()
returns jsonb language sql stable security definer set search_path = public as $$
    select jsonb_build_object(
        'nom',  coalesce(a.value ->> 'appName', ''),
        'logo', coalesce(a.value ->> 'logo', ''))
    from public.app_data a
    where a.key = 'gu2:settings';
$$;

-- Le logo est rangé dans les fichiers : seul ce fichier-là est lisible sans compte.
create or replace function public.gu_logo_object()
returns text language sql stable security definer set search_path = public as $$
    select 'f/' || split_part(a.value ->> 'logo', ':', 2)
    from public.app_data a
    where a.key = 'gu2:settings' and a.value ->> 'logo' like 'gufile:%';
$$;

revoke execute on function public.public_branding(), public.gu_logo_object() from public;
grant execute on function public.public_branding(), public.gu_logo_object() to anon, authenticated;

drop policy if exists gu_fichiers_logo_public on storage.objects;
create policy gu_fichiers_logo_public on storage.objects for select to anon
    using (bucket_id = 'fichiers' and name = public.gu_logo_object());
