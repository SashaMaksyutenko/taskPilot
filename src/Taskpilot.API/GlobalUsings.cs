// Shared messaging contracts (NotificationType, NotificationDeliveryMessage) live in the
// Taskpilot.Contracts assembly so the notification service can share them. Exposing them
// globally keeps every existing reference working without touching each file's usings.
global using Taskpilot.Contracts;
// External notification senders + their options moved to Taskpilot.Integrations; exposing
// them globally keeps the ~21 existing references working without touching each file.
global using Taskpilot.Integrations;
