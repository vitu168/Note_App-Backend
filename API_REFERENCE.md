# Note App Backend — Full API Reference

Complete reference for every endpoint in the Note App backend, including request/response schemas, sample payloads, error codes, query parameters, and the database invariants the backend relies on.

- **Local base URL:** `http://localhost:5000`
- **Swagger UI:** `http://localhost:5000/swagger`
- **All requests/responses:** JSON (`Content-Type: application/json`)
- **Auth header for note management:** `X-User-Id: <uuid>` (caller's user ID — required on `PUT/DELETE /api/NoteInfo/...` and all share endpoints)
- **Live test users in this database:**
  - `8e3c27af-2a0b-41fb-9faa-6e32a2dd9c60` — langvitu081@gmail.com
  - `d1b02488-74f0-4a89-930f-376c2d809487` — phornleanghour@gmail.com
  - `3d3574b2-fc8b-44b5-bc05-e855dc77e05a` — vithulang081@gmail.com

---

## Table of Contents

1. [Auth — `/api/auth`](#1-auth--apiauth)
2. [User Profiles — `/api/UserProfile`](#2-user-profiles--apiuserprofile)
3. [Devices / FCM — `/api/device`](#3-devices--fcm--apidevice)
4. [Notes CRUD — `/api/NoteInfo`](#4-notes-crud--apinoteinfo)
5. [Note Sharing & Permissions — `/api/NoteInfo/{id}/...`](#5-note-sharing--permissions--apinoteinfoid)
6. [Chat Messenger — `/api/ChatMessenger`](#6-chat-messenger--apichatmessenger)
7. [Health & Misc](#7-health--misc)
8. [Common DTO schemas](#8-common-dto-schemas)
9. [Database invariants](#9-database-invariants)
10. [Status code conventions](#10-status-code-conventions)

---

## 1. Auth — `/api/auth`

Backed by Supabase Auth. Source: [Controllers/AuthController.cs](Controllers/AuthController.cs).

On signup, two database triggers fire automatically:
- `on_auth_user_created` → inserts a row into `userinfo`
- `on_auth_user_created_device` → inserts a row into `userdevices` with `FCMToken = NULL`

### 1.1 `POST /api/auth/signup`

Create a new account.

**Request body** (`SignUpDto`):

| Field | Type | Required | Notes |
|---|---|---|---|
| `email` | string | yes | unique email |
| `password` | string | yes | min length per Supabase rules |
| `name` | string | no | display name |

**Response 200** (`AuthResponseDto`):

```json
{
  "userId": "8e3c27af-2a0b-41fb-9faa-6e32a2dd9c60",
  "email": "langvitu081@gmail.com",
  "name": "Vithu",
  "accessToken": "eyJhbGciOi...",
  "refreshToken": "v1.MS4w..."
}
```

**Errors:** `400 { "error": "Email and Password are required" }` · `400 { "error": "Sign up failed" }`.

```bash
curl -X POST http://localhost:5000/api/auth/signup \
  -H "Content-Type: application/json" \
  -d '{"email":"new@x.com","password":"Pass1234","name":"Vithu"}'
```

### 1.2 `POST /api/auth/signin`

Sign in with email/password. Optionally registers the device's FCM token at the same time.

**Request body** (`SignInDto`):

| Field | Type | Required |
|---|---|---|
| `email` | string | yes |
| `password` | string | yes |
| `fcmToken` | string | no — if provided, upserted into `userdevices` |

**Response 200** = same shape as `AuthResponseDto`.

**Errors:** `400` (missing email/password) · `401 { "error": "Invalid email or password" }`.

```bash
curl -X POST http://localhost:5000/api/auth/signin \
  -H "Content-Type: application/json" \
  -d '{"email":"langvitu081@gmail.com","password":"...","fcmToken":"abc123"}'
```

### 1.3 `POST /api/auth/social`

Finalise an OAuth session (Google/Facebook) initiated client-side. Creates the `userinfo` row on first social login.

**Request body** (`SocialAuthDto`):

| Field | Type | Required |
|---|---|---|
| `accessToken` | string | yes — token returned by Supabase OAuth on the client |
| `name` | string | no |
| `avatarUrl` | string | no |

**Response 200** = `AuthResponseDto`.

### 1.4 `POST /api/auth/signout`

```http
POST /api/auth/signout
```

**Response 200:** `{ "message": "Signed out successfully" }`.

### 1.5 `GET /api/auth/me`

Returns the currently authenticated profile (Supabase `CurrentUser` server-side).

**Response 200** (`UserProfileDto`):

```json
{
  "id": "8e3c27af-2a0b-41fb-9faa-6e32a2dd9c60",
  "name": "Vithu",
  "avatarUrl": null,
  "createdAt": "2026-04-18T07:03:54Z",
  "email": "langvitu081@gmail.com",
  "isNote": false
}
```

**Errors:** `401 { "error": "Not authenticated" }` · `404 { "error": "User profile not found" }`.

---

## 2. User Profiles — `/api/UserProfile`

Source: [Controllers/UserProfileController.cs](Controllers/UserProfileController.cs).

### 2.1 `GET /api/UserProfile`

List profiles with optional filters & pagination.

**Query params** (`UserProfileQueryParams`):

| Param | Type | Notes |
|---|---|---|
| `search` | string | matches `Name` or `Email` (substring) |
| `isNote` | bool | filter by `IsNote` flag |
| `page` | int | 1-indexed |
| `pageSize` | int | omit (or `0`) for no pagination |

**Response 200** (`PageProfilesResult`):

```json
{
  "items": [
    {
      "id": "8e3c27af-...",
      "name": "Vithu",
      "avatarUrl": null,
      "createdAt": "2026-04-18T07:03:54Z",
      "email": "langvitu081@gmail.com",
      "isNote": false
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 3,
  "hasPrevious": false,
  "hasNext": false
}
```

### 2.2 `GET /api/UserProfile/{id}`

Single profile. **Response 200** = `UserProfileDto`. **`404`** when missing.

### 2.3 `GET /api/UserProfile/{id}/notes`

Returns the profile **plus** every note the user owns (matched on `noteinfo.UserId`). Note: this is owner-only; shared notes (where the user is editor/viewer/deleter) are not included here — use [`GET /api/NoteInfo`](#41-get-apinoteinfo) for that.

**Response 200** (`UserProfileWithNotesDto`):

```json
{
  "id": "8e3c27af-...",
  "name": "Vithu",
  "email": "langvitu081@gmail.com",
  "createdAt": "2026-04-18T07:03:54Z",
  "isNote": false,
  "avatarUrl": null,
  "notes": [
    { "id": 71, "name": "Shared Sample Note", "userId": "8e3c27af-...", "isFavorites": false, "...": "..." }
  ]
}
```

### 2.4 `POST /api/UserProfile`

Create a profile manually (rare — the trigger does this at signup).

**Body:** full `UserProfileDto`. **Response 201** = the created `UserProfileDto`.

### 2.5 `PUT /api/UserProfile/{id}`

Update a profile.

**Body** (`UserProfileUpdateDto`):

| Field | Type |
|---|---|
| `name` | string? |
| `avatarUrl` | string? |
| `email` | string? |
| `isNote` | bool? |

**Response:** `204 No Content` on success, `404` if the profile is missing.

### 2.6 `DELETE /api/UserProfile/{id}`

Delete a profile. Cascades via `ON DELETE SET NULL` — the user's notes / chat messages / device row stay but their FK is null-ed.

**Response:** `204 No Content`.

---

## 3. Devices / FCM — `/api/device`

Source: [Controllers/DeviceController.cs](Controllers/DeviceController.cs). FCM tokens are stored one-per-user in `userdevices` (`UserId` is unique).

### How `userdevices` gets data

| Trigger | What writes |
|---|---|
| New user signs up | DB trigger inserts `(UserId, FCMToken=NULL, CreatedAt=NOW(), UpdatedAt=NOW())` |
| Sign-in with `fcmToken` field | `AuthController.SaveFcmTokenAsync` upserts the token |
| Direct API call | `POST /api/device/save-token` upserts |

### 3.1 `POST /api/device/save-token`

**Body** (`SaveDeviceTokenDto`):

| Field | Type | Required |
|---|---|---|
| `userId` | string | yes |
| `fcmToken` | string | yes |

**Response 200:** `{ "message": "Token saved successfully" }`.
**Errors:** `400 { "error": "UserId is required" }` · `400 { "error": "FCMToken is required" }`.

```bash
curl -X POST http://localhost:5000/api/device/save-token \
  -H "Content-Type: application/json" \
  -d '{"userId":"8e3c27af-2a0b-41fb-9faa-6e32a2dd9c60","fcmToken":"sample_fcm_token_test_001"}'
```

### 3.2 `DELETE /api/device/{userId}`

Remove the device row (e.g. on logout from a particular device).

**Response:** `204 No Content`.

---

## 4. Notes CRUD — `/api/NoteInfo`

Source: [Controllers/NoteInfoController.cs](Controllers/NoteInfoController.cs).

### Note model

`NoteinfoDto`:

| Field | Type | Notes |
|---|---|---|
| `id` | int | PK |
| `name` | string? | |
| `description` | string? | |
| `createdAt` | datetime? | |
| `updatedAt` | datetime? | |
| `userId` | string? | the **owner**'s id (mirrors the owner row in `note_users`) |
| `userIds` | string[]? | full sharing list — owner first, then editors/viewers/deleters |
| `isFavorites` | bool? | |
| `reminder` | datetime? | |

### 4.1 `GET /api/NoteInfo`

List notes with filters & pagination. Includes `userIds` for each note via a single batch lookup.

**Query params** (`NoteQueryParams`):

| Param | Type | Notes |
|---|---|---|
| `search` | string | substring match on `name` or `description` |
| `userId` | string | filter to owner = this user |
| `isFavorites` | bool | |
| `page` | int | 1-indexed |
| `pageSize` | int | `0` or omitted = no paging |

**Response 200** (`PageNotesResult`):

```json
{
  "items": [
    {
      "id": 71,
      "name": "Shared Sample Note",
      "description": "...",
      "createdAt": "2026-05-03T10:11:00Z",
      "updatedAt": "2026-05-03T10:11:00Z",
      "userId": "8e3c27af-...",
      "userIds": [
        "8e3c27af-2a0b-41fb-9faa-6e32a2dd9c60",
        "d1b02488-74f0-4a89-930f-376c2d809487",
        "3d3574b2-fc8b-44b5-bc05-e855dc77e05a"
      ],
      "isFavorites": false,
      "reminder": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 9,
  "hasPrevious": false,
  "hasNext": false
}
```

### 4.2 `GET /api/NoteInfo/{id}`

Single note + full `userIds` list.

**Response 200:** `NoteinfoDto` (see fields above).
**Errors:** `404 { "error": "Note not found" }`.

### 4.3 `POST /api/NoteInfo`

Create a note. The first element of `userIds` becomes the **owner**; the rest become **viewers**. If `userIds` is empty/missing, the legacy single `userId` is used as the owner.

If the `note_users` insert fails after the note row was created, the note row is rolled back (deleted) and the error is propagated.

**Body** (`NoteinfoCreateDto`):

| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | string? | no | |
| `description` | string? | no | |
| `userId` | string? | optional | legacy single-owner field; ignored if `userIds` is non-empty |
| `userIds` | string[]? | optional | first = owner, others = viewer |
| `isFavorites` | bool? | no | |
| `reminder` | datetime? | no | |

**Response 201:** `NoteinfoDto` with `userIds` populated.

```bash
curl -X POST http://localhost:5000/api/NoteInfo \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Q2 Plan",
    "description": "Roadmap doc",
    "userIds": [
      "8e3c27af-2a0b-41fb-9faa-6e32a2dd9c60",
      "d1b02488-74f0-4a89-930f-376c2d809487"
    ],
    "isFavorites": false
  }'
```

### 4.4 `PUT /api/NoteInfo/{id}`

Update a note. The acting user must have at least **edit** permission. If `userIds` is included, the request additionally requires **manage shares** (i.e. owner) — the entire share list is replaced (delete-then-insert).

**Required header:** `X-User-Id: <uuid>`.

**Body** (`NoteinfoUpdateDto`):

| Field | Type | Notes |
|---|---|---|
| `name` | string? | |
| `description` | string? | |
| `userId` | string? | If `userIds` not provided, becomes the new owner |
| `userIds` | string[]? | When present: replaces share list; first element = owner; **owner-only** |
| `isFavorites` | bool? | |
| `reminder` | datetime? | |

**Response:** `204 No Content`.
**Errors:**
- `401 { "error": "X-User-Id header required" }`
- `403 { "error": "User cannot Edit this note" }` / `"User cannot ManageShares this note"`

```bash
curl -X PUT http://localhost:5000/api/NoteInfo/71 \
  -H "X-User-Id: 8e3c27af-2a0b-41fb-9faa-6e32a2dd9c60" \
  -H "Content-Type: application/json" \
  -d '{"name":"Q2 Plan (rev2)"}'
```

### 4.5 `DELETE /api/NoteInfo/{id}`

Hard-delete the note for everyone. `ON DELETE CASCADE` automatically removes every row in `note_users` for that note.

**Required header:** `X-User-Id: <uuid>`.
Allowed roles: `owner`, `deleter`.

**Response:** `204 No Content`.
**Errors:**
- `401 { "error": "X-User-Id header required" }`
- `403 { "error": "User cannot delete this note" }`

### 4.6 `POST /api/NoteInfo/batchCreateNotes`

Create many notes in one call.

**Body** (`BatchNoteinfo`):

```json
{
  "notes": [
    { "name": "...", "description": "...", "userId": "...", "isFavorites": false, "reminder": null }
  ]
}
```

**Response 200** (`BatchCreateNotesResponse`):

```json
{
  "totalCount": 2,
  "successCount": 2,
  "results": [
    { "index": 0, "isSuccess": true, "createdId": 72, "errorMessage": null },
    { "index": 1, "isSuccess": true, "createdId": 73, "errorMessage": null }
  ]
}
```

This endpoint uses single-row `userId` only (no `userIds` array); for multi-user shares use `POST /api/NoteInfo` then add shares via `POST /{id}/share`.

---

## 5. Note Sharing & Permissions — `/api/NoteInfo/{id}/...`

Backed by table `note_users(NoteId, UserId, Role, CreatedAt)` with composite PK `(NoteId, UserId)`. Both FKs `ON DELETE CASCADE`. Constraints:

- `Role IN ('owner','deleter','editor','viewer')` (CHECK constraint)
- Exactly one `owner` per note (`uniq_note_owner` partial unique index)

### Permission matrix

| Role | View | Edit content | Delete note | Manage shares |
|---|---|---|---|---|
| `owner`   | ✅ | ✅ | ✅ | ✅ |
| `deleter` | ✅ | ✅ | ✅ | ❌ |
| `editor`  | ✅ | ✅ | ❌ | ❌ |
| `viewer`  | ✅ | ❌ | ❌ | ❌ |

All endpoints in this section require header `X-User-Id`. The header value is the **caller** (acting user), not the target.

### 5.1 `GET /api/NoteInfo/{id}/shares`

List all share rows for a note. Owner is returned first.

**Response 200** (`NoteShareDto[]`):

```json
[
  { "userId": "8e3c27af-...", "role": "owner",  "createdAt": "2026-05-03T10:11:00Z" },
  { "userId": "d1b02488-...", "role": "editor", "createdAt": "2026-05-03T10:11:00Z" },
  { "userId": "3d3574b2-...", "role": "viewer", "createdAt": "2026-05-03T10:11:00Z" }
]
```

### 5.2 `POST /api/NoteInfo/{id}/share`

Add a user to a note (or update their role if they're already on it).

**Owner-only.** Cannot target the acting user (use `transfer-owner` for that). Cannot promote to `owner` (use `transfer-owner`).

**Body** (`ShareNoteDto`):

| Field | Type | Required |
|---|---|---|
| `userId` | string | yes |
| `role` | enum | yes — one of `deleter`, `editor`, `viewer` |

**Response 200:** `{ "noteId": 71, "userId": "...", "role": "editor" }`.
**Errors:**
- `400 { "error": "userId required; role must be deleter|editor|viewer" }`
- `400 { "error": "Cannot change your own role here; use transfer-owner" }`
- `400 { "error": "Target is already the owner" }`
- `403 { "error": "Only the owner can share this note" }`

```bash
curl -X POST http://localhost:5000/api/NoteInfo/71/share \
  -H "X-User-Id: 8e3c27af-2a0b-41fb-9faa-6e32a2dd9c60" \
  -H "Content-Type: application/json" \
  -d '{"userId":"d1b02488-74f0-4a89-930f-376c2d809487","role":"editor"}'
```

### 5.3 `PUT /api/NoteInfo/{id}/share/{userId}`

Change an existing share's role.

**Owner-only.** Cannot change the owner's role (use `transfer-owner`).

**Body** (`ChangeRoleDto`): `{ "role": "deleter" | "editor" | "viewer" }`.

**Response 200:** `{ "noteId": 71, "userId": "...", "role": "deleter" }`.
**Errors:** `400` (bad role) · `403` (not owner) · `404 { "error": "User is not shared on this note" }` · `400 { "error": "Cannot change owner's role; use transfer-owner" }`.

### 5.4 `DELETE /api/NoteInfo/{id}/share/{userId}`

Revoke another user's access.

**Owner-only.** Cannot revoke the owner directly — call `transfer-owner` first.

**Response:** `204 No Content`.
**Errors:** `403` (not owner) · `404` (user not on note) · `400` (target is owner).

### 5.5 `DELETE /api/NoteInfo/{id}/leave`

"Remove this note from my account." Per-user soft removal — note + other users untouched.

Behaviour:
- **Caller is editor/viewer/deleter** → caller's row removed.
- **Caller is owner with at least one other user** → oldest other user (by `CreatedAt`) is auto-promoted to `owner`, `noteinfo.UserId` updated, then caller's row removed.
- **Caller is owner alone** → blocked (`400`) with hint to share first or call hard-delete.

**Response 204** when leaving without ownership change.
**Response 200** when ownership was transferred:

```json
{ "noteId": 71, "newOwnerId": "d1b02488-...", "message": "Left note; ownership transferred" }
```

**Errors:** `404` (not on note) · `400` (only user on note).

### 5.6 `POST /api/NoteInfo/{id}/transfer-owner`

Hand ownership to another user. The current owner is demoted to `editor`. The target gets promoted to `owner`. `noteinfo.UserId` is synced.

**Owner-only.**

**Body** (`TransferOwnerDto`): `{ "newOwnerId": "<uuid>" }`.

**Response 200:** `{ "noteId": 71, "newOwnerId": "d1b02488-..." }`.
**Errors:** `400 { "error": "newOwnerId required" }` · `403 { "error": "Only the current owner can transfer ownership" }`.

```bash
curl -X POST http://localhost:5000/api/NoteInfo/71/transfer-owner \
  -H "X-User-Id: 8e3c27af-2a0b-41fb-9faa-6e32a2dd9c60" \
  -H "Content-Type: application/json" \
  -d '{"newOwnerId":"d1b02488-74f0-4a89-930f-376c2d809487"}'
```

---

## 6. Chat Messenger — `/api/ChatMessenger`

Source: [Controllers/ChatMessengerController.cs](Controllers/ChatMessengerController.cs).

### Message model

`ChatMessengerDto`:

| Field | Type |
|---|---|
| `id` | int |
| `conversationId` | int |
| `senderId` | string? |
| `receiverId` | string? |
| `content` | string? |
| `messageType` | string? — defaults to `"text"` if omitted |
| `isRead` | bool |
| `createdAt` | datetime? |
| `updatedAt` | datetime? |

### 6.1 `GET /api/ChatMessenger`

List messages with rich filtering. Two modes:

- **Two-way** (when both `senderId` and `receiverId` are set): returns messages between the pair in either direction, ordered by `createdAt` ascending.
- **Single-direction**: filter by any subset of `conversationId`, `senderId`, `receiverId`, `isRead`, `search`.

**Query params** (`ChatMessengerQueryParams`):

| Param | Type |
|---|---|
| `conversationId` | int? |
| `senderId` | string? |
| `receiverId` | string? |
| `isRead` | bool? |
| `search` | string — substring match on `content` |
| `page`, `pageSize` | int — pagination is applied in memory after sorting |

**Response 200** (`PageChatMessengerResult`):

```json
{
  "items": [ /* ChatMessengerDto */ ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 42,
  "hasPrevious": false,
  "hasNext": true
}
```

### 6.2 `GET /api/ChatMessenger/{id}`

Single message. **`200`** = `ChatMessengerDto`. **`404`** if missing.

### 6.3 `GET /api/ChatMessenger/conversation/{conversationId:int}`

All messages in a conversation, sorted by `createdAt` ascending.

**Response 200:** `ChatMessengerDto[]`.

### 6.4 `GET /api/ChatMessenger/unread-count/{receiverId}`

Number of unread messages for a recipient.

**Response 200:** `{ "receiverId": "...", "unreadCount": 5 }`.
**Errors:** `400 { "error": "ReceiverId is required" }`.

### 6.5 `POST /api/ChatMessenger`

Send a message. Server-side behaviour:
1. Validates `senderId` exists in `userinfo`. Validates `receiverId` exists if provided.
2. Inserts the row (auto-generated `id`).
3. Sets `conversationId = id` (1:1 chats use the message id as the conversation id).
4. If receiver has an FCM token in `userdevices`, fires a push notification (failures are swallowed — the message still saves).

**Body** (`ChatMessengerCreateDto`):

| Field | Type | Required |
|---|---|---|
| `senderId` | string | yes — must exist in `userinfo` |
| `receiverId` | string? | optional — must exist in `userinfo` if set |
| `content` | string? | |
| `messageType` | string? | defaults to `"text"` |
| `isRead` | bool? | defaults `false` |

**Response 201:** `ChatMessengerDto`.
**Errors:** `400` for missing/invalid sender or receiver.

```bash
curl -X POST http://localhost:5000/api/ChatMessenger \
  -H "Content-Type: application/json" \
  -d '{
    "senderId":"8e3c27af-2a0b-41fb-9faa-6e32a2dd9c60",
    "receiverId":"d1b02488-74f0-4a89-930f-376c2d809487",
    "content":"Hello!",
    "messageType":"text"
  }'
```

### 6.6 `PUT /api/ChatMessenger/{id}`

Edit a message. Sends only the fields you want to change.

**Body** (`ChatMessengerUpdateDto`):

| Field | Type |
|---|---|
| `senderId` | string? — must exist if changed |
| `receiverId` | string? — must exist if changed |
| `content` | string? |
| `messageType` | string? |
| `isRead` | bool? |

**Response 200:** the updated `ChatMessengerDto`.
**Errors:** `404` (missing) · `400` (sender/receiver not found).

### 6.7 `PUT /api/ChatMessenger/{id}/mark-as-read`

Set `isRead = true`.

**Response 200:** `{ "message": "Message marked as read", "id": 123 }`.
**Errors:** `404 { "error": "Message not found" }`.

### 6.8 `DELETE /api/ChatMessenger/{id}`

**Response 204.** **Errors:** `404 { "error": "Message not found" }`.

---

## 7. Health & Misc

| Method | Path | Purpose |
|---|---|---|
| GET | `/health` | Pings Supabase by listing one `userinfo` row. Used by Render & UptimeRobot. Returns `{ status: "healthy", timestamp }` or `503`. |
| GET | `/` | Redirects to `/swagger`. |

---

## 8. Common DTO schemas

### `AuthResponseDto`
`{ userId: string, email: string?, name: string?, accessToken: string?, refreshToken: string? }`

### `UserProfileDto`
`{ id: string, name: string?, avatarUrl: string?, createdAt: datetime?, email: string?, isNote: bool? }`

### `UserProfileUpdateDto`
`{ name?, avatarUrl?, email?, isNote? }` — all nullable.

### `UserProfileWithNotesDto`
`UserProfileDto` + `notes: NoteinfoDto[]`.

### `NoteinfoDto`
`{ id: int, name?, description?, createdAt?, updatedAt?, userId?, userIds?, isFavorites?, reminder? }`

### `NoteinfoCreateDto`
`{ name?, description?, userId?, userIds?, isFavorites?, reminder? }`

### `NoteinfoUpdateDto`
`{ name?, description?, userId?, userIds?, isFavorites?, reminder? }`

### `NoteShareDto`
`{ userId: string, role: string, createdAt: datetime? }`

### `ShareNoteDto` / `ChangeRoleDto` / `TransferOwnerDto`
`{ userId, role }` / `{ role }` / `{ newOwnerId }`.

### `ChatMessengerDto`
`{ id, conversationId, senderId?, receiverId?, content?, messageType?, isRead, createdAt?, updatedAt? }`

### `SaveDeviceTokenDto`
`{ userId: string, fcmToken: string }`

### Page result wrapper
Every list endpoint returns:
`{ items: [...], page?, pageSize?, totalCount, hasPrevious?, hasNext? }`. Pagination fields are omitted from the response when no pagination is applied.

---

## 9. Database invariants

Enforced by Postgres so the backend can trust them:

- **`userinfo`**
  - PK `Id` is the `auth.users.id` (text uuid)
  - DB trigger `on_auth_user_created` auto-creates a row on signup
- **`userdevices`**
  - `UserId` UNIQUE → safe upsert by user
  - DB trigger `on_auth_user_created_device` auto-creates a row (FCMToken NULL) on signup
  - FK `UserId → userinfo.Id` `ON DELETE SET NULL`
- **`noteinfo`**
  - `UserId` mirrors the **owner** in `note_users` (kept in sync by the controller)
  - FK `UserId → userinfo.Id` `ON DELETE SET NULL`
- **`chatmessenger`**
  - FK `SenderId → userinfo.Id` `ON DELETE SET NULL`
- **`note_users`** (junction)
  - Composite PK `(NoteId, UserId)`
  - `Role IN ('owner','deleter','editor','viewer')` (CHECK)
  - Exactly one `owner` per note (partial unique index `uniq_note_owner`)
  - FK `NoteId → noteinfo.Id` `ON DELETE CASCADE`
  - FK `UserId → userinfo.Id` `ON DELETE CASCADE`
  - RLS: a row is readable only by the user it concerns; only owner can insert

---

## 10. Status code conventions

| Code | Meaning in this API |
|---|---|
| `200 OK` | Success with body |
| `201 Created` | POST that created a resource (also `Location` header) |
| `204 No Content` | Success without body (PUT/DELETE) |
| `400 Bad Request` | Missing/invalid input, business-rule violation |
| `401 Unauthorized` | Missing `X-User-Id` (or Supabase session for `/me`) |
| `403 Forbidden` | Caller authenticated but lacks role for the action |
| `404 Not Found` | Resource absent |
| `503 Service Unavailable` | `/health` only — Supabase ping failed |

Every error body has the shape `{ "error": "..." }` unless otherwise documented.
