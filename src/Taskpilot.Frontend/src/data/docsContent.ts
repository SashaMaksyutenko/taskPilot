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

**Just want to look around?** If the demo is enabled, click **Try the live demo** on the landing or login page — it drops you into a ready-made workspace (a sample project, tasks and a note) with no signup. Demo accounts are temporary and are cleaned up automatically, so explore freely.

**First steps**
1. Create an account (or sign in with Google, GitHub or LinkedIn).
2. Create your first **project** from the Projects page.
3. Add **tasks** to the board and drag them across the Backlog → In Progress → Review → Done columns.
4. Invite teammates from the project's **Members** button.

That's it — you're ready to work. The sections on the left cover each feature in more detail.

**Tips:** press **⌘/Ctrl + K** to open the command palette and jump anywhere, and pick an **accent colour** in Settings.`,
      uk: `## Ласкаво просимо в TaskPilot

TaskPilot — це універсальний робочий простір: проєкти, задачі, чат у реальному часі, маркетплейс задач, форум, нотатки та AI-асистент.

**Просто хочете роздивитись?** Якщо демо ввімкнене, натисніть **«Спробувати демо»** на головній або сторінці входу — і ви одразу опинитесь у готовому робочому просторі (приклад проєкту, задачі та нотатка) без реєстрації. Демо-акаунти тимчасові й прибираються автоматично, тож досліджуйте вільно.

**Перші кроки**
1. Створіть акаунт (або увійдіть через Google, GitHub чи LinkedIn).
2. Створіть перший **проєкт** на сторінці «Проєкти».
3. Додайте **задачі** на дошку й перетягуйте їх колонками Backlog → In Progress → Review → Done.
4. Запросіть колег кнопкою **«Учасники»** у проєкті.

Ось і все — можна працювати. Розділи ліворуч описують кожну можливість детальніше.

**Поради:** натисніть **⌘/Ctrl + K**, щоб відкрити командну палітру й перейти будь-куди, а в Налаштуваннях оберіть **колір акценту**.`,
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
- **Filter** the board by assignee, priority or tags; **select** cards for bulk actions (move, assign, set priority, delete).
- **Members & roles:** the owner adds collaborators as **Editor** (can create/edit tasks) or **Viewer** (read-only).
- **WIP limits:** cap how many tasks a column may hold — the board blocks moving a card into a full column.
- Export the board to **CSV, Excel or PDF**, or import tasks from **CSV**, from the board toolbar.`,
      uk: `## Проєкти та Kanban-дошки

У кожного проєкту є **Kanban-дошка** з чотирма колонками: Backlog, In Progress, Review і Done.

- **Перемістити задачу** — перетягніть картку між колонками.
- У *Done* задачу переводить лише **власник** (підтверджує її); виконавець може перевести свою задачу в *Review*.
- **Сортування** впорядковує задачі в колонці за пріоритетом, дедлайном або назвою.
- **Фільтруйте** дошку за виконавцем, пріоритетом чи тегами; **виділяйте** картки для групових дій (перемістити, призначити, змінити пріоритет, видалити).
- **Учасники та ролі:** власник додає колег як **Editor** (створює/редагує задачі) або **Viewer** (лише перегляд).
- **WIP-ліміти:** обмежте кількість задач у колонці — дошка не дасть перемістити картку в переповнену колонку.
- Дошку можна експортувати в **CSV, Excel або PDF** чи імпортувати задачі з **CSV** — з панелі інструментів.`,
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

You can also request a **deadline extension**, which the owner approves or rejects. See **Advanced task features** for dependencies, custom fields, watchers and attachments.`,
      uk: `## Робота із задачами

Відкрийте задачу, щоб редагувати її деталі:

- **Пріоритет** (Low / Medium / High) і **дедлайн** (лише власник).
- **Виконавець** — знайдіть учасника проєкту, щоб призначити.
- **Теги**, **підзадачі**, **коментарі** та вбудований **таймер**.
- **Повторювані задачі:** увімкніть *Повторення* — Щодня / Щотижня / Щомісяця. Коли ви завершуєте повторювану задачу, TaskPilot автоматично створює наступну зі зсунутим дедлайном. Картки з повторенням позначені 🔁.

