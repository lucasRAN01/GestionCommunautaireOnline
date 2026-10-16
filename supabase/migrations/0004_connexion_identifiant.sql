-- Connexion avec l'identifiant (« lucas », « jean.pierre », « lucas1 »…) en plus de l'e-mail.
-- L'écran de connexion demande au serveur l'e-mail du compte qui porte cet
-- identifiant ; le serveur ne le donne que si le mot de passe est juste, pour ne
-- pas révéler les adresses des membres. Après 5 essais faux en 15 minutes, cet
-- identifiant est bloqué 15 minutes (la connexion par e-mail reste possible).
-- À exécuter une fois dans Supabase : SQL Editor > New query > coller > Run.
-- Le script peut être relancé sans danger.

create table if not exists public.login_attempts (
    identifiant text not null,
    at          timestamptz not null default now()
);
create index if not exists login_attempts_identifiant_at on public.login_attempts (identifiant, at);
alter table public.login_attempts enable row level security;
-- Aucune politique : la table n'est lisible que par les fonctions ci-dessous.

create or replace function public.login_email(p_identifiant text, p_password text)
returns text language plpgsql security definer set search_path = public, auth, extensions as $$
declare
    v_ident text := lower(trim(coalesce(p_identifiant, '')));
    v_id    uuid;
    v_email text;
begin
    if v_ident = '' or length(v_ident) > 120 or coalesce(p_password, '') = '' then
        return null;
    end if;
    delete from public.login_attempts where at < now() - interval '1 day';
    if (select count(*) from public.login_attempts
         where identifiant = v_ident and at > now() - interval '15 minutes') >= 5 then
        raise exception 'trop_de_tentatives';
    end if;

    -- Fiche membre (données partagées) reliée à un compte en ligne actif.
    select p.id into v_id
      from public.app_data d
      cross join lateral jsonb_array_elements(d.value) as u(fiche)
      join public.profiles p on p.id::text = u.fiche->>'id'
     where d.key = 'gu2:users'
       and lower(u.fiche->>'identifiant') = v_ident
       and p.statut = 'actif'
     limit 1;

    if v_id is not null then
        select a.email into v_email
          from auth.users a
         where a.id = v_id
           and a.encrypted_password = extensions.crypt(p_password, a.encrypted_password);
    end if;

    if v_email is null then
        insert into public.login_attempts (identifiant) values (v_ident);
        return null;
    end if;
    delete from public.login_attempts where identifiant = v_ident;
    return v_email;
end;
$$;

revoke execute on function public.login_email(text, text) from public;
grant execute on function public.login_email(text, text) to anon, authenticated;
