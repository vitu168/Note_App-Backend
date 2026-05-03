-- =============================================================================
-- 00_init.sql — full schema bootstrap
--
-- Runs every numbered script in dependency order. Idempotent; safe to run
-- against an empty DB or against an already-deployed one.
--
--   Order:
--     01_userinfo.sql       (independent)
--     02_userdevices.sql    (depends on userinfo)
--     03_noteinfo.sql       (depends on userinfo)
--     04_note_users.sql     (depends on userinfo + noteinfo)
--     05_chatmessenger.sql  (depends on userinfo)
--
-- How to run from psql:
--     \i 00_init.sql
--
-- How to run in Supabase Studio SQL Editor:
--     This file uses psql's `\i` meta-command, which is not understood by
--     Supabase's web SQL editor. From the web editor, paste each numbered
--     file individually, in order.
-- =============================================================================

\i 01_userinfo.sql
\i 02_userdevices.sql
\i 03_noteinfo.sql
\i 04_note_users.sql
\i 05_chatmessenger.sql

-- ── Verify ──────────────────────────────────────────────────────────────────
SELECT
    table_name,
    (xpath('/row/c/text()', query_to_xml(
        format('SELECT count(*) AS c FROM public.%I', table_name),
        false, true, ''
    )))[1]::text::int AS row_count
FROM (VALUES
    ('userinfo'),
    ('userdevices'),
    ('noteinfo'),
    ('note_users'),
    ('chatmessenger')
) AS t(table_name)
ORDER BY table_name;
