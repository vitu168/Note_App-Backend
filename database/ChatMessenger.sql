CREATE TABLE ChatMessenger (
    "id" SERIAL PRIMARY KEY, 
    "ConversationId" INTEGER NOT NULL,
    "SenderId" TEXT NULL,              
    "Content" TEXT NULL,                   
    "MessageType" TEXT NULL,               
    "IsRead" BOOLEAN DEFAULT FALSE,         
    "CreatedAt" TIMESTAMPTZ DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ DEFAULT NOW() 
);

ALTER TABLE ChatMessenger
ADD CONSTRAINT fk_chat_messenger 
FOREIGN KEY ("SenderId") REFERENCES UserInfo("Id")
ON DELETE SET NULL;

SELECT * FROM ChatMessenger ORDER BY "CreatedAt" DESC;

ALTER TABLE ChatMessenger 
ADD COLUMN "ReceiverId" TEXT NULL;

SELECT 
    m."id",
    m."Content",
    m."MessageType",
    m."CreatedAt"
FROM ChatMessenger AS m
INNER JOIN UserInfo AS u ON m."SenderId" = u."Id"
WHERE m."ConversationId" = 101
ORDER BY m."CreatedAt" ASC;
