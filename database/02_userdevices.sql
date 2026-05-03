-- =============================================================================
-- 02_userdevices.sql
--
-- Stores one FCM (push) token per user. Used by ChatMessengerController to
-- fan-out notifications when a chat message is received.
--
-- Invariant (relied on by the backend): `"UserId"` is UNIQUE so the controller
-- can safely upsert by user id. See API_REFERENCE.md §3.
--
-- Authoritative source: Models/UserDevice.cs → [Table("userdevices")]
-- Depends on: 01_userinfo.sql
--
-- Idempotent — safe to re-run.
-- =============================================================================

BEGIN;

-- ── Table ────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.userdevices (
    "Id"        SERIAL      PRIMARY KEY,
    "UserId"    TEXT        NULL,
    "FCMToken"  TEXT        NULL,
    "CreatedAt" TIMESTAMPTZ NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NULL DEFAULT NOW()
);

-- ── Constraints ──────────────────────────────────────────────────────────────
-- Unique-on-UserId is the invariant POST /api/device/save-token relies on.
ALTER TABLE public.userdevices
    DROP CONSTRAINT IF EXISTS userdevices_userid_unique;
ALTER TABLE public.userdevices
    ADD  CONSTRAINT userdevices_userid_unique UNIQUE ("UserId");

-- FK to userinfo. ON DELETE SET NULL so deleting a profile retains the row
-- (and the device can be re-bound if the user re-signs-up).
ALTER TABLE public.userdevices
    DROP CONSTRAINT IF EXISTS fk_userdevices_user;
ALTER TABLE public.userdevices
    ADD  CONSTRAINT fk_userdevices_user
    FOREIGN KEY ("UserId")
    REFERENCES public.userinfo ("Id")
    ON DELETE SET NULL;

-- ── Indexes ──────────────────────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_userdevices_user_id ON public.userdevices ("UserId");

-- ── UpdatedAt auto-bump ─────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION public.set_userdevices_updated_at()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    NEW."UpdatedAt" := NOW();
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_userdevices_updated_at ON public.userdevices;
CREATE TRIGGER trg_userdevices_updated_at
    BEFORE UPDATE ON public.userdevices
    FOR EACH ROW
    EXECUTE FUNCTION public.set_userdevices_updated_at();

-- ── Auth trigger: seed an empty device row on every Supabase signup ─────────
-- Documented in API_REFERENCE.md §3 ("New user signs up → DB trigger inserts
-- (UserId, FCMToken=NULL, CreatedAt=NOW(), UpdatedAt=NOW())").
CREATE OR REPLACE FUNCTION public.handle_new_auth_user_device()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
    INSERT INTO public.userdevices ("UserId", "FCMToken", "CreatedAt", "UpdatedAt")
    VALUES (NEW.id::text, NULL, NOW(), NOW())
    ON CONFLICT ("UserId") DO NOTHING;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS on_auth_user_created_device ON auth.users;
CREATE TRIGGER on_auth_user_created_device
    AFTER INSERT ON auth.users
    FOR EACH ROW
    EXECUTE FUNCTION public.handle_new_auth_user_device();

-- ── Row Level Security ──────────────────────────────────────────────────────
ALTER TABLE public.userdevices ENABLE ROW LEVEL SECURITY;

-- Backend reads tokens via service role (bypasses RLS). Clients can only see
-- their own device row.
DROP POLICY IF EXISTS userdevices_select_self ON public.userdevices;
CREATE POLICY userdevices_select_self
    ON public.userdevices
    FOR SELECT
    USING (auth.uid()::text = "UserId");

DROP POLICY IF EXISTS userdevices_upsert_self ON public.userdevices;
CREATE POLICY userdevices_upsert_self
    ON public.userdevices
    FOR INSERT
    WITH CHECK (auth.uid()::text = "UserId");

DROP POLICY IF EXISTS userdevices_update_self ON public.userdevices;
CREATE POLICY userdevices_update_self
    ON public.userdevices
    FOR UPDATE
    USING (auth.uid()::text = "UserId");

DROP POLICY IF EXISTS userdevices_delete_self ON public.userdevices;
CREATE POLICY userdevices_delete_self
    ON public.userdevices
    FOR DELETE
    USING (auth.uid()::text = "UserId");

COMMIT;
