# Deploying TaskPilot

Free-tier deployment: **PostgreSQL on Neon**, **backend on Render**, **frontend on Vercel**.
Total cost: $0/month.

Nothing here is host-specific: the API reads the standard `PORT` and `DATABASE_URL`
variables every container platform injects, so Railway, Fly.io, Koyeb or a VPS work the
same way — only the dashboard clicks differ.

The order matters — database first, then the backend, then the frontend, and finally back
to the backend to tell it the frontend's address (CORS). Each step below says why.

> Free tiers change often. Check the current limits on each provider before relying on them.

---

## 1. Database on Neon

Neon gives a free serverless Postgres that does not expire, which is why it is used here
instead of a host-bundled database.

1. Sign up at [neon.tech](https://neon.tech) and create a project (any region near you).
2. Copy the **connection string** it shows — it looks like:

   ```
   postgresql://user:password@ep-something.eu-central-1.aws.neon.tech/dbname?sslmode=require
   ```

That single string is all the API needs; it is converted to the form Npgsql expects
automatically, including the TLS settings.

---

## 2. Backend on Render

1. Sign up at [render.com](https://render.com) → *New* → **Web Service** → connect this
   GitHub repository.
2. **Runtime: Docker.** Render finds the root `Dockerfile` on its own — leave the build and
   start commands empty. The image reads the `PORT` Render injects.
3. **Instance type:** Free.
4. **Environment variables:**

   | Variable | Value |
   | --- | --- |
   | `DATABASE_URL` | the Neon connection string from step 1 |
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

5. **Deploy**, then note the URL, e.g. `https://taskpilot-api.onrender.com`.
6. **Check it is alive:** open `https://<your-api-domain>/health` — it should report
   `"status":"ok"` and `"database":"up"`. If the database line is missing or the service
   crashes, the connection string is wrong — that is the first thing to check.

Database migrations run automatically on startup, so there is no manual migration step.

**Free instances sleep** after about 15 minutes of inactivity; the next request wakes them
and can take 30–60 seconds. Fine for a portfolio demo — just do not panic at the first
slow load, and open the API URL once before showing the app to someone.

---

## 3. Frontend on Vercel

1. [vercel.com](https://vercel.com) → *Add New* → *Project* → import the same repository.
2. **Root Directory:** `src/Taskpilot.Frontend` ← easy to miss; without it the build fails.
   Framework preset *Vite* and the default build command/output (`npm run build` → `dist`)
   are correct.
3. **Environment variable:**

   | Variable | Value |
   | --- | --- |
   | `VITE_API_URL` | your Render API URL, e.g. `https://taskpilot-api.onrender.com` |

   This is baked in at build time, so changing it later requires a redeploy.
4. **Deploy**, then note the domain, e.g. `https://taskpilot.vercel.app`.

`vercel.json` in the frontend folder rewrites unknown paths to `index.html` so that
client-side routes (`/projects/<id>`) survive a refresh or a shared link.

---

## 4. Connect the two (CORS)

Back on Render, add one more variable to the API service:

| Variable | Value |
| --- | --- |
| `Cors__AllowedOrigins` | your Vercel URL, e.g. `https://taskpilot.vercel.app` |

Render redeploys automatically. **Until this is set, the browser blocks every request
from the deployed site** — the app will look broken with network errors in the console,
even though the API itself is healthy.

Multiple origins are comma-separated. A trailing slash is tolerated. `http://localhost:5173`
is always allowed, so local development keeps working against the deployed API.

---

## 5. Verify

1. Open the Vercel URL and register an account (or log in with the admin credentials set
   in step 2).
2. Create a project and a task — this proves the database is connected and writable.
3. Open the browser console: no CORS errors.

---

## Optional integrations

Everything below is **off unless configured** — the app runs fine without any of it.
Add the variables on Render to switch a feature on:

| Feature | Variables |
| --- | --- |
| Email (password reset, digests) | `Email__SmtpHost`, `Email__SmtpPort`, `Email__SmtpUser`, `Email__SmtpPassword`, `Email__FromAddress` — or `Email__ApiKey` for SendGrid |
| Google / GitHub / LinkedIn sign-in | `GoogleOAuth__ClientId` + `ClientSecret`, `GitHubOAuth__*`, `LinkedInOAuth__*` |
| GitHub account link (per-user repos) | `GitHubIntegration__ClientId`, `GitHubIntegration__ClientSecret` |
| AI assistant & subtasks | `OpenAi__ApiKey` |
| Stripe marketplace payments | `Stripe__SecretKey`, `Stripe__WebhookSecret` |
| Stripe subscriptions (Free/Pro plan) | `Stripe__SecretKey`, `Stripe__ProPriceId`, `Stripe__WebhookSecret` |
| Telegram / Viber bots | `Telegram__BotToken`, `Viber__AuthToken` |
| Web push | `Vapid__PublicKey`, `Vapid__PrivateKey`, `Vapid__Subject` |
| Passkeys (WebAuthn) | `Fido2__ServerDomain`, `Fido2__Origins__0` |
| Redis cache | `Redis__Connection` |
| RabbitMQ queue | `RabbitMq__Connection` |
| S3/R2 file storage | `Storage__Bucket`, `Storage__AccessKey`, `Storage__SecretKey`, `Storage__ServiceUrl` |

**OAuth redirect URIs must be updated** in each provider's console to the deployed
frontend, e.g. `https://taskpilot.vercel.app/auth/google/callback`. The frontend also
needs the matching public client ids at build time (`VITE_GOOGLE_CLIENT_ID`,
`VITE_GITHUB_CLIENT_ID`, `VITE_LINKEDIN_CLIENT_ID`) on Vercel.

**Passkeys (WebAuthn)** run their ceremony in the browser, so the relying-party id and origin
**must match the frontend domain** — otherwise registration and sign-in are rejected. Set on Render:

- `Fido2__ServerDomain` = the bare host, e.g. `taskpilot.vercel.app` (no scheme, no path, no port).
- `Fido2__Origins__0` = the full origin, e.g. `https://taskpilot.vercel.app` (no trailing slash).

Left unset, they default to `localhost` / `http://localhost:5173` (local dev only). Add one origin
per index (`Fido2__Origins__1`, …) if the app is served from more than one domain.

**Stripe subscriptions (Free/Pro plan)** power the workspace plan and its limits. Enabling them:

1. In the Stripe Dashboard **stay in one mode** — use **Test mode** (or a *Sandbox*) with test keys for demos; no account activation is needed. Live mode requires activating the account.
2. Create a **Product** (e.g. "Pro") with a **recurring** Price, and copy its **Price id** (`price_…`, *not* the product id `prod_…`).
3. Add a **webhook endpoint** at `https://<api>/api/billing/webhook` subscribed to `checkout.session.completed`, `customer.subscription.updated` and `customer.subscription.deleted`; copy its **signing secret** (`whsec_…`).
4. Set on Render: `Stripe__SecretKey` (`sk_…`), `Stripe__ProPriceId` (`price_…`), `Stripe__WebhookSecret` (`whsec_…`).

The keys, product/price and webhook must **all be from the same Stripe account and the same mode** (mixing test/live gives "No such price"). Checkout is server-side, so the **publishable key is not used**. **Until this is configured the plan gate is off** — everything (projects, AI, automations, whiteboard) stays unlocked, so it never blocks a workspace with no payment provider. Once on, **Free** caps projects (3) and locks AI/automations/whiteboard; **Pro** unlocks them. Test upgrades with card `4242 4242 4242 4242`.

**Google Calendar 2-way sync** reuses the same Google OAuth app (no extra secrets). To turn it
on in the Google Cloud Console:

1. **Enable the Google Calendar API** for the project (APIs & Services → Library → *Google Calendar API* → Enable).
2. Add **`https://<frontend>/calendar`** to the OAuth client's *Authorized redirect URIs* (this is where Google returns the user after consent — it is separate from the sign-in callback; the origin, without the path, must already be in *Authorized JavaScript origins*).
3. While the OAuth app is in **Testing**, add each user's email under *Audience → Test users*.

> ⚠️ In *Testing* mode Google **expires the refresh token after 7 days**, so users must reconnect
> weekly. Publish the app (Publishing status → *Publish*) for a permanent connection — the
> `calendar.events` scope is *sensitive*, so publishing may require Google verification.

**GitHub account link (per-user repos)** lets each user connect *their own* GitHub account and
browse their repositories from **Settings** — separate from the project webhook integration. Its
callback is `https://<frontend>/settings`.

You can **reuse the GitHub sign-in OAuth app** — modern OAuth apps allow up to 10 redirect URIs, so
one app serves both:

1. Open the OAuth app (GitHub → *Settings → Developer settings → OAuth Apps*).
2. Under **Redirect URIs**, **Add redirect URI** = `https://<frontend>/settings` (keep the existing
   sign-in callback `.../auth/github/callback`), then **Update application**. A wildcard on the
   sign-in URI does *not* cover `/settings`, so add it explicitly.
3. Set on Render: `GitHubIntegration__ClientId` and `GitHubIntegration__ClientSecret` — the **same
   values** as `GitHubOAuth__ClientId` / `GitHubOAuth__ClientSecret` (no new secret needed).

*(Prefer isolation? Create a separate OAuth App instead, with callback `https://<frontend>/settings`,
and use its own client id/secret.)*

The `repo` scope is requested by the app at connect time (nothing to configure on the OAuth app).
Left unset, the GitHub section on Settings is hidden. The access token is stored server-side and
used only to list the user's repositories on their behalf. For local testing, also add
`http://localhost:5173/settings` as a redirect URI.

---

## Things worth knowing

- **File uploads are ephemeral by default.** A container's filesystem resets on every
  deploy (and on Render's free tier, on every sleep/wake), so uploaded files and avatars
  disappear. Configure the `Storage__*` variables above — Cloudflare R2 has a free tier —
  to keep them.
- **Locking yourself out of registration.** The admin panel can restrict sign-ups by email
  domain and cap the member count. An allowlist means *only* those domains may register —
  leave it empty for an open demo.
- **Free-tier sleeping.** Render idles a free service after inactivity; the first request
  afterwards is slow while it wakes. Hitting `/health` before a demo warms it up.
- **Secrets never go in the repo.** `.env` is gitignored; everything above lives in the
  Render/Vercel dashboards.

### Other hosts

The app is not tied to Render. Anything that can run a Docker container and set
environment variables works, because the image reads the standard `PORT` and the API
accepts a standard `DATABASE_URL`:

- **Fly.io** — no sleeping, but usually asks for a card.
- **Koyeb** — free instance, similar to Render.
- **Railway** — the original target; its free trial is time-limited, so it now needs a
  paid plan.
- **A VPS** — `docker compose up` with the bundled `docker-compose.yml`.
