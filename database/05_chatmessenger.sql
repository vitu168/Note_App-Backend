-- =============================================================================
-- 05_chatmessenger.sql
--
-- 1:1 chat messages. ConversationId is set to the row's own id on insert
-- (see ChatMessengerController.Create), so a single chat thread between two
-- users uses the first message id as its conversation id.
--
-- Authoritative source: Models/ChatMessenger.cs → [Table("chatmessenger")]
-- Depends on: 01_userinfo.sql
--
-- Note the lowercase "id" PK — that matches the C# model
-- ([PrimaryKey("id")]). Don't rename it.
--
-- Idempotent — safe to re-run.
-- =============================================================================

BEGIN;

-- ── Table ────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.chatmessenger (
    "id"             SERIAL      PRIMARY KEY,
    "ConversationId" INTEGER     NOT NULL,
    "SenderId"       TEXT        NULL,
    "ReceiverId"     TEXT        NULL,
    "Content"        TEXT        NULL,
    "MessageType"    TEXT        NULL DEFAULT 'text',
    "IsRead"         BOOLEAN     NOT NULL DEFAULT FALSE,
    "CreatedAt"      TIMESTAMPTZ NULL DEFAULT NOW(),
    "UpdatedAt"      TIMESTAMPTZ NULL DEFAULT NOW()
);

-- The original migration added ReceiverId after the fact. Keep the ADD
-- IF NOT EXISTS so re-running on already-migrated DBs is a no-op.
ALTER TABLE public.chatmessenger
    ADD COLUMN IF NOT EXISTS "ReceiverId" TEXT NULL;

-- ── Constraints ──────────────────────────────────────────────────────────────
-- Sender FK: SET NULL on profile delete so historical messages survive.
ALTER TABLE public.chatmessenger
    DROP CONSTRAINT IF EXISTS fk_chatmessenger_sender;
ALTER TABLE public.chatmessenger
    ADD  CONSTRAINT fk_chatmessenger_sender
    FOREIGN KEY ("SenderId")
    REFERENCES public.userinfo ("Id")
    ON DELETE SET NULL;

-- Receiver FK: same policy.
ALTER TABLE public.chatmessenger
    DROP CONSTRAINT IF EXISTS fk_chatmessenger_receiver;
ALTER TABLE public.chatmessenger
    ADD  CONSTRAINT fk_chatmessenger_receiver
    FOREIGN KEY ("ReceiverId")
    REFERENCES public.userinfo ("Id")
    ON DELETE SET NULL;

-- ── Indexes ──────────────────────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_chatmessenger_conversation
    ON public.chatmessenger ("ConversationId", "CreatedAt");
CREATE INDEX IF NOT EXISTS idx_chatmessenger_sender
    ON public.chatmessenger ("SenderId");
CREATE INDEX IF NOT EXISTS idx_chatmessenger_receiver
    ON public.chatmessenger ("ReceiverId");
-- Hot path: GET /api/ChatMessenger/unread-count/{receiverId}
CREATE INDEX IF NOT EXISTS idx_chatmessenger_unread
    ON public.chatmessenger ("ReceiverId")
    WHERE "IsRead" = FALSE;

-- ── UpdatedAt auto-bump ─────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION public.set_chatmessenger_updated_at()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    NEW."UpdatedAt" := NOW();
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_chatmessenger_updated_at ON public.chatmessenger;
CREATE TRIGGER trg_chatmessenger_updated_at
    BEFORE UPDATE ON public.chatmessenger
    FOR EACH ROW
    EXECUTE FUNCTION public.set_chatmessenger_updated_at();

-- ── Row Level Security ──────────────────────────────────────────────────────
-- Direct PostgREST clients can read messages where they're a participant.
-- The backend service role bypasses RLS for cross-user queries (e.g. admin).
ALTER TABLE public.chatmessenger ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS chatmessenger_select_participant ON public.chatmessenger;
CREATE POLICY chatmessenger_select_participant
    ON public.chatmessenger
    FOR SELECT
    USING (
        auth.uid()::text = "SenderId"
        OR auth.uid()::text = "ReceiverId"
    );

DROP POLICY IF EXISTS chatmessenger_insert_self ON public.chatmessenger;
CREATE POLICY chatmessenger_insert_self
    ON public.chatmessenger
    FOR INSERT
    WITH CHECK (auth.uid()::text = "SenderId");

DROP POLICY IF EXISTS chatmessenger_update_participant ON public.chatmessenger;
CREATE POLICY chatmessenger_update_participant
    ON public.chatmessenger
    FOR UPDATE
    USING (
        auth.uid()::text = "SenderId"
        OR auth.uid()::text = "ReceiverId"
    );

COMMIT;
