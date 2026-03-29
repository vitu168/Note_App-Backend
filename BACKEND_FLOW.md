# Note App Backend - Logic & Flow

This document is designed for a beginner C# backend developer. It explains the structure, logic, and runtime flow of the Note App backend project.

---

## 📂 Project Structure

The repository uses a standard ASP.NET Core Web API layout.

```
/ (root)
├─ Controllers/       # API controllers handling HTTP requests
├─ Models/            # Data models and DTOs (Data Transfer Objects)
├─ Properties/        # Launch settings, etc.
├─ appsettings.json   # Configuration (Supabase, logging, connection string)
├─ Program.cs         # Application startup/configuration
├─ DbContext.cs       # EF Core context (present but not used)
└─ NoteApi.sln/.csproj
```

Each folder and file has a specific role. Controllers respond to HTTP routes, Models represent database entities or shape data in/out of the API, and Program.cs configures the app.

---

## ⚙️ Configuration & Startup

1. **Program.cs** is the entry point. It:
   - Builds a `WebApplicationBuilder`.
   - Adds MVC controllers, Swagger (for API docs) and the Supabase client as a singleton service.
   - Reads the `Supabase:Url` and `Supabase:Key` from `appsettings.json` (or environment variables).
   - Throws an error if these are missing.
   - Configures application URL and ports (default 8080, can override with `PORT` env var).
   - Applies HTTPS redirection, authorization, and maps controller routes.
   - Starts the application (`app.Run()`).

2. **appsettings.json** holds:
   - Supabase connection settings.
   - A Postgres connection string (though not actively used).
   - Logging levels and allowed hosts.

These settings are loaded automatically by `builder.Configuration`.

---

## 🧩 Data Access

The project relies on **Supabase** (a hosted Postgres with REST API) via the `Supabase.Client`:

- The client is injected into controllers via constructor or thereceiving parameter.
- The fluent PostgREST API (`_supabase.From<T>()`) is used to query tables.
- Entities are defined as C# classes that inherit from `BaseModel` and use `[Table("...")]` attribute to map them to Supabase tables.

The `DbContext` class exists but isn’t referenced anywhere; all persistence happens through Supabase.

---

## 🗂 Models & DTOs

### Entities (database representations):
- `Noteinfo` – represents a note record.
- `UserProfile` – represents a user profile.
- `NoteinfoDetail` – extended note view with user info (unused in controllers).

These classes often match the underlying table schema and may include metadata fields like `CreatedAt`.

### DTOs (Data Transfer Objects):
- `NoteinfoDto`, `NoteinfoCreateDto`, `NoteinfoUpdateDto` – shape the data for requests/responses.
- `UserProfileDto` – used for profile JSON payloads.
- Batch-related DTOs for creating multiple notes in one request.

DTOs decouple the API contract from the internal model and help avoid exposing sensitive or unnecessary fields.

---

## 📡 Controllers & Endpoints

Controllers live in `Controllers/` and follow these patterns:

### NoteInfoController

Routes: `api/noteinfo`

- **GET /**: List notes with optional `search`, `isFavorites`, `page`, and `pageSize` query parameters.
  - Builds a query, applies filters, uses `.Range()` for paging, and reads the `Content-Range` header to compute total count.
  - Returns a `PageNotesResult` object containing items and pagination metadata.

- **GET /{id}**: Fetch a single note by ID. Returns `404` if not found.

- **POST /**: Create a new note.
  - Manually computes `nextId` by querying for the max existing ID (causes race-condition risk).
  - Inserts the note and returns `201 Created` with the created DTO.

- **PUT /{id}**: Update an existing note (sends entire object, including `CreatedAt`).

- **DELETE /{id}**: Delete a note.

- **POST batchCreateNotes**: Accepts a collection of notes and inserts them one-by-one, returning success/error details.

Helper method `ParseContentRangeCount` extracts the total record count from the HTTP header.

### NoteInfoDetailsController (redundant)

- **GET /{id}**: Very similar to `NoteInfoController.GetNote(id)` but returns fewer fields. Could be merged.

### UserProfileController

Routes: `api/userprofile`

- **GET /**: List all user profiles.

- **GET /{id}**: Fetch profile by ID using `Filter()` to match the string key.

- **POST /**: Create a profile; sets `CreatedAt` to `UtcNow`.

- **PUT /{id}**: Update a profile if the path ID matches the body ID.

- **DELETE /{id}**: Remove a profile by ID.

Each action maps between entity and DTO manually.

---

## 🧠 Logic Flow Example: Creating a Note

1. Client sends `POST /api/noteinfo` with a JSON body matching `NoteinfoCreateDto`.
2. ASP.NET model binding deserializes the payload to `noteDto`.
3. Controller method executes:
   - Queries Supabase for the current maximum `Id` in `noteinfo` table.
   - Calculates `nextId`.
   - Constructs a `Noteinfo` entity (including timestamps).
   - Inserts the entity via `_supabase.From<Noteinfo>().Insert(...)`.
4. Supabase returns the created row; the controller builds a `NoteinfoDto` from it.
5. Response is `201 Created` with the new note data.

---

## ✅ Best Practices Highlighted

- **Dependency Injection**: Supabase `Client` registered as a singleton and injected into controllers.
- **DTO usage**: Safe boundary between external requests and internal models.
- **Swagger**: Auto‑generated API docs helpful for testing, enabled in development.
- **Config via `appsettings.json`**: Clean separation of secrets and environment settings.

---

## 🔧 Suggestions for Improvement

1. **Use database-generated IDs** (serial/UUID) instead of manual max‑id logic.
2. **Consolidate controllers** (`NoteInfoDetailsController` is unnecessary).
3. **Remove unused `DbContext`** or wire it up if EF Core is desired.
4. **Implement data validation** (e.g., `[Required]` attributes, model state checks). 
5. **Batch insertion** could be optimized by sending a single request with many rows instead of a loop.

---

## 🎯 Summary

This backend serves as a simple CRUD API built on top of Supabase, using modern ASP.NET Core patterns. The flow begins at `Program.cs`, travels through controllers with DI‑injected services, manipulates data via models/DTOs, and returns JSON responses. Understanding this loop is a great foundation for exploring more advanced C# backend concepts.

Feel free to refer back to this document as you explore and expand the project! Good luck on your backend journey. \o/
