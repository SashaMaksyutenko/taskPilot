// NotificationType and the delivery message moved to the Taskpilot.Contracts assembly
// (shared with the notification service). A global using keeps the existing test
// references resolving without editing each file.
global using Taskpilot.Contracts;
// Notification senders + their options moved to Taskpilot.Integrations (shared with the
// notification service). A global using keeps the existing test references resolving.
global using Taskpilot.Integrations;
