
DROP TABLE IF EXISTS NoteInfo CASCADE;

CREATE TABLE NoteInfo (
    "Id" SERIAL PRIMARY KEY,
    "Name" TEXT NULL,
    "Description" TEXT NULL,
    "CreatedAt" TIMESTAMPTZ NULL,
    "UpdatedAt" TIMESTAMPTZ NULL,
    "UserId" TEXT NULL,
    "isFavorites" BOOLEAN NULL
);

ALTER TABLE noteinfo
ADD COLUMN "Reminder" timestamp;


ALTER TABLE NoteInfo
ADD CONSTRAINT fk_note_user 
FOREIGN KEY ("UserId") REFERENCES UserInfo("Id")
ON DELETE SET NULL;

CREATE INDEX idx_noteinfo_userid ON NoteInfo("UserId");
CREATE INDEX idx_noteinfo_createdat ON NoteInfo("CreatedAt");
CREATE INDEX idx_noteinfo_isfavorites ON NoteInfo("isFavorites");



INSERT INTO NoteInfo ("Name", "Description", "CreatedAt", "UpdatedAt", "UserId", "isFavorites")
VALUES
    ('Note Test User១', 'សូមតេស្តមួយសិន', NOW(), NOW(), '1', true),
    ('Note Test User២', 'សូមតេស្តមួយសិន', NOW(), NOW(), '2', false)
-- RETURNING *;

SELECT * FROM noteinfo WHERE "Name" = 'Note 25';
-- ORDER BY "CreatedAt" DESC;

UPDATE NoteInfo 
SET 
    "Description" = 'Updated description for the note.',
    "isFavorites" = false,
    "UpdatedAt" = NOW()
WHERE "Id" = 1;

-- SELECT * FROM NoteInfo LIMIT 2;

-- ALTER TABLE public.noteinfo DISABLE ROW LEVEL SECURITY;
-- ALTER TABLE public.userinfo DISABLE ROW LEVEL SECURITY;

-- CREATE POLICY "Allow all reads on noteinfo"
ON public.noteinfo
FOR SELECT
USING (true);