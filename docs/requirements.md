# TaskPilot — Requirements & Feature Specification

> **Your co-pilot for team productivity.**
> Full-stack team collaboration platform: projects & tasks, real-time
> communication, marketplace, forum, analytics, admin & moderation.
>
> Scope: 30 feature modules · ~200+ hours · 6–8 weeks. This document is the
> source of truth for what TaskPilot must contain. Status notes mark what is
> already built vs pending.

---

## Complete Feature List (30 modules)

### 1. Projects & Tasks
Create/edit/delete projects · tasks with priority/deadline/assignee · statuses
Backlog → In Progress → Review → Done · subtasks · task history (audit trail) ·
archive/restore · task details with comments · drag-drop priority changes ·
bulk operations (multi-select, change status) · time tracking (optional).

### 2. Group Chat (by project/task)
Real-time SignalR messages · auto-created per project and per task · @mentions +
notifications · emoji reactions · pin messages · message search · rich text
(bold/italic/code) · file attachments · edit/delete own messages · read
receipts · typing indicator · message history.

### 3. Private Messaging (Direct Messages)
One-to-one chat · same features as group chat · online/offline status ·
conversation history · archive conversation · block user (optional) · last seen.

### 4. Forum & Discussions
Create topics · browse (paginated, searchable, filterable) · nested replies ·
quote/reply · upvote/downvote · mark reply as solution · pin (admin) · lock
topics · delete topics/replies · reputation system · badges ("Helpful",
"Expert") · views counter · notify on replies · rich text editor.

### 5. Task Marketplace (Public Tasks)
Manager posts public tasks (budget, deadline, skills) · developers browse &
filter · apply with cover letter + rate · manager reviews/accepts/rejects ·
developer notified · progress tracking · complete & submit · manager approves ·
two-way rating · payment tracking · task history.

### 6. Notifications & Alerts
**Types:** task assigned, deadline reminders, comments/mentions, status changes,
application updates, chat messages, forum replies, team updates.
**Channels:** Email (HTML), Viber Bot API, Push (browser), in-app bell,
daily/weekly digest.
**Preferences:** enable/disable per type, choose channels, frequency, quiet
hours (22:00–08:00), mute specific chat/project.

### 7. File Sharing & Attachments
Upload to tasks/messages/forum posts · preview (images, PDFs, docs) ·
download/delete/share (link) · version history · max 10MB/file · 1GB/org.

### 8. Notes & Bookmarks
**Notes:** rich text, tags, color coding, pin, share, export PDF, search/filter.
**Bookmarks:** tasks, messages, files, forum topics — quick-access list.

### 9. Calendar & Timeline
Month/Week/Day views · color indicators (Green=Done, Yellow=In Progress,
Red=Overdue, Blue=Due today, White=Backlog) · drag-drop reschedule · Gantt view ·
team availability · navigation · Google Calendar sync · iCal export.

### 10. User Profiles & Reputation
Profile: avatar, name, email, title, location, timezone, skills, bio.
Stats: tasks completed, on-time rate, reputation points, badges, projects.
Badges: Expert, Helpful, Responsive, Productive, Team Player, Trusted, Leader.
Reviews: average rating, written reviews, endorsements.

### 11. Personal Dashboard
Quick stats · my tasks · recent activity · weekly metrics · messages &
notifications · calendar widget · suggestions.

### 12. Admin Dashboard
KPIs · charts · user management · team management · project & task monitoring ·
analytics/reports · org settings · audit logs · Viber integration · system
health · file management · security.

### 13. Chat Bot (command-based)
Commands: `/help /tasks /team /status /deadline /online /create /find /search
/remind /summarize` · smart responses · `@bot` mention · action suggestions.

### 14. Telegram Bot
Same commands · inline buttons (View / Mark Done / Reply) · quick replies that
update the app.

### 15. Role-Based Access Control (RBAC)
- **Developer:** own tasks, chat, files, forum, notes, marketplace.
- **Manager:** + projects, assign tasks, team management, analytics.
- **Admin:** + all users, all data, org config, audit logs, moderation.
- **Viewer:** read-only (no create/edit/chat).

