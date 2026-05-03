-- =============================================================================
-- UserInfo_Restore.sql — operational tool, not part of the schema bootstrap
--
-- Run this AFTER a destructive change (CASCADE drop of userinfo, etc.) to:
--   1. Re-create the userinfo rows from `userinfo_backup`
--   2. Rebind FK columns on noteinfo / chatmessenger / userdevices that
--      Postgres set to NULL when their referenced row vanished
--   3. Re-hydrate `note_users` from its backup
--   4. Re-create the auth-signup trigger (DROPped by the destructive step)
--   5. Re-attach FK constraints
--
-- Prerequisites:
--   • UserInfo_Backup.sql was run before the destructive change
--   • 01_userinfo.sql … 05_chatmessenger.sql have been re-run since
--
-- Idempotent — every step uses IF NOT EXISTS / DROP IF EXISTS / ON CONFLICT.
-- =============================================================================

BEGIN;

-- ── 1. Drop FKs we're about to re-establish ─────────────────────────────────
ALTER TABLE IF EXISTS public.noteinfo
    DROP CONSTRAINT IF EXISTS fk_noteinfo_user;
ALTER TABLE IF EXISTS public.chatmessenger
    DROP CONSTRAINT IF EXISTS fk_chatmessenger_sender,
    DROP CONSTRAINT IF EXISTS fk_chatmessenger_receiver;
ALTER TABLE IF EXISTS public.userdevices
    DROP CONSTRAINT IF EXISTS fk_userdevices_user;
ALTER TABLE IF EXISTS public.note_users
    DROP CONSTRAINT IF EXISTS fk_note_users_user,
    DROP CONSTRAINT IF EXISTS fk_note_users_note;

-- ── 2. Restore userinfo rows ────────────────────────────────────────────────
INSERT INTO public.userinfo ("Id", "Name", "AvatarUrl", "Email", "CreatedAt", "IsNote")
SELECT "Id", "Name", "AvatarUrl", "Email", "CreatedAt", "IsNote"
FROM public.userinfo_backup
ON CONFLICT ("Id") DO UPDATE SET
    "Name"      = EXCLUDED."Name",
    "AvatarUrl" = EXCLUDED."AvatarUrl",
    "Email"     = EXCLUDED."Email",
    "CreatedAt" = EXCLUDED."CreatedAt",
    "IsNote"    = EXCLUDED."IsNote";

-- ── 3. Rebind FK columns from snapshots ─────────────────────────────────────
-- Only restore values whose target user is back in userinfo. Anything else
-- stays NULL so a later FK add doesn't fail.

UPDATE public.noteinfo n
SET "UserId" = b."UserId"
FROM public.noteinfo_userid_backup b
WHERE n."Id" = b."Id"
  AND  n."UserId" IS DISTINCT FROM b."UserId"
  AND  EXISTS (SELECT 1 FROM public.userinfo u WHERE u."Id" = b."UserId");

UPDATE public.chatmessenger c
SET "SenderId"   = b."SenderId",
    "ReceiverId" = b."ReceiverId"
FROM public.chatmessenger_userid_backup b
WHERE c."id" = b."id"
  AND (
        c."SenderId"   IS DISTINCT FROM b."SenderId"
     OR c."ReceiverId" IS DISTINCT FROM b."ReceiverId"
  )
  AND  (
        b."SenderId"   IS NULL
     OR EXISTS (SELECT 1 FROM public.userinfo u WHERE u."Id" = b."SenderId")
  )
  AND  (
        b."ReceiverId" IS NULL
     OR EXISTS (SELECT 1 FROM public.userinfo u WHERE u."Id" = b."ReceiverId")
  );

UPDATE public.userdevices d
SET "UserId" = b."UserId"
FROM public.userdevices_userid_backup b
WHERE d."Id" = b."Id"
  AND  d."UserId" IS DISTINCT FROM b."UserId"
  AND  EXISTS (SELECT 1 FROM public.userinfo u WHERE u."Id" = b."UserId");

-- ── 4. Re-hydrate note_users from its backup ────────────────────────────────
INSERT INTO public.note_users ("NoteId", "UserId", "Role", "CreatedAt")
SELECT b."NoteId", b."UserId", b."Role", b."CreatedAt"
FROM public.note_users_backup b
WHERE EXISTS (SELECT 1 FROM public.userinfo u WHERE u."Id" = b."UserId")
  AND EXISTS (SELECT 1 FROM public.noteinfo n WHERE n."Id" = b."NoteId")
ON CONFLICT ("NoteId", "UserId") DO UPDATE SET
    "Role"      = EXCLUDED."Role",
    "CreatedAt" = EXCLUDED."CreatedAt";

-- ── 5. Re-create the auth-signup triggers (matches 01/02 scripts) ──────────
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

-- ── 6. Re-attach FK constraints ─────────────────────────────────────────────
ALTER TABLE public.noteinfo
    ADD CONSTRAINT fk_noteinfo_user
    FOREIGN KEY ("UserId")
    REFERENCES public.userinfo ("Id")
    ON DELETE SET NULL;

ALTER TABLE public.chatmessenger
    ADD CONSTRAINT fk_chatmessenger_sender
    FOREIGN KEY ("SenderId")
    REFERENCES public.userinfo ("Id")
    ON DELETE SET NULL;

ALTER TABLE public.chatmessenger
    ADD CONSTRAINT fk_chatmessenger_receiver
    FOREIGN KEY ("ReceiverId")
    REFERENCES public.userinfo ("Id")
    ON DELETE SET NULL;

ALTER TABLE public.userdevices
    ADD CONSTRAINT fk_userdevices_user
    FOREIGN KEY ("UserId")
    REFERENCES public.userinfo ("Id")
    ON DELETE SET NULL;

ALTER TABLE public.note_users
    ADD CONSTRAINT fk_note_users_note
    FOREIGN KEY ("NoteId")
    REFERENCES public.noteinfo ("Id")
    ON DELETE CASCADE;

ALTER TABLE public.note_users
    ADD CONSTRAINT fk_note_users_user
    FOREIGN KEY ("UserId")
    REFERENCES public.userinfo ("Id")
    ON DELETE CASCADE;

COMMIT;

-- ── 7. Verify ───────────────────────────────────────────────────────────────
SELECT 'userinfo'                  AS table_name, COUNT(*) AS rows FROM public.userinfo
UNION ALL SELECT 'noteinfo (with user)',  COUNT(*) FROM public.noteinfo      WHERE "UserId"     IS NOT NULL
UNION ALL SELECT 'chatmsg (with sender)', COUNT(*) FROM public.chatmessenger WHERE "SenderId"   IS NOT NULL
UNION ALL SELECT 'chatmsg (with recv)',   COUNT(*) FROM public.chatmessenger WHERE "ReceiverId" IS NOT NULL
UNION ALL SELECT 'userdevices (with u)',  COUNT(*) FROM public.userdevices   WHERE "UserId"     IS NOT NULL
UNION ALL SELECT 'note_users',            COUNT(*) FROM public.note_users;
