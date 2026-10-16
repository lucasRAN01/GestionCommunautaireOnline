-- Mot de passe oublié, sans service d'e-mail :
-- 1. sur l'écran de connexion, le membre saisit son e-mail : la demande
--    arrive chez les administrateurs (Comptes en ligne) ;
-- 2. un administrateur donne un mot de passe provisoire et le transmet
--    lui-même (téléphone, SMS, de vive voix) ;
-- 3. à la connexion suivante, le membre doit choisir son propre mot de passe.
-- À exécuter une fois dans Supabase : SQL Editor > New query > coller > Run.
-- Le script peut être relancé sans danger.

alter table public.profiles add column if not exists mdp_demande_at timestamptz;
alter table public.profiles add column if not exists mdp_provisoire boolean not null default false;

-- Demande envoyée depuis l'écran de connexion (sans compte connecté).
-- La réponse est toujours la même, que l'e-mail existe ou non, pour ne pas
-- révéler qui est inscrit. Une demande déjà en attente garde sa date.
create or replace function public.request_password_help(p_email text)
returns void language plpgsql security definer set search_path = public as $$
begin
    if length(coalesce(p_email, '')) between 3 and 254 then
        update public.profiles
           set mdp_demande_at = now()
         where lower(email) = lower(trim(p_email))
           and statut = 'actif'
           and mdp_demande_at is null;
    end if;
end;
$$;

-- L'administrateur écarte une demande (par exemple déjà réglée de vive voix).
create or replace function public.dismiss_password_request(p_id uuid)
returns void language plpgsql security definer set search_path = public as $$
begin
    if not public.gu_is_admin() then
        raise exception 'Accès refusé' using errcode = '42501';
    end if;
    update public.profiles set mdp_demande_at = null where id = p_id;
end;
$$;

-- Le membre a choisi son propre mot de passe : il n'est plus provisoire.
create or replace function public.password_changed()
returns void language sql security definer set search_path = public as $$
    update public.profiles set mdp_provisoire = false, mdp_demande_at = null where id = auth.uid();
$$;

-- Le mot de passe donné par l'administrateur est provisoire et règle la demande.
create or replace function public.admin_set_password(p_id uuid, p_password text)
returns void language plpgsql security definer set search_path = public, auth, extensions as $$
begin
    if not public.gu_is_admin() then
        raise exception 'Seul un administrateur peut réinitialiser un mot de passe.' using errcode = '42501';
    end if;
    if length(coalesce(p_password, '')) < 8 then
        raise exception 'Le mot de passe doit contenir au moins 8 caractères.';
    end if;
    if exists (select 1 from public.profiles where id = p_id and role = 'Administrateur de la base')
       and public.gu_role() <> 'Administrateur de la base' then
        raise exception 'Seul l''administrateur de la base peut modifier ce compte.' using errcode = '42501';
    end if;
    update auth.users
       set encrypted_password = extensions.crypt(p_password, extensions.gen_salt('bf')),
           updated_at = now()
     where id = p_id;
    if not found then raise exception 'Compte introuvable.'; end if;
    update public.profiles set mdp_provisoire = true, mdp_demande_at = null where id = p_id;
end;
$$;

-- Résumé administrateur : ajoute le nombre de demandes de mot de passe
-- (et le niveau reste « ok » quand aucune alerte n'est ouverte).
create or replace function public.capacity_status()
returns jsonb language plpgsql stable security definer set search_path = public as $$
begin
    if not public.gu_is_admin() then
        return jsonb_build_object('open_alerts', 0, 'level', 'ok', 'pending_accounts', 0, 'password_requests', 0);
    end if;
    return jsonb_build_object(
        'open_alerts', (select count(*) from public.admin_alerts where acknowledged_at is null),
        'level', coalesce((select case when bool_or(level = 'critique') then 'critique' else 'attention' end
                             from public.admin_alerts where acknowledged_at is null having count(*) > 0), 'ok'),
        'pending_accounts', (select count(*) from public.profiles where statut = 'en_attente'),
        'password_requests', (select count(*) from public.profiles where statut = 'actif' and mdp_demande_at is not null));
end;
$$;

revoke execute on function public.request_password_help(text), public.dismiss_password_request(uuid),
    public.password_changed() from public;
grant execute on function public.request_password_help(text) to anon, authenticated;
grant execute on function public.dismiss_password_request(uuid), public.password_changed() to authenticated;
