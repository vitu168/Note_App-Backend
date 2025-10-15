# Copilot Instructions for NoteApi (.NET Backend)

## Project Overview

This backend powers a professional Flutter note-taking app with a modern UI.  
It is built with C# and .NET, exposing RESTful APIs for user authentication, note management, favorites, archive, reminders, notifications, and user profile management.  
The backend is designed to integrate with a Supabase-powered Flutter frontend.

---

## Project Structure

- **Controllers:** `Controllers/`  
  Contains API controllers for Notes, UserProfile, Auth, etc.
- **Models:** `Models/`  
  Contains data models such as `UserProfile`, `Note`, etc.
- **Data:** `Data/`  
  Contains database context and migration files.
- **Services:** `Services/`  
  Contains business logic and service classes.
- **Program Entry:** `Program.cs`  
  Configures and runs the web API.

---

## UserProfile Model

- File: `Models/user_profile.cs`
- Structure:
  ```csharp
  public class UserProfile
  {
      public string Id { get; set; } = null!;
      public string? FullName { get; set; }
      public string? Email { get; set; }      // Required for frontend profile/settings
      public string? AvatarUrl { get; set; }
      public DateTime? CreatedAt { get; set; }
  }
  ```
- The `Email` property is required to match the Flutter frontend, which displays the user's email in profile and settings screens.

---

## API Guidelines

- Expose RESTful endpoints for all core features:
  - Notes CRUD (create, read, update, delete)
  - Favorites and Archive management
  - Reminders
  - Notifications
  - User Profile CRUD
- All endpoints must return JSON.
- Use async/await for all database operations.
- Validate all incoming data and return appropriate HTTP status codes.
- Secure endpoints using authentication (JWT or Supabase Auth integration).
- Support real-time updates for notes and reminders if possible.

---

## Data Storage

- Use Supabase as the primary data store.
- All persistent data (notes, user profiles, reminders, etc.) must be stored in Supabase tables.
- Do not store sensitive data on the device or in memory.
- Ensure all data access is secured with row-level security (RLS) policies in Supabase.

---

## Coding Style

- Use C# best practices and .NET conventions.
- Use async/await for all I/O operations.
- Use dependency injection for services.
- Use descriptive variable and method names.
- Add comments for complex logic.

---

## Do Not

- Do not store sensitive data on the device.
- Do not use global variables for state.
- Do not expose sensitive information in API responses.
- Do not use deprecated .NET APIs.

---

**End of Copilot Instructions**