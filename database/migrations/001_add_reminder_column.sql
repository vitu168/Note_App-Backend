-- =============================================================================
-- 001_add_reminder_column.sql
--
-- Adds the `Reminder` column to `noteinfo`. Already baked into 03_noteinfo.sql
-- for fresh deployments — this file exists for production DBs that were
-- created before the column.
--
-- Column is TIMESTAMP WITHOUT TIME ZONE because the Flutter client serializes
-- the user's wall-clock pick as a naive ISO string (no offset). Storing as
-- TIMESTAMPTZ would re-interpret it as the session timezone (usually UTC) and
-- shift the alert by hours.
--
-- Idempotent — safe to re-run.
-- =============================================================================

ALTER TABLE public.noteinfo
    ADD COLUMN IF NOT EXISTS "Reminder" TIMESTAMP NULL;

CREATE INDEX IF NOT EXISTS idx_noteinfo_reminder_future
    ON public.noteinfo ("Reminder")
    WHERE "Reminder" IS NOT NULL;

-- Verification — should show "Reminder" with data_type "timestamp without time zone".
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name   = 'noteinfo'
  AND column_name  = 'Reminder';
