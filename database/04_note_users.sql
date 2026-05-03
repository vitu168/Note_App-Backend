-- =============================================================================
-- 04_note_users.sql
--
-- Junction table: which users have access to which note, and at what role.
-- Backs every endpoint under /api/NoteInfo/{id}/share* and the permission
-- matrix in API_REFERENCE.md §5.
--
-- Authoritative source: Models/NoteUser.cs → [Table("note_users")]
-- Depends on: 01_userinfo.sql, 03_noteinfo.sql
--
-- Invariants the backend relies on:
--   • Composite PK (NoteId, UserId)            → one row per (note, user)
--   • Role IN (owner, deleter, editor, viewer) → see permission matrix
--   • Exactly one owner per note               → partial unique index
--   • Both FKs ON DELETE CASCADE               → leave/share endpoints rely on it
--
-- Idempotent — safe to re-run.
-- =============================================================================

BEGIN;

-- ── Table ────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.note_users (
    "NoteId"    INTEGER     NOT NULL,
    "UserId"    TEXT        NOT NULL,
    "Role"      TEXT        NOT NULL DEFAULT 'viewer',
    "CreatedAt" TIMESTAMPTZ NULL    DEFAULT NOW(),
    CONSTRAINT pk_note_users PRIMARY KEY ("NoteId", "UserId")
);

-- ── Constraints ──────────────────────────────────────────────────────────────
-- Role whitelist. Keep in sync with the controller and the Flutter NoteRole
-- constants (lib/core/services/note_api_service.dart).
ALTER TABLE public.note_users
    DROP CONSTRAINT IF EXISTS chk_note_users_role;
ALTER TABLE public.note_users
    ADD  CONSTRAINT chk_note_users_role
    CHECK ("Role" IN ('owner', 'deleter', 'editor', 'viewer'));

-- FK → noteinfo. CASCADE: deleting the note must wipe every share row.
ALTER TABLE public.note_users
    DROP CONSTRAINT IF EXISTS fk_note_users_note;
ALTER TABLE public.note_users
    ADD  CONSTRAINT fk_note_users_note
    FOREIGN KEY ("NoteId")
    REFERENCES public.noteinfo ("Id")
    ON DELETE CASCADE;

-- FK → userinfo. CASCADE: deleting a profile yanks them off every note.
ALTER TABLE public.note_users
    DROP CONSTRAINT IF EXISTS fk_note_users_user;
ALTER TABLE public.note_users
    ADD  CONSTRAINT fk_note_users_user
    FOREIGN KEY ("UserId")
    REFERENCES public.userinfo ("Id")
    ON DELETE CASCADE;

-- Exactly one owner per note. Partial unique index — viewers/editors/deleters
-- can be many; owner can be only one. transfer-owner / leave depend on this.
DROP INDEX IF EXISTS public.uniq_note_owner;
CREATE UNIQUE INDEX uniq_note_owner
    ON public.note_users ("NoteId")
    WHERE "Role" = 'owner';

-- ── Indexes ──────────────────────────────────────────────────────────────────
-- "What notes does this user have access to?" — hot path for the home screen.
CREATE INDEX IF NOT EXISTS idx_note_users_user_id ON public.note_users ("UserId");
-- "Who has access to this note?" — already covered by the PK leading edge,
-- but explicit index makes the role lookups self-documenting.
CREATE INDEX IF NOT EXISTS idx_note_users_note_id ON public.note_users ("NoteId");

-- ── Row Level Security ──────────────────────────────────────────────────────
-- A user may only see share rows that concern them; only an owner may insert.
-- The backend service role bypasses RLS, so it can still administer freely.
ALTER TABLE public.note_users ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS note_users_select_self ON public.note_users;
CREATE POLICY note_users_select_self
    ON public.note_users
    FOR SELECT
    USING (auth.uid()::text = "UserId");

DROP POLICY IF EXISTS note_users_insert_owner ON public.note_users;
CREATE POLICY note_users_insert_owner
    ON public.note_users
    FOR INSERT
    WITH CHECK (
        EXISTS (
            SELECT 1
            FROM public.note_users existing
            WHERE existing."NoteId" = note_users."NoteId"
              AND existing."UserId" = auth.uid()::text
              AND existing."Role"   = 'owner'
        )
    );

COMMIT;