### 16. Search & Filtering
Global search (tasks, users, files, forum) · advanced filters everywhere · save
searches (optional).

### 17. Settings & Customization
**User:** profile (name, email, avatar), password change, theme Light/Dark
(saved per user), language EN/UK/RU (full i18n), notification preferences,
privacy settings.
**Org (admin):** general (name, logo, timezone), members (invite, limit,
domain), projects (defaults, templates), features toggle, file storage limits,
security (2FA, IP whitelist).

### 18. Security & Compliance
**Auth:** email+password, Google OAuth 2.0, GitHub OAuth 2.0, LinkedIn OAuth 2.0.
**Security:** JWT (15 min) + refresh (7 days) · RBAC · audit logs · file
encryption (optional) · GDPR data export · account deletion on request · 2FA
(optional or admin-required) · session management (view/revoke) · API keys ·
**rate limiting** · IP whitelist · CORS · BCrypt · HMAC-SHA256 webhook
signatures.

### 19. Calendar & Task Timeline
Multiple views · status colors · date info · overdue counter · drag-drop · Gantt
· team calendar · export (iCal/CSV/PDF).

### 20. Overdue Task Handling & Escalation
Day 1: notification · Day 3: manager notified · Day 5: critical · Day 7+: admin
notified · configurable escalation. Warnings: Day 3 (soft), Day 5 (-5 rep), Day
7 (-10 rep). Extension request/approval system.

### 21. Warnings & Escalation System
3 warning levels → suspension after 3 in 30 days · appeal system · manager can
lift · auto-clear after 30 days.

### 22. Reputation & Reviews System
**Earned:** on-time +10, early +15, upvote +5, solution +50, 5-star +15,
marketplace +25. **Lost:** 1-day late -2, 3-day -5, 5-day -10, warning -5,
1-star -10. Badges at: 50 upvotes, 200 rep, 500 rep, 1000 rep, 20+ tasks.

### 23. Bans & Restrictions
Temporary (7–30d) or permanent · appeal once · during ban: view only, can
download own files · moderation: delete/hide/flag message, mute (24h), ban.

### 24. Admin Moderation & Permissions
User: reassign tasks, delete, role change, ban, warn, activity view. Content:
delete/hide posts, lock/pin topics, mute/ban. Settings: notifications,
restrictions, features, storage, limits, integrations.

### 25. Appeals & Dispute System
Appeal ban/warning/deletion with reason + evidence · admin response within 3
days · decision logged · reputation partially restored if accepted.

### 26. Webhooks & External Integrations
Outgoing HTTP POST on events (Slack, Jira, GitHub, Zapier react in real time).
**Events:** task.created/updated/completed/overdue, project.created/archived,
user.joined/banned, marketplace.application.accepted,
marketplace.task.completed, comment.created, mention.triggered, warning.issued,
escalation.triggered.
**Features:** admin configures URLs per event · HMAC-SHA256 signature · retry
(3 attempts) · delivery logs · test button · pause/resume · per-project config.

### 27. Reports & Export
**Report types:** task report · team performance · project health · marketplace
report · user activity · audit log · overdue & escalation.
**Formats:** PDF (with charts), Excel/XLSX, CSV, JSON.
**Features:** scheduled (daily/weekly/monthly auto-email) · custom date range ·
filters · charts in PDF · download or email · history (last 30) · shareable link.
**Access:** Manager (own team/projects), Admin (full org), Developer (own stats).

### 28. Localization (i18n)
Languages: EN (default), UK, RU. Localized: all UI labels/buttons/menus/tooltips,
error & validation text, email templates, date/time & number formats, calendar
names, bot responses. Tech: react-i18next · `en.json`/`uk.json`/`ru.json` ·
language saved in profile · fallback to EN · admin sets org default.

