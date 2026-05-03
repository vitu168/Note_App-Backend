-- =============================================================================
-- UserInfo_Backup.sql — operational tool, not part of the schema bootstrap
--
-- Snapshots `userinfo` and the FK columns (noteinfo.UserId, chatmessenger.SenderId,
-- chatmessenger.ReceiverId, userdevices.UserId) into sibling _backup tables.
--
-- Use case: before any destructive migration that might drop+recreate userinfo
-- (CASCADE would null out the FKs), take a snapshot here, run the migration,
-- then run UserInfo_Restore.sql to rebind the FK rows to the rehydrated profiles.
--
-- Idempotent — TRUNCATE+INSERT means a rerun simply refreshes the snapshot.
--
-- Run order:  Backup → destructive change → Restore
-- =============================================================================

BEGIN;

-- ── 1. userinfo snapshot ────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.userinfo_backup (
    "Id"          TEXT,
    "Name"        TEXT,
    "AvatarUrl"   TEXT,
    "Email"       TEXT,
    "CreatedAt"   TIMESTAMPTZ,
    "IsNote"      BOOLEAN,
    backed_up_at  TIMESTAMPTZ DEFAULT NOW()
);

TRUNCATE TABLE public.userinfo_backup;

INSERT INTO public.userinfo_backup
    ("Id", "Name", "AvatarUrl", "Email", "CreatedAt", "IsNote")
SELECT
    "Id", "Name", "AvatarUrl", "Email", "CreatedAt", "IsNote"
FROM public.userinfo;

-- ── 2. noteinfo → userinfo mapping ──────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.noteinfo_userid_backup (
    "Id"          INTEGER,
    "UserId"      TEXT,
    backed_up_at  TIMESTAMPTZ DEFAULT NOW()
);

TRUNCATE TABLE public.noteinfo_userid_backup;

INSERT INTO public.noteinfo_userid_backup ("Id", "UserId")
SELECT "Id", "UserId"
FROM public.noteinfo
WHERE "UserId" IS NOT NULL;

-- ── 3. chatmessenger → userinfo mapping (sender + receiver) ────────────────
CREATE TABLE IF NOT EXISTS public.chatmessenger_userid_backup (
    "id"          INTEGER,
    "SenderId"    TEXT,
    "ReceiverId"  TEXT,
    backed_up_at  TIMESTAMPTZ DEFAULT NOW()
);

TRUNCATE TABLE public.chatmessenger_userid_backup;

INSERT INTO public.chatmessenger_userid_backup ("id", "SenderId", "ReceiverId")
SELECT "id", "SenderId", "ReceiverId"
FROM public.chatmessenger
WHERE "SenderId" IS NOT NULL OR "ReceiverId" IS NOT NULL;

-- ── 4. userdevices → userinfo mapping ───────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.userdevices_userid_backup (
    "Id"          INTEGER,
    "UserId"      TEXT,
    "FCMToken"    TEXT,
    backed_up_at  TIMESTAMPTZ DEFAULT NOW()
);

TRUNCATE TABLE public.userdevices_userid_backup;

INSERT INTO public.userdevices_userid_backup ("Id", "UserId", "FCMToken")
SELECT "Id", "UserId", "FCMToken"
FROM public.userdevices
WHERE "UserId" IS NOT NULL;

-- ── 5. note_users snapshot ──────────────────────────────────────────────────
-- The full junction table; CASCADE on userinfo would wipe it.
CREATE TABLE IF NOT EXISTS public.note_users_backup (
    "NoteId"      INTEGER,
    "UserId"      TEXT,
    "Role"        TEXT,
    "CreatedAt"   TIMESTAMPTZ,
    backed_up_at  TIMESTAMPTZ DEFAULT NOW()
);

TRUNCATE TABLE public.note_users_backup;

INSERT INTO public.note_users_backup ("NoteId", "UserId", "Role", "CreatedAt")
SELECT "NoteId", "UserId", "Role", "CreatedAt"
FROM public.note_users;

COMMIT;

-- ── 6. Verification ─────────────────────────────────────────────────────────
SELECT 'userinfo'                AS source, COUNT(*) AS rows FROM public.userinfo
UNION ALL SELECT 'userinfo_backup',          COUNT(*)        FROM public.userinfo_backup
UNION ALL SELECT 'noteinfo_userid_backup',   COUNT(*)        FROM public.noteinfo_userid_backup
UNION ALL SELECT 'chatmessenger_userid_bk',  COUNT(*)        FROM public.chatmessenger_userid_backup
UNION ALL SELECT 'userdevices_userid_backup',COUNT(*)        FROM public.userdevices_userid_backup
UNION ALL SELECT 'note_users_backup',        COUNT(*)        FROM public.note_users_backup;
