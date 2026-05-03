-- =============================================================================
-- 01_userinfo.sql
--
-- Profile table that mirrors `auth.users`. The PK is the Supabase auth user id
-- (stored as TEXT). Auto-populated by a trigger so a profile row exists for
-- every signed-up user.
--
-- Authoritative source: Models/UserProfile.cs → [Table("userinfo")]
--
-- Idempotent — safe to re-run.
-- =============================================================================

BEGIN;

-- ── Table ────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.userinfo (
    "Id"        TEXT        PRIMARY KEY,
    "Name"      TEXT        NULL,
    "AvatarUrl" TEXT        NULL,
    "Email"     TEXT        NULL,
    "CreatedAt" TIMESTAMPTZ NULL DEFAULT NOW(),
    "IsNote"    BOOLEAN     NULL DEFAULT FALSE
);

-- ── Indexes ──────────────────────────────────────────────────────────────────
-- Email lookups (login, search). Unique would break shared/legacy rows, so we
-- only index, not constrain.
CREATE INDEX IF NOT EXISTS idx_userinfo_email      ON public.userinfo ("Email");
CREATE INDEX IF NOT EXISTS idx_userinfo_created_at ON public.userinfo ("CreatedAt");
CREATE INDEX IF NOT EXISTS idx_userinfo_is_note    ON public.userinfo ("IsNote");

-- ── Auth trigger: seed a userinfo row on every Supabase signup ──────────────
-- Mirrors the implicit contract used by AuthController.SignUp() and the live
-- DB documented in API_REFERENCE.md (§ "Database invariants").
CREATE OR REPLACE FUNCTION public.handle_new_auth_user()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
    INSERT INTO public.userinfo ("Id", "Email", "CreatedAt")
    VALUES (NEW.id::text, NEW.email, NOW())
    ON CONFLICT ("Id") DO NOTHING;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
CREATE TRIGGER on_auth_user_created
    AFTER INSERT ON auth.users
    FOR EACH ROW
    EXECUTE FUNCTION public.handle_new_auth_user();

-- ── Row Level Security ──────────────────────────────────────────────────────
ALTER TABLE public.userinfo ENABLE ROW LEVEL SECURITY;

-- Everyone can read profiles (needed for sharing UX, search, mentions).
DROP POLICY IF EXISTS userinfo_select_all  ON public.userinfo;
CREATE POLICY userinfo_select_all
    ON public.userinfo
    FOR SELECT
    USING (true);

-- Inserts only happen via the auth trigger (SECURITY DEFINER) or the backend
-- service role; we still allow generic INSERT for the rare manual case.
DROP POLICY IF EXISTS userinfo_insert_self ON public.userinfo;
CREATE POLICY userinfo_insert_self
    ON public.userinfo
    FOR INSERT
    WITH CHECK (true);

-- Only the row's owner (or the service role bypassing RLS) can update.
DROP POLICY IF EXISTS userinfo_update_self ON public.userinfo;
CREATE POLICY userinfo_update_self
    ON public.userinfo
    FOR UPDATE
    USING (auth.uid()::text = "Id");

COMMIT;
