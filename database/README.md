# `database/` — schema, migrations, ops scripts

PostgreSQL scripts that build and maintain the Supabase Postgres backing the API. **All scripts target PostgreSQL 14+** (Supabase) — they will throw lint errors in editors set to a different SQL dialect; that's the editor, not the SQL.

## Layout

| File | What it does | When to run |
|---|---|---|
| `00_init.sql` | Runs every numbered script in order. | Bootstrap on a fresh DB, or to re-assert schema on an existing one. |
| `01_userinfo.sql` | `userinfo` table + auth-signup trigger + RLS. | Bootstrap. |
| `02_userdevices.sql` | `userdevices` table + auth-signup trigger + RLS. | Bootstrap. |
| `03_noteinfo.sql` | `noteinfo` table (incl. `Reminder`) + RLS. | Bootstrap. |
| `04_note_users.sql` | `note_users` junction table + role check + owner-uniqueness. | Bootstrap. |
| `05_chatmessenger.sql` | `chatmessenger` table + indexes + RLS. | Bootstrap. |
| `migrations/001_add_reminder_column.sql` | Adds `Reminder` to existing prod DBs. | One-shot, prod only. Already part of `03_noteinfo.sql` for fresh deployments. |
| `UserInfo_Backup.sql` | Snapshots `userinfo` and dependent FK columns. | Before any destructive change to `userinfo`. |
| `UserInfo_Restore.sql` | Restores from the snapshot, rebinds FKs, re-attaches triggers + constraints. | After the destructive change has run. |

Every numbered script is **idempotent** — safe to re-run any number of times. They use `CREATE … IF NOT EXISTS`, `DROP … IF EXISTS` + `ADD`, `ON CONFLICT DO NOTHING/UPDATE`, etc.

## Dependency order

```
01_userinfo
   ├── 02_userdevices
   ├── 03_noteinfo
   │     └── 04_note_users
   └── 05_chatmessenger
```

`00_init.sql` runs them in this order via psql `\i` meta-commands.

## How to run

### From psql (CLI)

```bash
psql "$DATABASE_URL" -f 00_init.sql
```

### From Supabase Studio (web SQL Editor)

The web editor doesn't understand `\i`. Paste each numbered file separately, in order: `01` → `02` → `03` → `04` → `05`.

### One-off migration on prod

```bash
psql "$DATABASE_URL" -f migrations/001_add_reminder_column.sql
```

## Reminder column — type rationale

`noteinfo."Reminder"` is **`TIMESTAMP WITHOUT TIME ZONE`**, not `TIMESTAMPTZ`. Reason: the Flutter client serializes the user's wall-clock pick as a naive ISO string (no offset). With `TIMESTAMPTZ` Postgres would re-interpret that string as the session timezone (usually UTC) and shift the alarm by hours. With `TIMESTAMP` it stores the wall clock as-is and the client treats it as local time on read. Don't change this without a coordinated client change.

## Schema invariants the API depends on

These are documented in `../API_REFERENCE.md` §9. The scripts here implement them — keep both in sync:

- `userinfo."Id"` is the Supabase auth user id (text uuid). DB trigger seeds the row.
- `userdevices."UserId"` is `UNIQUE` so `POST /api/device/save-token` can upsert. DB trigger seeds an empty row on signup.
- `noteinfo."UserId"` mirrors the **owner** in `note_users` (controller keeps them in sync).
- `note_users` has composite PK `("NoteId","UserId")`, role CHECK in `(owner|deleter|editor|viewer)`, and a partial unique index `uniq_note_owner` enforcing one owner per note.
- All FKs to `userinfo` are `ON DELETE SET NULL` (so historical data survives a profile delete) **except** `note_users` which is `ON DELETE CASCADE` (a deleted user is removed from every share).

## Editor lint warnings

If your editor flags `ADD COLUMN IF NOT EXISTS`, partial indexes, or `auth.users` references as syntax errors, it's parsing the file as **T-SQL (SQL Server)** or generic ANSI SQL. Switch the language mode to **PostgreSQL** (status bar, bottom right) — the SQL itself is valid for Supabase/Postgres.
