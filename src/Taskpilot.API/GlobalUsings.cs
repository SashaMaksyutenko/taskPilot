// Shared messaging contracts (NotificationType, NotificationDeliveryMessage) live in the
// Taskpilot.Contracts assembly so the notification service can share them. Exposing them
// globally keeps every existing reference working without touching each file's usings.
global using Taskpilot.Contracts;
