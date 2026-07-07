# TaskPilot

> **Your co-pilot for team productivity.**

TaskPilot is a full-stack team-collaboration platform: projects & tasks, a
Kanban board, real-time chat and notifications, a task marketplace, a forum,
a calendar, analytics, and an admin/moderation area.

- **Backend** — ASP.NET Core (.NET 8) Web API, EF Core 8, PostgreSQL, SignalR, JWT auth
- **Frontend** — React 19 + TypeScript + Vite, Redux Toolkit, Tailwind CSS, react-i18next (EN/UK)
- **Infrastructure** — PostgreSQL, optional Redis cache, optional RabbitMQ

The full feature specification (30 modules) lives in [docs/requirements.md](docs/requirements.md).

---

## Quick start with Docker

The fastest way to run the whole stack (API + frontend + PostgreSQL + Redis + RabbitMQ):

```bash
cp .env.example .env          # then edit .env and set JWT_KEY + ADMIN_PASSWORD
docker-compose up -d
```

- Frontend: http://localhost:3000
- API: http://localhost:8080
- RabbitMQ dashboard: http://localhost:15672 (guest / guest)

The frontend is served by nginx and proxies `/api` and `/hubs` to the API, so the
browser talks to a single origin (no CORS setup needed).

---

## Local development (without Docker)

### Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/download) (see [global.json](global.json))
- [Node.js 20+](https://nodejs.org/)
- A PostgreSQL instance (or run just the database via `docker-compose up -d postgres`)

### 1. Backend

```bash
cd src/Taskpilot.API
cp .env.example .env          # then fill in the values (see Configuration below)
dotnet ef database update     # apply migrations (needs ConnectionStrings__DefaultConnection)
dotnet run
```

The API reads `.env` **at startup** (via DotNetEnv), so restart it after any `.env` change.
By default the `http` profile listens on **http://localhost:5025**.

An initial admin account is created/promoted on startup from the `Admin__*` values.

### 2. Frontend

```bash
cd src/Taskpilot.Frontend
cp .env.example .env          # point VITE_API_URL at the API (default http://localhost:5025)
npm install
npm run dev                   # http://localhost:5173
```

Vite only exposes variables prefixed with `VITE_`, and reads them **at startup** —
restart the dev server after editing `.env`. Never put secrets in `VITE_*`; those
values are shipped to the browser.

---

## Configuration

Secrets live only in the gitignored `.env` files (or .NET User Secrets) — never in
source control. Copy each `.env.example` to `.env` and fill in real values. .NET maps
the `__` separator to the `:` configuration hierarchy.

### Required

| Key | Where | Purpose |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | API | PostgreSQL connection string |
| `Jwt__Key` | API | Long random secret for signing JWTs |
| `Admin__Email` / `Admin__Password` | API | Initial admin account seeded on startup |

### Optional integrations

Every integration below is **config-gated**: leave it empty and the related feature
is simply disabled (the app still runs). Enable it by filling in the values.

| Feature | Keys (API `.env`) | Notes |
| --- | --- | --- |
| **Google sign-in** | `GoogleOAuth__ClientId` / `ClientSecret` / `RedirectUri` + frontend `VITE_GOOGLE_CLIENT_ID` | From Google Cloud Console |
| **GitHub sign-in** | `GitHubOAuth__ClientId` / `ClientSecret` / `RedirectUri` + frontend `VITE_GITHUB_CLIENT_ID` | From GitHub Developer settings |
| **Email** | `Email__SmtpHost` / `SmtpPort` / `SmtpUser` / `SmtpPassword` (or `Email__ApiKey` for SendGrid) | SMTP is used when `SmtpHost` is set; SendGrid API is the fallback |
| **Telegram bot** | `Telegram__BotToken` / `BotUsername` | Token from @BotFather |
| **Web push** | `Vapid__Subject` / `PublicKey` / `PrivateKey` | The API logs a fresh VAPID key pair at startup when these are empty |
| **Redis cache** | `Redis__Connection` (e.g. `localhost:6379`) | Empty = in-memory cache |

See [src/Taskpilot.API/.env.example](src/Taskpilot.API/.env.example) and
[src/Taskpilot.Frontend/.env.example](src/Taskpilot.Frontend/.env.example) for the
full list with inline notes.

### Calendar subscription

Each user gets a private, auto-updating iCal feed (Calendar → **Subscribe**). Add the
URL to Google/Apple/Outlook Calendar to keep deadlines in sync. The feed URL is built
from the API's public host, so an external calendar service can only reach it once the
API is deployed to a public domain — on `localhost` use **Export .ics** for a one-time
import instead.

---

## Testing

```bash
# Backend unit tests
dotnet test

# Frontend type-check and production build
cd src/Taskpilot.Frontend
npx tsc --noEmit
npm run build
```

---

## Project structure

```
TaskPilot/
├─ src/
│  ├─ Taskpilot.API/         ASP.NET Core Web API (controllers, services, EF Core, SignalR hubs)
│  └─ Taskpilot.Frontend/    React + TypeScript + Vite SPA
├─ tests/
│  └─ Taskpilot.API.Tests/   xUnit backend tests
├─ docs/                     Requirements spec and screenshots
├─ docker-compose.yml        Full local stack (API, frontend, Postgres, Redis, RabbitMQ)
└─ Dockerfile                Backend API image
```
