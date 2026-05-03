
BEGIN;

ALTER TABLE IF EXISTS public.noteinfo
    DROP CONSTRAINT IF EXISTS fk_note_user;
ALTER TABLE IF EXISTS public.chatmessenger
    DROP CONSTRAINT IF EXISTS fk_chat_messenger;
ALTER TABLE IF EXISTS public.userdevices
    DROP CONSTRAINT IF EXISTS fk_note_user;

CREATE TABLE IF NOT EXISTS public.userinfo (
    "Id"        TEXT PRIMARY KEY,
    "Name"      TEXT NULL,
    "AvatarUrl" TEXT NULL,
    "Email"     TEXT NULL,
    "CreatedAt" TIMESTAMPTZ DEFAULT NOW(),
    "IsNote"    BOOLEAN DEFAULT FALSE
);

INSERT INTO public.userinfo ("Id", "Name", "AvatarUrl", "Email", "CreatedAt", "IsNote")
SELECT "Id", "Name", "AvatarUrl", "Email", "CreatedAt", "IsNote"
FROM public.userinfo_backup
ON CONFLICT ("Id") DO UPDATE SET
    "Name"      = EXCLUDED."Name",
    "AvatarUrl" = EXCLUDED."AvatarUrl",
    "Email"     = EXCLUDED."Email",
    "CreatedAt" = EXCLUDED."CreatedAt",
    "IsNote"    = EXCLUDED."IsNote";


UPDATE public.noteinfo n
SET "UserId" = b."UserId"
FROM public.noteinfo_userid_backup b
WHERE n."Id" = b."Id"
  AND (n."UserId" IS DISTINCT FROM b."UserId")
  AND EXISTS (SELECT 1 FROM public.userinfo u WHERE u."Id" = b."UserId");

UPDATE public.chatmessenger c
SET "SenderId" = b."SenderId"
FROM public.chatmessenger_senderid_backup b
WHERE c."id" = b."id"
  AND (c."SenderId" IS DISTINCT FROM b."SenderId")
  AND EXISTS (SELECT 1 FROM public.userinfo u WHERE u."Id" = b."SenderId");

UPDATE public.userdevices d
SET "UserId" = b."UserId"
FROM public.userdevices_userid_backup b
WHERE d."Id" = b."Id"
  AND (d."UserId" IS DISTINCT FROM b."UserId")
  AND EXISTS (SELECT 1 FROM public.userinfo u WHERE u."Id" = b."UserId");

-- ---------- 5. Indexes ----------
CREATE INDEX IF NOT EXISTS idx_userinfo_email     ON public.userinfo ("Email");
CREATE INDEX IF NOT EXISTS idx_userinfo_createdat ON public.userinfo ("CreatedAt");
CREATE INDEX IF NOT EXISTS idx_userinfo_isnote    ON public.userinfo ("IsNote");

-- ---------- 6. Auth signup trigger (mirrors AuthController.SignUp) ----------
CREATE OR REPLACE FUNCTION public.handle_new_auth_user()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO public.userinfo ("Id", "Email", "CreatedAt")
    VALUES (NEW.id::text, NEW.email, NOW())
    ON CONFLICT ("Id") DO NOTHING;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
CREATE TRIGGER on_auth_user_created
    AFTER INSERT ON auth.users
    FOR EACH ROW EXECUTE FUNCTION public.handle_new_auth_user();

-- ---------- 7. Row Level Security ----------
ALTER TABLE public.userinfo ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "Allow all reads on userinfo"  ON public.userinfo;
DROP POLICY IF EXISTS "Allow all reads on usersinfo" ON public.userinfo;
CREATE POLICY "Allow all reads on userinfo"
    ON public.userinfo FOR SELECT USING (true);

DROP POLICY IF EXISTS "Allow insert own userinfo" ON public.userinfo;
CREATE POLICY "Allow insert own userinfo"
    ON public.userinfo FOR INSERT WITH CHECK (true);

DROP POLICY IF EXISTS "Allow update own userinfo" ON public.userinfo;
CREATE POLICY "Allow update own userinfo"
    ON public.userinfo FOR UPDATE USING (auth.uid()::text = "Id");

-- ---------- 8. Recreate FK constraints ----------
ALTER TABLE public.noteinfo
    ADD CONSTRAINT fk_note_user
    FOREIGN KEY ("UserId") REFERENCES public.userinfo("Id")
    ON DELETE SET NULL;

ALTER TABLE public.chatmessenger
    ADD CONSTRAINT fk_chat_messenger
    FOREIGN KEY ("SenderId") REFERENCES public.userinfo("Id")
    ON DELETE SET NULL;

ALTER TABLE public.userdevices
    ADD CONSTRAINT fk_note_user
    FOREIGN KEY ("UserId") REFERENCES public.userinfo("Id")
    ON DELETE SET NULL;

COMMIT;

-- ---------- 9. Verify ----------
SELECT 'userinfo' AS table_name, COUNT(*) AS rows FROM public.userinfo
UNION ALL SELECT 'noteinfo_with_user',     COUNT(*) FROM public.noteinfo      WHERE "UserId"   IS NOT NULL
UNION ALL SELECT 'chatmessenger_w_sender', COUNT(*) FROM public.chatmessenger WHERE "SenderId" IS NOT NULL
UNION ALL SELECT 'userdevices_with_user',  COUNT(*) FROM public.userdevices   WHERE "UserId"   IS NOT NULL;
