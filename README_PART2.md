# RaceDay — Part 2: RESTful API

**IIE Rosebank College | POE Part 2**
**Student:** Junior (st10469891)

---

## System Description

**RaceDay** is a full-stack event management system for the South African road running, walking, and cycling community. This Part 2 repository contains the complete **RESTful API** built with ASP.NET Core 8 Web API and Entity Framework Core.

## Two User Roles

### Organiser
An Organiser is a club, race director, or sports organisation that runs events.
- Create, edit, and delete events and categories
- View all participants enrolled in their events
- Capture and update participant results after the race

### Participant
A Participant is a runner, cyclist, or walker who enters events.
- Register an account and log in
- Browse all upcoming events
- Enrol in events and track their personal results

> Role-based access is enforced at the **API level** using server-side sessions. Organisers cannot access Participant endpoints and vice versa.

---

## Setup Instructions

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- SQL Server (local instance or SQL Server Express)
- SQL Server Management Studio (SSMS) — optional

### 1. Clone the repository
```bash
git clone <your-github-repo-url>
cd raceday-poe
```

### 2. Configure the database connection
Edit `RaceDay.API/appsettings.json` and update the connection string:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.;Database=RaceDay;Trusted_Connection=True;TrustServerCertificate=True;"
}
```
Replace `.` with your SQL Server instance name if different (e.g. `.\SQLEXPRESS`).

### 3. Apply EF Core migrations to create the database
```bash
cd RaceDay.API
dotnet ef migrations add InitialCreate
dotnet ef database update
```
This creates the `RaceDay` database with all seven tables matching the Part 1 SQL script.

### 4. Run the API
```bash
dotnet run
```
The API starts at `https://localhost:5001` (or `http://localhost:5000`).
Swagger UI opens automatically at the root URL: `http://localhost:5000`

### 5. Run unit tests
```bash
cd RaceDay.Tests
dotnet test
```
Tests use an **in-memory database** — no SQL Server connection is needed.

---

## API Endpoints Summary

| Method | Route | Role | Description |
|--------|-------|------|-------------|
| POST | /api/auth/register | Public | Register as Organiser or Participant |
| POST | /api/auth/login | Public | Log in and start session |
| POST | /api/auth/logout | Any | End the session |
| GET | /api/users/profile | Any | View own profile |
| PUT | /api/users/profile | Any | Update own profile |
| DELETE | /api/users/{id} | Any | Delete own account |
| GET | /api/events | Public | List all active events |
| GET | /api/events/{id} | Public | Get one event |
| POST | /api/events | Organiser | Create event |
| PUT | /api/events/{id} | Organiser | Update event |
| DELETE | /api/events/{id} | Organiser | Delete event |
| GET | /api/events/{id}/enrolments | Organiser | View enrolments for event |
| GET | /api/categories | Public | List all categories |
| GET | /api/categories/{id} | Public | Get one category |
| POST | /api/categories | Organiser | Create category |
| PUT | /api/categories/{id} | Organiser | Update category |
| DELETE | /api/categories/{id} | Organiser | Delete category |
| POST | /api/enrolments | Participant | Enrol in an event |
| GET | /api/enrolments/my | Participant | View own enrolments |
| DELETE | /api/enrolments/{id} | Participant | Cancel enrolment |
| POST | /api/results | Organiser | Capture race result |
| PUT | /api/results/{id} | Organiser | Update result |
| GET | /api/results/my | Participant | View own race history |

---

## Project Structure

```
RaceDay.API/
├── Controllers/       — 6 API controllers
├── Data/              — EF Core DbContext (Code-First)
├── DTOs/              — Request body models
├── Filters/           — SessionAuthorize action filter
├── Helpers/           — BCrypt password helper
└── Models/            — 7 entity classes (matches Part 1 ERD)

RaceDay.Tests/
├── Helpers/           — MockSession, TestHelpers
├── AuthControllerTests.cs
├── EventsControllerTests.cs
├── CategoriesControllerTests.cs
├── EnrolmentsControllerTests.cs
├── ResultsControllerTests.cs
└── RoleAccessTests.cs
```

---

## Authentication

This API uses **server-side sessions** (not JWT tokens). On successful login:
1. The server creates a session and stores `UserId`, `UserRole`, and `UserName`
2. A session cookie is sent to the client automatically
3. All subsequent requests include this cookie
4. Protected endpoints read the session to confirm identity and role

To test in Swagger:
1. Call `POST /api/auth/login` — the session cookie is set automatically in the browser
2. All subsequent Swagger calls will include the session cookie

---

## CI/CD

GitHub Actions workflow: `.github/workflows/part2-ci.yml`

The workflow:
1. Restores NuGet packages for both projects
2. Builds `RaceDay.API` in Release mode
3. Builds `RaceDay.Tests` in Release mode
4. Runs all unit tests (in-memory DB, no SQL Server needed)
5. Verifies the `/docs` folder from Part 1 still exists

**CI/CD Screenshot:** *(<img width="1917" height="936" alt="image" src="https://github.com/user-attachments/assets/1b7e8a9a-2ce4-44f9-9dc8-47ef7dd45ffd" />
)*

---

## YouTube Video

*(Add your unlisted YouTube video link here)*

---

## AI Tool Disclosure

As permitted by the assignment, AI tools (Claude) were used during:
- Initial project scaffolding and structure planning
- Code review and debugging
- Understanding EF Core fluent API configuration

All code was reviewed, understood, and modified by the student. The final implementation reflects the student's own decisions regarding architecture, naming conventions, and design choices.
