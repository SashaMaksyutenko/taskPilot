using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class CsvImportService : ICsvImportService
{
    // Bound a single import so a huge paste can't create thousands of tasks / errors.
    private const int MaxRows = 500;
    private const int MaxErrors = 20;

    private readonly TaskpilotDbContext _context;
    private readonly ILogger<CsvImportService> _logger;

    public CsvImportService(TaskpilotDbContext context, ILogger<CsvImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ImportResultDto>> ImportTasksAsync(Guid userId, Guid projectId, string csv)
    {
        if (!await ProjectAccess.CanAccessAsync(_context, projectId, userId))
            return Result<ImportResultDto>.Fail("Project not found.");
        if (!await ProjectAccess.CanWriteAsync(_context, projectId, userId))
            return Result<ImportResultDto>.Fail("You have read-only access to this project.");

        var rows = ParseCsv(csv ?? string.Empty);
        if (rows.Count < 2)
            return Result<ImportResultDto>.Fail("The CSV has no data rows.");

        // Map recognized columns by header name (case-insensitive, any order).
        var header = rows[0];
        int Col(string name) => Array.FindIndex(header, h => h.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));
        var iTitle = Col("Title");
        var iStatus = Col("Status");
        var iPriority = Col("Priority");
        var iDeadline = Col("Deadline");
        var iAssignee = Col("Assignee");
        var iDescription = Col("Description");
        if (iTitle < 0)
            return Result<ImportResultDto>.Fail("The CSV must have a \"Title\" column.");

        // Resolve assignee names to ids among the project's people (owner + members).
        var nameToId = await ProjectPeopleAsync(projectId);

        var result = new ImportResultDto();
        var toAdd = new List<ProjectTask>();

        for (var r = 1; r < rows.Count && toAdd.Count < MaxRows; r++)
        {
            var row = rows[r];
            string Get(int i) => i >= 0 && i < row.Length ? row[i].Trim() : string.Empty;

            var title = Get(iTitle);
            if (title.Length < 2)
            {
                result.Skipped++;
                if (result.Errors.Count < MaxErrors) result.Errors.Add($"Row {r + 1}: title is required (min 2 characters).");
                continue;
            }

            var status = Enum.TryParse<ProjectTaskStatus>(Get(iStatus), ignoreCase: true, out var st) ? st : ProjectTaskStatus.Backlog;
            var priority = Enum.TryParse<TaskPriority>(Get(iPriority), ignoreCase: true, out var pr) ? pr : TaskPriority.Medium;

            DateTime? deadline = null;
            var deadlineRaw = Get(iDeadline);
            if (deadlineRaw.Length > 0 &&
                DateTime.TryParse(deadlineRaw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dl))
                deadline = dl;

            Guid? assigneeId = null;
            var assigneeName = Get(iAssignee);
            if (assigneeName.Length > 0 && nameToId.TryGetValue(assigneeName, out var aid))
                assigneeId = aid;

            toAdd.Add(new ProjectTask
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = title,
                Description = Get(iDescription) is { Length: > 0 } d ? d : null,
                Status = status,
                Priority = priority,
                AssigneeId = assigneeId,
                CreatorId = userId,
                Deadline = deadline,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = status == ProjectTaskStatus.Done ? DateTime.UtcNow : null,
            });
        }

        if (toAdd.Count > 0)
        {
            _context.ProjectTasks.AddRange(toAdd);
            await _context.SaveChangesAsync();
        }
        result.Created = toAdd.Count;

        _logger.LogInformation("CSV import into {Project}: {Created} created, {Skipped} skipped.", projectId, result.Created, result.Skipped);
        return Result<ImportResultDto>.Ok(result);
    }

    /// <summary>Case-insensitive map of a project's people (owner + members) name → id.</summary>
    private async Task<Dictionary<string, Guid>> ProjectPeopleAsync(Guid projectId)
    {
        var members = await _context.ProjectMembers
            .Where(m => m.ProjectId == projectId)
            .Select(m => new { m.UserId, m.User.Name })
            .ToListAsync();
        var owner = await _context.Projects
            .Where(p => p.Id == projectId)
            .Select(p => new { p.OwnerId, Name = p.Owner.Name })
            .FirstAsync();

        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in members) map[m.Name] = m.UserId;
        map[owner.Name] = owner.OwnerId; // owner wins on a name clash
        return map;
    }

    /// <summary>Minimal RFC-4180 CSV parser: handles quoted fields, "" escapes and CRLF.</summary>
    private static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\n')
            {
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row.ToArray());
                row = new List<string>();
            }
            else if (c != '\r') field.Append(c);
        }

        // Flush a final field/row that isn't newline-terminated.
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }
        return rows;
    }
}
