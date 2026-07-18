# Deploying TaskPilot

Free-tier deployment: **backend + PostgreSQL on Railway**, **frontend on Vercel**.
Total cost: $0/month.

The order matters — deploy the backend first, the frontend second, then come back and
tell the backend the frontend's address (CORS). Each step below says why.

---

## 1. Backend + database on Railway

1. **Create the project.** [railway.app](https://railway.app) → *New Project* →
   *Deploy from GitHub repo* → pick this repository.
2. **Add PostgreSQL.** In the project: *New* → *Database* → *Add PostgreSQL*.
   Railway creates it and exposes a `DATABASE_URL` variable.
3. **Point the API at the database.** Open the API service → *Variables* → add:

   | Variable | Value |
   | --- | --- |
   | `DATABASE_URL` | `${{Postgres.DATABASE_URL}}` (Railway's reference syntax) |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `Jwt__Key` | a long random string (see below) |
   | `Jwt__Issuer` | `Taskpilot.API` |
   | `Jwt__Audience` | `Taskpilot.Client` |
   | `Admin__Email` | your admin login |
   | `Admin__Password` | a strong password |

   Generate a signing key:

   ```bash
   openssl rand -base64 48
   ```

4. **Deploy.** Railway builds the root `Dockerfile` automatically. No start command
   needed — the image reads Railway's injected `PORT`.
5. **Generate a domain.** Service → *Settings* → *Networking* → *Generate Domain*.
   Note it down, e.g. `https://taskpilot-api.up.railway.app`.
6. **Check it is alive:** open `https://<your-api-domain>/health` — it should return `ok`.

Database migrations run automatically on startup, so there is no manual migration step.

---

## 2. Frontend on Vercel

1. [vercel.com](https://vercel.com) → *Add New* → *Project* → import the same repository.
2. **Root Directory:** `src/Taskpilot.Frontend` ← easy to miss; without it the build fails.
   Framework preset *Vite* and the default build command/output (`npm run build` → `dist`)
   are correct.
3. **Environment variable:**

   | Variable | Value |
   | --- | --- |
   | `VITE_API_URL` | your Railway API URL, e.g. `https://taskpilot-api.up.railway.app` |

   This is baked in at build time, so changing it later requires a redeploy.
4. **Deploy**, then note the domain, e.g. `https://taskpilot.vercel.app`.

`vercel.json` in the frontend folder rewrites unknown paths to `index.html` so that
client-side routes (`/projects/<id>`) survive a refresh or a shared link.

---

## 3. Connect the two (CORS)

Back on Railway, add one more variable to the API service:

| Variable | Value |
| --- | --- |
| `Cors__AllowedOrigins` | your Vercel URL, e.g. `https://taskpilot.vercel.app` |

Railway redeploys automatically. **Until this is set, the browser blocks every request
from the deployed site** — the app will look broken with network errors in the console,
even though the API itself is healthy.

Multiple origins are comma-separated. A trailing slash is tolerated. `http://localhost:5173`
is always allowed, so local development keeps working against the deployed API.

---

## 4. Verify

1. Open the Vercel URL and register an account (or log in with the admin credentials set
   in step 1).
2. Create a project and a task — this proves the database is connected and writable.
3. Open the browser console: no CORS errors.

---

## Optional integrations

Everything below is **off unless configured** — the app runs fine without any of it.
Add the variables on Railway to switch a feature on:

| Feature | Variables |
| --- | --- |
| Email (password reset, digests) | `Email__SmtpHost`, `Email__SmtpPort`, `Email__SmtpUser`, `Email__SmtpPassword`, `Email__FromAddress` — or `Email__ApiKey` for SendGrid |
| Google / GitHub / LinkedIn sign-in | `GoogleOAuth__ClientId` + `ClientSecret`, `GitHubOAuth__*`, `LinkedInOAuth__*` |
| AI assistant & subtasks | `OpenAi__ApiKey` |
| Stripe marketplace payments | `Stripe__SecretKey`, `Stripe__WebhookSecret` |
| Telegram / Viber bots | `Telegram__BotToken`, `Viber__AuthToken` |
| Web push | `Vapid__PublicKey`, `Vapid__PrivateKey`, `Vapid__Subject` |
| Redis cache | `Redis__Connection` |
| RabbitMQ queue | `RabbitMq__Connection` |
| S3/R2 file storage | `Storage__Bucket`, `Storage__AccessKey`, `Storage__SecretKey`, `Storage__ServiceUrl` |

**OAuth redirect URIs must be updated** in each provider's console to the deployed
frontend, e.g. `https://taskpilot.vercel.app/auth/google/callback`. The frontend also
needs the matching public client ids at build time (`VITE_GOOGLE_CLIENT_ID`,
`VITE_GITHUB_CLIENT_ID`, `VITE_LINKEDIN_CLIENT_ID`) on Vercel.

---

## Things worth knowing

- **File uploads are ephemeral by default.** Railway's filesystem resets on every deploy,
  so uploaded files and avatars disappear. Configure the `Storage__*` variables above
  (Cloudflare R2 has a free tier) to keep them.
- **Locking yourself out of registration.** The admin panel can restrict sign-ups by email
  domain and cap the member count. An allowlist means *only* those domains may register —
  leave it empty for an open demo.
- **Free-tier sleeping.** Railway's free plan may idle the service; the first request after
  an idle period is slow while it wakes.
- **Secrets never go in the repo.** `.env` is gitignored; everything above lives in the
  Railway/Vercel dashboards.
