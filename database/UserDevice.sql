CREATE TABLE UserDevices (
  "Id" SERIAL PRIMARY KEY,
  "UserId" TEXT NOT NULL,
  "FCMToken" TEXT NOT NULL,
  "CreatedAt" TIMESTAMPTZ,
  "UpdatedAt" TIMESTAMPTZ
);

SELECT *FROM userdevices;

ALTER TABLE userdevices
ADD CONSTRAINT fk_note_user 
FOREIGN KEY ("UserId") REFERENCES UserInfo("Id")
ON DELETE SET NULL;


CREATE OR REPLACE FUNCTION public.handle_new_auth_user_device()
RETURNS TRIGGER AS $$
BEGIN
  INSERT INTO public.userdevices ("UserId", "FCMToken", "CreatedAt", "UpdatedAt")
  VALUES (NEW.id::text, NULL, NOW(), NOW())
  ON CONFLICT ("UserId") DO NOTHING;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE OR REPLACE TRIGGER on_auth_user_created_device
  AFTER INSERT ON auth.users
  FOR EACH ROW EXECUTE FUNCTION public.handle_new_auth_user_device();