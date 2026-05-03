-- ============================================================
-- STEP 1: Create UserInfo table
-- Matches: Models/UserProfile.cs → [Table("userinfo")]
-- ============================================================

CREATE TABLE IF NOT EXISTS public.userinfo (
    "Id"        TEXT PRIMARY KEY,
    "Name"      TEXT NULL,
    "AvatarUrl" TEXT NULL,
    "Email"     TEXT NULL,
    "CreatedAt" TIMESTAMPTZ DEFAULT NOW(),
    "IsNote"    BOOLEAN DEFAULT FALSE
);

-- ============================================================
-- STEP 2: Indexes for common queries
-- ============================================================

CREATE INDEX IF NOT EXISTS idx_userinfo_email     ON public.userinfo ("Email");
CREATE INDEX IF NOT EXISTS idx_userinfo_createdat ON public.userinfo ("CreatedAt");
CREATE INDEX IF NOT EXISTS idx_userinfo_isnote    ON public.userinfo ("IsNote");

-- ============================================================
-- STEP 3: Auto-create userinfo row on Supabase Auth signup
-- Matches: AuthController.cs → SignUp() inserts into userinfo
-- ============================================================

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

-- ============================================================
-- STEP 4: Row Level Security
-- ============================================================

ALTER TABLE public.userinfo ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "Allow all reads on userinfo" ON public.userinfo;
CREATE POLICY "Allow all reads on usersinfo"
    ON public.userinfo FOR SELECT USING (true);

DROP POLICY IF EXISTS "Allow insert own userinfo" ON public.userinfo;
CREATE POLICY "Allow insert own userinfo"
    ON public.userinfo FOR INSERT WITH CHECK (true);

DROP POLICY IF EXISTS "Allow update own userinfo" ON public.userinfo;
CREATE POLICY "Allow update own userinfo"
    ON public.userinfo FOR UPDATE USING (auth.uid()::text = "Id");

-- ============================================================
-- STEP 5: Restore FK constraints broken by CASCADE drop
-- Run after userinfo is created
-- ============================================================

-- From NoteInfo table
ALTER TABLE public.noteinfo
    DROP CONSTRAINT IF EXISTS fk_note_user;
ALTER TABLE public.noteinfo
    ADD CONSTRAINT fk_note_user
    FOREIGN KEY ("UserId") REFERENCES public.userinfo("Id")
    ON DELETE SET NULL;

-- From ChatMessenger table
ALTER TABLE public.chatmessenger
    DROP CONSTRAINT IF EXISTS fk_chat_messenger;
ALTER TABLE public.chatmessenger
    ADD CONSTRAINT fk_chat_messenger
    FOREIGN KEY ("SenderId") REFERENCES public.userinfo("Id")
    ON DELETE SET NULL;

-- From UserDevices table
ALTER TABLE public.userdevices
    DROP CONSTRAINT IF EXISTS fk_note_user;
ALTER TABLE public.userdevices
    ADD CONSTRAINT fk_note_user
    FOREIGN KEY ("UserId") REFERENCES public.userinfo("Id")
    ON DELETE SET NULL;

-- ============================================================
-- STEP 6: Verify restore
-- ============================================================

SELECT * FROM public.userinfo ORDER BY "CreatedAt" DESC;
