-- =============================================================================
-- 03_noteinfo.sql
--
-- Notes table. The owner is mirrored into "UserId" for cheap "list my notes"
-- queries; the canonical share list lives in `note_users` (see 04_note_users.sql).
--
-- Authoritative source: Models/NoteInfo.cs → [Table("noteinfo")]
-- Depends on: 01_userinfo.sql
--
-- Reminder column type note:
--   The Flutter client serializes reminders as a "naive" ISO string (no
--   timezone marker) so the wall-clock time the user picked is preserved
--   as-is. The column is therefore TIMESTAMP WITHOUT TIME ZONE — never
--   coerce to TIMESTAMPTZ unless the client also starts sending an offset.
--
-- Idempotent — safe to re-run.
-- =============================================================================

BEGIN;

-- ── Table ────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.noteinfo (
    "Id"          SERIAL      PRIMARY KEY,
    "Name"        TEXT        NULL,
    "Description" TEXT        NULL,
    "CreatedAt"   TIMESTAMPTZ NULL DEFAULT NOW(),
    "UpdatedAt"   TIMESTAMPTZ NULL DEFAULT NOW(),
    "UserId"      TEXT        NULL,
    "isFavorites" BOOLEAN     NULL DEFAULT FALSE
);

-- Reminder column was added after the initial schema. ADD IF NOT EXISTS
-- keeps the script idempotent on already-migrated DBs.
ALTER TABLE public.noteinfo
    ADD COLUMN IF NOT EXISTS "Reminder" TIMESTAMP NULL;

-- ── Constraints ──────────────────────────────────────────────────────────────
-- ON DELETE SET NULL: deleting a profile shouldn't nuke their notes (a
-- co-owner from `note_users` may still need them). The controller resyncs
-- "UserId" with whoever the next owner becomes.
ALTER TABLE public.noteinfo
    DROP CONSTRAINT IF EXISTS fk_noteinfo_user;
ALTER TABLE public.noteinfo
    ADD  CONSTRAINT fk_noteinfo_user
    FOREIGN KEY ("UserId")
    REFERENCES public.userinfo ("Id")
    ON DELETE SET NULL;

-- ── Indexes ──────────────────────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_noteinfo_user_id      ON public.noteinfo ("UserId");
CREATE INDEX IF NOT EXISTS idx_noteinfo_created_at   ON public.noteinfo ("CreatedAt");
CREATE INDEX IF NOT EXISTS idx_noteinfo_updated_at   ON public.noteinfo ("UpdatedAt");
CREATE INDEX IF NOT EXISTS idx_noteinfo_isfavorites  ON public.noteinfo ("isFavorites");
-- Partial index for the reminders job / any "upcoming reminders" query.
CREATE INDEX IF NOT EXISTS idx_noteinfo_reminder_future
    ON public.noteinfo ("Reminder")
    WHERE "Reminder" IS NOT NULL;

-- ── UpdatedAt auto-bump ─────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION public.set_noteinfo_updated_at()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    NEW."UpdatedAt" := NOW();
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_noteinfo_updated_at ON public.noteinfo;
CREATE TRIGGER trg_noteinfo_updated_at
    BEFORE UPDATE ON public.noteinfo
    FOR EACH ROW
    EXECUTE FUNCTION public.set_noteinfo_updated_at();

-- ── Row Level Security ──────────────────────────────────────────────────────
-- The controller does its own authorization via the X-User-Id header against
-- `note_users`, so RLS here is mostly belt-and-suspenders for direct PostgREST
-- access. We allow read for any authenticated user and trust the backend for
-- writes (writes go through the service role, which bypasses RLS).
ALTER TABLE public.noteinfo ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS noteinfo_select_authed ON public.noteinfo;
CREATE POLICY noteinfo_select_authed
    ON public.noteinfo
    FOR SELECT
    USING (auth.uid() IS NOT NULL);

COMMIT;
