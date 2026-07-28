/**
 * Content for the public docs/help site (rendered as Markdown). Each article is bilingual;
 * the docs page picks `en` or `uk` from the active i18n language. Keep articles short and
 * practical — they mirror how the app actually works.
 */
export interface DocArticle {
  slug: string
  title: { en: string; uk: string }
  body: { en: string; uk: string }
}

export const DOC_ARTICLES: DocArticle[] = [
  {
    slug: 'getting-started',
    title: { en: 'Getting started', uk: 'Початок роботи' },
    body: {
      en: `## Welcome to TaskPilot

TaskPilot is an all-in-one workspace: projects, tasks, real-time chat, a task marketplace, forum, notes and an AI assistant.

**First steps**
1. Create an account (or sign in with Google, GitHub or LinkedIn).
2. Create your first **project** from the Projects page.
3. Add **tasks** to the board and drag them across the Backlog → In Progress → Review → Done columns.
4. Invite teammates from the project's **Members** button.

That's it — you're ready to work. The sections on the left cover each feature in more detail.`,
      uk: `## Ласкаво просимо в TaskPilot

TaskPilot — це універсальний робочий простір: проєкти, задачі, чат у реальному часі, маркетплейс задач, форум, нотатки та AI-асистент.

**Перші кроки**
1. Створіть акаунт (або увійдіть через Google, GitHub чи LinkedIn).
2. Створіть перший **проєкт** на сторінці «Проєкти».
3. Додайте **задачі** на дошку й перетягуйте їх колонками Backlog → In Progress → Review → Done.
4. Запросіть колег кнопкою **«Учасники»** у проєкті.

Ось і все — можна працювати. Розділи ліворуч описують кожну можливість детальніше.`,
    },
  },
  {
    slug: 'projects-and-boards',
    title: { en: 'Projects & boards', uk: 'Проєкти та дошки' },
    body: {
      en: `## Projects & Kanban boards

Each project has a **Kanban board** with four columns: Backlog, In Progress, Review and Done.

- **Move a task** by dragging its card between columns.
- Only the **owner** can move a task to *Done* (approving it); an assignee can push their own task to *Review*.
- Use **sort** to order tasks within a column by priority, deadline or title.
- **Members & roles:** the owner adds collaborators as **Editor** (can create/edit tasks) or **Viewer** (read-only).
- Export the board to **CSV, Excel or PDF** from the board toolbar.`,
      uk: `## Проєкти та Kanban-дошки

У кожного проєкту є **Kanban-дошка** з чотирма колонками: Backlog, In Progress, Review і Done.

- **Перемістити задачу** — перетягніть картку між колонками.
- У *Done* задачу переводить лише **власник** (підтверджує її); виконавець може перевести свою задачу в *Review*.
- **Сортування** впорядковує задачі в колонці за пріоритетом, дедлайном або назвою.
- **Учасники та ролі:** власник додає колег як **Editor** (створює/редагує задачі) або **Viewer** (лише перегляд).
- Дошку можна експортувати в **CSV, Excel або PDF** з панелі інструментів.`,
    },
  },
  {
    slug: 'tasks',
    title: { en: 'Tasks', uk: 'Задачі' },
    body: {
      en: `## Working with tasks

Open a task to edit its details:

- **Priority** (Low / Medium / High) and **deadline** (owner-only).
- **Assignee** — search a project member to assign.
- **Tags**, **subtasks**, **comments** and a built-in **time tracker**.
- **Recurring tasks:** set *Repeat* to Daily / Weekly / Monthly. When you complete a recurring task, TaskPilot automatically creates the next occurrence with its deadline moved forward. A 🔁 badge marks recurring cards.

You can also request a **deadline extension**, which the owner approves or rejects.`,
      uk: `## Робота із задачами

Відкрийте задачу, щоб редагувати її деталі:

- **Пріоритет** (Low / Medium / High) і **дедлайн** (лише власник).
- **Виконавець** — знайдіть учасника проєкту, щоб призначити.
- **Теги**, **підзадачі**, **коментарі** та вбудований **таймер**.
- **Повторювані задачі:** увімкніть *Повторення* — Щодня / Щотижня / Щомісяця. Коли ви завершуєте повторювану задачу, TaskPilot автоматично створює наступну зі зсунутим дедлайном. Картки з повторенням позначені 🔁.

Також можна **запросити продовження дедлайну** — власник підтверджує або відхиляє.`,
    },
  },
  {
    slug: 'automations',
    title: { en: 'Automations', uk: 'Автоматизації' },
    body: {
      en: `## Automations ("robots")

Automations run an action automatically when something happens to a task. The project owner manages them from the **Automations** button on the board.

**A rule is:** *When [trigger] → [action]*.

- **Triggers:** a task is created, or a task moves to a status (e.g. Done).
- **Actions:** set priority, assign to a member, notify the project owner, or add a comment.

For example: *When a task moves to Review → notify the owner*, or *When a task is created → assign to the team lead*. Rules can be turned on/off at any time.`,
      uk: `## Автоматизації («роботи»)

Автоматизації виконують дію самі, коли щось стається із задачею. Керує ними власник проєкту через кнопку **«Автоматизації»** на дошці.

**Правило:** *Коли [тригер] → [дія]*.

- **Тригери:** задачу створено або переведено в статус (напр. Done).
- **Дії:** встановити пріоритет, призначити учасника, сповістити власника проєкту або додати коментар.

Наприклад: *Коли задачу переведено в Review → сповістити власника*, або *Коли створено задачу → призначити тімліду*. Правила можна вмикати/вимикати будь-коли.`,
    },
  },
  {
    slug: 'ai-assistant',
    title: { en: 'AI assistant', uk: 'AI-асистент' },
    body: {
      en: `## AI assistant

The built-in assistant answers questions about your data **and takes actions** on your behalf. Open it from the sidebar or the "Ask AI" button.

**Ask it things like:**
- "What tasks of mine are overdue?"
- "What's due this week?"

**Or tell it to do things:**
- "Create a task 'Write docs' in the Website project and move it to In Progress."
- "Start a forum topic about our release process."
- "Message Bob that the report is ready."

It works with your own permissions — it can only do what you could do yourself. You can also chat with it over **Telegram** once you link your account in Settings.`,
      uk: `## AI-асистент

Вбудований асистент відповідає на запитання про ваші дані **та виконує дії** за вас. Відкрийте його з бічного меню або кнопкою «Ask AI».

**Запитуйте, наприклад:**
- «Які мої задачі прострочені?»
- «Що з дедлайнами цього тижня?»

**Або доручайте дії:**
- «Створи задачу "Написати доки" в проєкті Website і переведи в In Progress».
- «Відкрий тему на форумі про наш процес релізів».
- «Напиши Bob, що звіт готовий».

Він діє з вашими правами — може лише те, що можете ви. Спілкуватися з ним можна й через **Telegram**, залінкувавши акаунт у Налаштуваннях.`,
    },
  },
  {
    slug: 'notifications-integrations',
    title: { en: 'Notifications & integrations', uk: 'Сповіщення та інтеграції' },
    body: {
      en: `## Notifications & integrations

Stay in the loop across channels:

- **In-app** notifications and real-time toasts.
- **Email** digests — choose *Off*, *Daily* or *Weekly* in Settings.
- **Telegram / Viber** — link your account in Settings to receive notifications (and chat with the AI assistant over Telegram).
- **Browser push** — enable it to get alerts even when the tab is closed.
- **Quiet hours** — silence email/push during a nightly window; in-app notifications still arrive.

You can also mute a whole project or specific notification types.`,
      uk: `## Сповіщення та інтеграції

Будьте в курсі через різні канали:

- **У застосунку** — сповіщення й тости в реальному часі.
- **Email-дайджести** — оберіть *Вимк.*, *Щодня* або *Щотижня* в Налаштуваннях.
- **Telegram / Viber** — залінкуйте акаунт у Налаштуваннях, щоб отримувати сповіщення (і спілкуватися з AI-асистентом через Telegram).
- **Push у браузері** — увімкніть, щоб отримувати сповіщення навіть із закритою вкладкою.
- **Тихі години** — вимикають email/push у нічному вікні; сповіщення в застосунку все одно надходять.

Також можна заглушити цілий проєкт або окремі типи сповіщень.`,
    },
  },
]
