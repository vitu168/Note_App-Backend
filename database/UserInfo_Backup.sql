BEGIN;

CREATE TABLE IF NOT EXISTS public.userinfo_backup (
    "Id"         TEXT,
    "Name"       TEXT,
    "AvatarUrl"  TEXT,
    "Email"      TEXT,
    "CreatedAt"  TIMESTAMPTZ,
    "IsNote"     BOOLEAN,
    backed_up_at TIMESTAMPTZ DEFAULT NOW()
);

TRUNCATE TABLE public.userinfo_backup;

INSERT INTO public.userinfo_backup
    ("Id", "Name", "AvatarUrl", "Email", "CreatedAt", "IsNote")
SELECT "Id", "Name", "AvatarUrl", "Email", "CreatedAt", "IsNote"
FROM public.userinfo;

-- ---------- 2. noteinfo → userinfo mapping ----------
CREATE TABLE IF NOT EXISTS public.noteinfo_userid_backup (
    "Id"         INTEGER,
    "UserId"     TEXT,
    backed_up_at TIMESTAMPTZ DEFAULT NOW()
);

TRUNCATE TABLE public.noteinfo_userid_backup;

INSERT INTO public.noteinfo_userid_backup ("Id", "UserId")
SELECT "Id", "UserId"
FROM public.noteinfo
WHERE "UserId" IS NOT NULL;

-- ---------- 3. chatmessenger → userinfo mapping ----------
CREATE TABLE IF NOT EXISTS public.chatmessenger_senderid_backup (
    "id"         INTEGER,
    "SenderId"   TEXT,
    backed_up_at TIMESTAMPTZ DEFAULT NOW()
);

TRUNCATE TABLE public.chatmessenger_senderid_backup;

INSERT INTO public.chatmessenger_senderid_backup ("id", "SenderId")
SELECT "id", "SenderId"
FROM public.chatmessenger
WHERE "SenderId" IS NOT NULL;

-- ---------- 4. userdevices → userinfo mapping ----------
CREATE TABLE IF NOT EXISTS public.userdevices_userid_backup (
    "Id"         INTEGER,
    "UserId"     TEXT,
    backed_up_at TIMESTAMPTZ DEFAULT NOW()
);

TRUNCATE TABLE public.userdevices_userid_backup;

INSERT INTO public.userdevices_userid_backup ("Id", "UserId")
SELECT "Id", "UserId"
FROM public.userdevices
WHERE "UserId" IS NOT NULL;

COMMIT;

-- ---------- Verify counts ----------
SELECT 'userinfo'                AS source, COUNT(*) FROM public.userinfo
UNION ALL SELECT 'userinfo_backup',          COUNT(*) FROM public.userinfo_backup
UNION ALL SELECT 'noteinfo_userid_backup',   COUNT(*) FROM public.noteinfo_userid_backup
UNION ALL SELECT 'chatmessenger_sender_bk',  COUNT(*) FROM public.chatmessenger_senderid_backup
UNION ALL SELECT 'userdevices_userid_backup',COUNT(*) FROM public.userdevices_userid_backup;