Також можна **запросити продовження дедлайну** — власник підтверджує або відхиляє. Про залежності, кастомні поля, спостерігачів та вкладення — див. **Розширені можливості задач**.`,
    },
  },
  {
    slug: 'advanced-tasks',
    title: { en: 'Advanced task features', uk: 'Розширені можливості задач' },
    body: {
      en: `## Advanced task features

Inside a task you'll also find:

- **Dependencies** — mark a task as blocked by another. A blocked task can't move to *Done* until its blockers are finished; TaskPilot prevents dependency cycles.
- **Custom fields** — the owner/editors define per-project fields (Text, Number, Select, Date) via the board's **Fields** button; fill them on each task.
- **Watchers** — press *Watch* to receive a task's notifications even when you're not the assignee. You're auto-subscribed when you create, are assigned to, comment on or are @mentioned in a task.
- **Attachments & versions** — attach files to a task and upload new versions; the full history is kept.
- **Comments & reactions** — discuss inline, @mention teammates, edit your own comments and add emoji reactions.
- **Epics & sprints** — group a task into an epic (a colour-coded theme) or assign it to a sprint from the task's pickers.`,
      uk: `## Розширені можливості задач

Усередині задачі також є:

- **Залежності** — позначте задачу як заблоковану іншою. Заблокована задача не перейде в *Done*, доки не завершено її блокери; TaskPilot не дає створити цикл залежностей.
- **Кастомні поля** — власник/редактори визначають поля проєкту (Text, Number, Select, Date) через кнопку **«Поля»** на дошці; заповнюйте їх у кожній задачі.
- **Спостерігачі** — натисніть *Стежити*, щоб отримувати сповіщення задачі, навіть не будучи виконавцем. Ви підписуєтесь автоматично, коли створюєте задачу, отримуєте її, коментуєте або вас @згадали.
- **Вкладення та версії** — прикріплюйте файли до задачі й завантажуйте нові версії; зберігається вся історія.
- **Коментарі та реакції** — обговорюйте всередині, @згадуйте колег, редагуйте свої коментарі й додавайте емодзі-реакції.
- **Епіки та спрінти** — згрупуйте задачу в епік (кольорову тему) або призначте в спрінт через відповідні селектори в задачі.`,
    },
  },
  {
    slug: 'board-views',
    title: { en: 'Board views & planning', uk: 'Вигляди дошки та планування' },
    body: {
      en: `## Board views & planning

The board toolbar switches between views of the same tasks:

- **Board** — the Kanban columns.
- **Timeline (Gantt)** — tasks as bars from creation to deadline, with the **critical path** (the bottleneck sequence) highlighted.
- **Team** — who is busy with what over the next month.
- **Analytics** — a burn-up of created vs completed, status/priority mix, average cycle time, throughput and workload per assignee.
- **Sprints** — plan iterations: create sprints, drag tasks in and out of the backlog, add **story-point estimates**, and track **velocity** (committed vs completed points).
- **Activity** — a timeline of who did what in the project.

**Epics** group tasks under a colour-coded theme that spans several sprints; the epic chip shows on each card.`,
      uk: `## Вигляди дошки та планування

Панель дошки перемикає вигляди тих самих задач:

- **Дошка** — колонки Kanban.
- **Таймлайн (Gantt)** — задачі як смуги від створення до дедлайну, з підсвіченим **критичним шляхом** (послідовність-«вузьке місце»).
- **Команда** — хто чим зайнятий наступного місяця.
- **Аналітика** — burn-up створених проти виконаних, розподіл за статусом/пріоритетом, середній час циклу, пропускна здатність і завантаження на виконавця.
- **Спрінти** — плануйте ітерації: створюйте спрінти, перетягуйте задачі з беклогу й назад, додавайте **оцінки в story-points** і стежте за **швидкістю** (заплановані проти виконаних балів).
- **Активність** — стрічка, хто що робив у проєкті.

**Епіки** групують задачі під кольоровою темою, що охоплює кілька спрінтів; чип епіка видно на кожній картці.`,
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
    slug: 'reports-sharing',
    title: { en: 'Reports & sharing', uk: 'Звіти та поширення' },
    body: {
      en: `## Reports & sharing

- **Export & import** — export a board to **CSV, Excel or PDF**, or bulk-create tasks by importing a **CSV**.
- **Reports** — from the board's **Reports** menu, generate a **project-health** or **team-performance** report as PDF or Excel.
- **Scheduled emails** — have those reports emailed **daily, weekly or monthly**.
- **Public share link** — publish a **read-only** view of a board that anyone can open without an account; revoke the link at any time.

Personal exports live in Settings — you can also download an **activity report** of your own work (PDF/Excel).`,
      uk: `## Звіти та поширення

- **Експорт та імпорт** — експортуйте дошку в **CSV, Excel або PDF** чи масово створюйте задачі, імпортувавши **CSV**.
- **Звіти** — у меню **«Звіти»** на дошці згенеруйте звіт **здоров'я проєкту** або **продуктивності команди** у PDF чи Excel.
- **Заплановані листи** — отримуйте ці звіти на пошту **щодня, щотижня чи щомісяця**.
- **Публічне посилання** — опублікуйте **лише для читання** вигляд дошки, який відкриється будь-кому без акаунта; посилання можна відкликати будь-коли.

Особисті експорти — у Налаштуваннях; там же можна завантажити **звіт власної активності** (PDF/Excel).`,
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

It works with your own permissions — it can only do what you could do yourself. You can also chat with it over **Telegram** once you link your account in Settings.

The dashboard also shows an AI **weekly digest** — a short summary of what happened across your projects. And when a task is complex, open it and use **Break into subtasks** to have the AI draft a checklist.`,
      uk: `## AI-асистент

Вбудований асистент відповідає на запитання про ваші дані **та виконує дії** за вас. Відкрийте його з бічного меню або кнопкою «Ask AI».

**Запитуйте, наприклад:**
- «Які мої задачі прострочені?»
- «Що з дедлайнами цього тижня?»

**Або доручайте дії:**
- «Створи задачу "Написати доки" в проєкті Website і переведи в In Progress».
- «Відкрий тему на форумі про наш процес релізів».
- «Напиши Bob, що звіт готовий».

Він діє з вашими правами — може лише те, що можете ви. Спілкуватися з ним можна й через **Telegram**, залінкувавши акаунт у Налаштуваннях.

На дашборді також є AI-**щотижневий дайджест** — короткий підсумок подій у ваших проєктах. А коли задача складна — відкрийте її та скористайтесь **«Розбити на підзадачі»**, щоб AI склав чек-лист.`,
    },
  },
  {
    slug: 'chat',
    title: { en: 'Chat', uk: 'Чат' },
    body: {
      en: `## Real-time chat

Message teammates one-to-one or in group conversations — messages arrive instantly.

- **Rich messages** — attachments, GIFs and @mentions.
- **React, edit & pin** — add emoji reactions, edit or delete your own messages, and pin important ones.
- **Presence** — a typing indicator and read receipts ("Seen").
- **Deep links** — copy a link to any message to share it.
- **Control** — **mute** a conversation to stop its notifications, or **block** a user in a direct chat.

Start a chat by searching for a person in the conversation list.`,
      uk: `## Чат у реальному часі

Спілкуйтесь із колегами віч-на-віч або в групових розмовах — повідомлення надходять миттєво.

- **Насичені повідомлення** — вкладення, GIF та @згадки.
- **Реакції, редагування, закріплення** — додавайте емодзі-реакції, редагуйте чи видаляйте свої повідомлення, закріплюйте важливі.
- **Присутність** — індикатор набору й позначки прочитання («Переглянуто»).
- **Прямі посилання** — скопіюйте посилання на будь-яке повідомлення, щоб поділитись.
- **Керування** — **заглушіть** розмову, щоб прибрати її сповіщення, або **заблокуйте** користувача в приватному чаті.

Щоб почати чат, знайдіть людину в списку розмов.`,
    },
  },
  {
    slug: 'forum',
    title: { en: 'Forum', uk: 'Форум' },
    body: {
      en: `## Community forum

Ask questions and share knowledge in threaded discussions.

- **Start a topic** with Markdown, **tags** and file attachments.
- **Reply and quote**, and **upvote / downvote** replies to surface the best answers.
- **Accepted solution** — the topic author can mark one reply as the solution; solved topics get a ✓ badge.
- **Find topics** — search, filter by tag or solved/unsolved, and sort by latest, active or top.
- **Subscribe** to a topic to be notified of new replies.
- **Moderation** — admins (and the author) can pin or lock a topic; report a reply to send it to moderators.`,
      uk: `## Форум спільноти

Ставте запитання та діліться знаннями в обговореннях.

- **Створіть тему** з Markdown, **тегами** та вкладеннями.
- **Відповідайте й цитуйте**, а також **голосуйте за/проти** відповідей, щоб найкращі були нагорі.
- **Прийняте рішення** — автор теми може позначити одну відповідь як рішення; вирішені теми мають позначку ✓.
- **Пошук тем** — шукайте, фільтруйте за тегом чи вирішеністю, сортуйте за новими, активними чи топовими.
- **Підпишіться** на тему, щоб отримувати сповіщення про нові відповіді.
- **Модерація** — адміни (і автор) можуть закріпити чи заблокувати тему; поскаржтесь на відповідь, щоб надіслати її модераторам.`,
    },
  },
  {
    slug: 'marketplace',
    title: { en: 'Task marketplace', uk: 'Маркетплейс задач' },
    body: {
      en: `## Task marketplace

Post paid tasks and hire people to do them.

1. **Post** — Managers and Admins publish a task with a budget and required skills.
2. **Apply** — anyone can apply with a cover letter and a proposed rate.
3. **Accept** — the poster reviews applications and accepts one.
4. **Deliver** — the assignee submits their finished work; the poster approves it.
5. **Pay** — pay securely through **Stripe**.
6. **Review** — afterwards both sides leave a star rating and a review, which builds reputation.`,
      uk: `## Маркетплейс задач

Публікуйте оплачувані задачі й наймайте виконавців.

1. **Публікація** — Manager та Admin розміщують задачу з бюджетом і потрібними навичками.
2. **Заявка** — будь-хто може подати заявку із супровідним листом і запропонованою ставкою.
3. **Прийняття** — автор переглядає заявки й приймає одну.
4. **Здача** — виконавець надсилає готову роботу; автор її схвалює.
5. **Оплата** — безпечна оплата через **Stripe**.
6. **Відгук** — після завершення обидві сторони залишають зіркову оцінку й відгук, що формує репутацію.`,
    },
  },
  {
    slug: 'notes-bookmarks-search',
    title: { en: 'Notes, bookmarks & search', uk: 'Нотатки, закладки та пошук' },
    body: {
      en: `## Notes, bookmarks & search

- **Notes** — keep personal, colour-tagged notes with tags; pin the important ones and export any note to **PDF**.
- **Bookmarks** — save any **task, forum topic, chat message or file** (from its right-click menu) for quick access on the Bookmarks page.
- **Search** — a global search across projects, tasks, topics and users. **Save** frequent searches to re-run them in one click.
- **Semantic search** — when the server has it enabled, switch to *Semantic* mode to find tasks and notes **by meaning**, not just exact words.`,
      uk: `## Нотатки, закладки та пошук

- **Нотатки** — ведіть особисті кольорові нотатки з тегами; закріплюйте важливі й експортуйте будь-яку в **PDF**.
- **Закладки** — збережіть будь-яку **задачу, тему форуму, повідомлення чату або файл** (з контекстного меню) для швидкого доступу на сторінці «Закладки».
- **Пошук** — глобальний пошук по проєктах, задачах, темах і користувачах. **Зберігайте** часті запити, щоб виконувати їх одним кліком.
- **Семантичний пошук** — якщо його ввімкнено на сервері, перемкніться в режим *Semantic*, щоб знаходити задачі й нотатки **за змістом**, а не лише за точними словами.`,
    },
  },
  {
    slug: 'community-reputation',
    title: { en: 'Community & reputation', uk: 'Спільнота та репутація' },
    body: {
      en: `## Community & reputation

TaskPilot rewards good work across the community.

- **Reputation** — earn points for finishing tasks on time, having a forum reply accepted as the solution, and completing marketplace tasks.
- **Leaderboard** — see the top contributors and your own standing.
- **Achievements** — unlock badges as you hit milestones (first task, ten/fifty tasks, an on-time streak, a solution, a completed trade).
- **Profiles** — every user has a public profile with their title, location, **skills** (which peers can **endorse**), reviews received and a reputation history.`,
      uk: `## Спільнота та репутація

TaskPilot заохочує якісну роботу в межах спільноти.

- **Репутація** — заробляйте бали за вчасне завершення задач, прийняту як рішення відповідь на форумі та виконані задачі маркетплейсу.
- **Рейтинг** — дивіться найкращих учасників і власну позицію.
- **Досягнення** — відкривайте бейджі за віхи (перша задача, десять/п'ятдесят задач, серія вчасних, рішення, завершена угода).
- **Профілі** — у кожного користувача є публічний профіль із посадою, локацією, **навичками** (які колеги можуть **підтверджувати**), отриманими відгуками та історією репутації.`,
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
- **Google Calendar** — on the *Calendar* page press *Connect* for two-way sync of your task deadlines (moving an event in Google reschedules the task), or *Subscribe* for a read-only iCal feed.
- **GitHub** — the project owner can link a repository (board **GitHub** button) so commits and pull requests that mention a task connect to it; when a PR that closes a task is merged, the task moves to **Review** by default so a person still confirms it — or straight to *Done*, or nowhere (the owner picks). Separately, connect **your own** GitHub account in *Settings* to browse your repositories.
- **Quiet hours** — silence email/push during a nightly window; in-app notifications still arrive.

You can also mute a whole project or specific notification types.`,
      uk: `## Сповіщення та інтеграції

Будьте в курсі через різні канали:

- **У застосунку** — сповіщення й тости в реальному часі.
- **Email-дайджести** — оберіть *Вимк.*, *Щодня* або *Щотижня* в Налаштуваннях.
- **Telegram / Viber** — залінкуйте акаунт у Налаштуваннях, щоб отримувати сповіщення (і спілкуватися з AI-асистентом через Telegram).
- **Push у браузері** — увімкніть, щоб отримувати сповіщення навіть із закритою вкладкою.
- **Google Календар** — на сторінці *Календар* натисніть *Підключити* для двосторонньої синхронізації дедлайнів (переміщення події в Google переносить задачу), або *Підписатися* для read-only iCal-фіду.
- **GitHub** — власник проєкту може під'єднати репозиторій (кнопка **GitHub** на дошці), щоб комміти та pull request-и, які згадують задачу, лінкувались до неї; коли merged-PR закриває задачу, вона за замовчуванням переходить у **Review**, щоб людина її ще підтвердила — або одразу в *Done*, або нікуди (обирає власник). Окремо в *Налаштуваннях* можна під'єднати **свій** GitHub і переглядати власні репозиторії.
- **Тихі години** — вимикають email/push у нічному вікні; сповіщення в застосунку все одно надходять.

Також можна заглушити цілий проєкт або окремі типи сповіщень.`,
    },
  },
  {
    slug: 'account-security',
    title: { en: 'Account & security', uk: 'Акаунт і безпека' },
    body: {
      en: `## Account & security

Everything about your account lives in **Settings**:

- **Profile** — name, title, bio, location, skills and contact links; choose an **accent colour** and light/dark theme.
- **Two-factor authentication** — protect sign-in with an authenticator app and one-time **backup codes**.
- **Passkeys** — sign in without a password using Face ID, Touch ID, Windows Hello or a security key. Add one under *Settings → Passkeys*, then use **Sign in with a passkey** on the login page (enter your email, then confirm on your device). Each device is registered separately; Apple passkeys sync across your devices via iCloud.
- **Sessions** — see your active devices and **revoke** any of them.
- **API keys** — create personal keys for programmatic access.
- **Webhooks** — POST events to your own URL, with a delivery log and a test button.
- **Privacy** — **export all your data** (GDPR) or **delete your account**.

TaskPilot is also a **PWA** — install it from your browser to run it like a native app, online or offline.`,
      uk: `## Акаунт і безпека

Усе про ваш акаунт — у **Налаштуваннях**:

- **Профіль** — ім'я, посада, біо, локація, навички та контактні посилання; оберіть **колір акценту** та світлу/темну тему.
- **Двофакторна автентифікація** — захистіть вхід застосунком-автентифікатором і одноразовими **резервними кодами**.
- **Passkeys** — вхід без пароля через Face ID, Touch ID, Windows Hello чи апаратний ключ. Додайте його в *Налаштування → Passkeys*, а потім користуйтесь кнопкою **«Увійти через passkey»** на сторінці входу (введіть email і підтвердьте на своєму пристрої). Кожен пристрій реєструється окремо; passkeys Apple синхронізуються між вашими пристроями через iCloud.
- **Сесії** — переглядайте активні пристрої та **відкликайте** будь-який.
- **API-ключі** — створюйте особисті ключі для програмного доступу.
- **Вебхуки** — надсилайте події POST-ом на власний URL, із журналом доставок і кнопкою тесту.
- **Приватність** — **експортуйте всі свої дані** (GDPR) або **видаліть акаунт**.

TaskPilot також є **PWA** — встановіть його з браузера, щоб запускати як застосунок, онлайн чи офлайн.`,
    },
  },
  {
    slug: 'admin',
    title: { en: 'Admin & moderation', uk: 'Адміністрування та модерація' },
    body: {
      en: `## Admin & moderation

Admins get an **Admin** section with extra controls:

- **Users** — search members, change **roles** (User / Manager / Admin) and **ban** or **mute** accounts.
- **Moderation** — issue **warnings**, review **appeals**, and resolve **reported** forum replies; repeated warnings can auto-ban.
- **Analytics** — organisation-wide charts (sign-ups, activity, tasks by status) and an **audit log** of recorded actions.
- **Organisation settings** — set the workspace **name & logo** (branding), toggle **features** on/off, and configure **registration** rules.
- **Plans & billing** — the workspace runs on a **Free** or **Pro** plan. From *Admin → Settings → Plan & billing* an admin can **Upgrade to Pro** (Stripe Checkout) or open the billing portal to manage/cancel. **Free** caps the number of projects and keeps the AI assistant, automations and the whiteboard locked; **Pro** unlocks them all and removes the limit. **Enterprise** is custom — contact sales from the Pricing page. (When no payment provider is configured, everything stays unlocked.)`,
      uk: `## Адміністрування та модерація

Адміни отримують розділ **«Admin»** з додатковими можливостями:

- **Користувачі** — шукайте учасників, змінюйте **ролі** (User / Manager / Admin) та **банте** чи **заглушуйте** акаунти.
- **Модерація** — видавайте **попередження**, розглядайте **апеляції** й вирішуйте **скарги** на відповіді форуму; повторні попередження можуть автоматично забанити.
- **Аналітика** — графіки по всій організації (реєстрації, активність, задачі за статусом) та **журнал дій** (audit log).
- **Налаштування організації** — задайте **назву й логотип** простору (брендинг), вмикайте/вимикайте **функції** та налаштовуйте правила **реєстрації**.
- **Тариф і оплата** — воркспейс працює на тарифі **Free** або **Pro**. У *Admin → Налаштування → Тариф і оплата* адмін може **Перейти на Pro** (Stripe Checkout) або відкрити портал оплати для керування/скасування. **Free** обмежує кількість проєктів і тримає AI-асистента, автоматизації та whiteboard закритими; **Pro** їх розблоковує й знімає ліміт. **Enterprise** — індивідуально, звертайтесь через сторінку Тарифів. (Якщо платіжний провайдер не налаштований — усе лишається відкритим.)`,
    },
  },
]