### 29. Branding & UI/UX
**Identity:** name TaskPilot · tagline "Your co-pilot for team productivity" ·
custom SVG compass/cockpit logo · primary deep blue/indigo · accent orange/cyan ·
Inter font · favicon · landing page for guests (hero, features, CTA, sign up).
**Themes:** Light (default) / Dark · saved per user · Tailwind class-based.
**Animations:** Framer Motion transitions · skeleton loaders · Lottie (empty/
success states) · upload progress bar · confetti on task completion · animated
notification badge.
**UX:** confirm modals before destructive actions · empty-state illustrations ·
toast notifications (top-right, auto-dismiss) · responsive · keyboard shortcuts ·
tooltips.

### 30. Context Menus & Right-Click Actions
Hover shows ⋮ three-dot button (top-right of card/row) with quick actions.
Right-click (or long-press on mobile) opens full context menu at cursor.
Per-entity actions for tasks, projects, chat messages, forum topics/replies,
files, users, dashboard/reports. Tech: radix-ui/context-menu or cmdk · keyboard
navigation · Esc/click-outside closes · respects permissions · logs actions.

---

## Technology Stack

**Backend (.NET 8 / ASP.NET Core / C#):** PostgreSQL · Redis · EF Core · RabbitMQ
+ MassTransit · SignalR · JWT + refresh + OAuth 2.0 (Google/GitHub/LinkedIn) ·
BCrypt · SendGrid · Viber Bot API · OpenAI (optional) · Serilog · xUnit + Moq ·
FluentValidation · AutoMapper · Swagger · built-in rate limiting · HMAC-SHA256 ·
QuestPDF/iTextSharp (PDF) · ClosedXML (Excel).

**Frontend (React 18 + TypeScript):** Redux Toolkit · React Query · Axios ·
SignalR JS · React Hook Form · Zod · Tailwind CSS · react-i18next · framer-motion
· lottie-react · @fontsource/inter · radix-ui/context-menu · Recharts ·
react-big-calendar · Slate.js · React Dropzone · React Toastify · React Router ·
Vite · date-fns · Vitest + RTL + Cypress (optional) · class-based dark/light.

**DevOps:** Docker + Docker Compose · GitHub + GitHub Actions · Kubernetes
(optional).

**External services:** Google/GitHub/LinkedIn OAuth · Viber Bot API · SendGrid
(100/day free) · Telegram Bot API · OpenAI (optional) · Firebase Cloud Messaging
(optional) · Zapier/Make (via webhooks).

**Deployment (free):** Frontend → Vercel · Backend → Railway · Email → SendGrid ·
Viber/Telegram/Google OAuth free. Target cost: $0/month.

---

## Development Timeline (planned)

- **Week 1 — Foundation + Auth** (~20 sessions): user model, DbContext, register,
  login, JWT, refresh, /me; React init, Redux, axios, register/login pages,
  protected routes.
- **Week 2 — Communication:** group chat (SignalR), private messaging, file upload.
- **Week 3 — Features:** forum, marketplace, notifications (SendGrid + Viber +
  RabbitMQ).
- **Week 4 — Admin & Polish:** admin panel, profiles + dashboard, calendar.
- **Week 5 — Integrations & Quality:** chat bot + Telegram, webhooks, reports &
  export, tests, Docker + CI/CD, i18n (EN/UK), theme polish.
- **Week 6 — Branding & Deployment:** SVG logo + favicon, landing page, Framer
  Motion, skeleton loaders, deploy (Vercel + Railway), screenshots, README.

---

## Target Repository Structure

```
TaskPilot/
├── src/
│   ├── Taskpilot.API/                 (Controllers, Services, Hubs, Models, Data,
│   │                                   Auth, Webhooks, Reports, Validators, Middleware)
│   ├── Taskpilot.NotificationService/ (Email/Viber/Push services, workers)
│   └── Taskpilot.Frontend/            (components, pages, services, store, hooks,
│                                       types, locales, animations, assets, styles)
├── tests/
│   ├── Taskpilot.API.Tests/
│   └── Taskpilot.Frontend.Tests/
├── docker-compose.yml
├── .github/workflows/
├── docs/   (architecture.md, api-docs.md, logo-usage.md, requirements.md)
├── README.md
└── LICENSE
```

> **Note:** the working rules for development (one task per session, mandatory
> comments & logging, security standards, session start/end protocol) live in the
> separate "Claude Working Instructions" document.
