using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Resolves a document id to the underlying entity to authorize a co-editor, and stores the
/// opaque Yjs snapshot. Only task descriptions are collaborative today; the id scheme
/// (<c>"task:{guid}"</c>) leaves room for notes and other surfaces later.
/// </summary>
public class CollabService : ICollabService
{
    private const string TaskPrefix = "task:";

    private readonly TaskpilotDbContext _context;

    public CollabService(TaskpilotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<bool> CanAccessAsync(string docId, Guid userId)
    {
        // A collaborator must be able to persist the result, so require the same write
        // permission the REST description save enforces (owner, or the assigned Editor).
        if (TryParseTaskId(docId, out var taskId))
            return ProjectAccess.CanModifyTaskAsync(_context, taskId, userId);

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetStateAsync(string docId)
    {
        var doc = await _context.CollabDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == docId);
        return doc?.State;
    }

    /// <inheritdoc />
    public async Task SaveStateAsync(string docId, byte[] state)
    {
        var doc = await _context.CollabDocuments.FirstOrDefaultAsync(d => d.Id == docId);
        if (doc is null)
        {
            doc = new CollabDocument { Id = docId };
            _context.CollabDocuments.Add(doc);
        }
        doc.State = state;
        doc.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    /// <summary>Extracts the task id from a <c>"task:{guid}"</c> document id.</summary>
    private static bool TryParseTaskId(string docId, out Guid taskId)
    {
        taskId = default;
        return docId is not null
            && docId.StartsWith(TaskPrefix, StringComparison.Ordinal)
            && Guid.TryParse(docId.AsSpan(TaskPrefix.Length), out taskId);
    }
}
