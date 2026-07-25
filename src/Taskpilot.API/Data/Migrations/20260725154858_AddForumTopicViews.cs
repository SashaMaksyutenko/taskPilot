using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskpilot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddForumTopicViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent on purpose. An earlier (renamed) form of this migration already
            // created "ForumTopicViews" on some databases with the old (TopicId, UserId)
            // unique index. Reconcile in place so this applies cleanly whether the table is
            // absent (fresh DB) or present with the old schema (already-deployed DB).
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ForumTopicViews"" (
                    ""Id"" uuid NOT NULL,
                    ""TopicId"" uuid NOT NULL,
                    ""UserId"" uuid NOT NULL,
                    ""TimeBucket"" bigint NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_ForumTopicViews"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_ForumTopicViews_ForumTopics_TopicId"" FOREIGN KEY (""TopicId"") REFERENCES ""ForumTopics"" (""Id"") ON DELETE CASCADE
                );");
            // Add the new column for tables that predate it (backfill existing rows with 0,
            // then drop the default so the column matches the model snapshot).
            migrationBuilder.Sql(@"ALTER TABLE ""ForumTopicViews"" ADD COLUMN IF NOT EXISTS ""TimeBucket"" bigint NOT NULL DEFAULT 0;");
            migrationBuilder.Sql(@"ALTER TABLE ""ForumTopicViews"" ALTER COLUMN ""TimeBucket"" DROP DEFAULT;");
            // Swap the old unique index for the bucketed one.
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ForumTopicViews_TopicId_UserId"";");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ForumTopicViews_TopicId_UserId_TimeBucket"" ON ""ForumTopicViews"" (""TopicId"", ""UserId"", ""TimeBucket"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForumTopicViews");
        }
    }
}
