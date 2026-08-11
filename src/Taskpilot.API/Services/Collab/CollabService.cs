using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Resolves a document id to the underlying entity to authorize a co-editor, and stores the
/// opaque Yjs snapshot. Two surfaces are collaborative today: a task's description
/// (<c>"task:{guid}"</c>) and a project's whiteboard (<c>"board:{guid}"</c>).
/// </summary>
public class CollabService : ICollabService
{
    private const string TaskPrefix = "task:";
    private const string BoardPrefix = "board:";

    private readonly TaskpilotDbContext _context;

    public CollabService(TaskpilotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<bool> CanAccessAsync(string docId, Guid userId)
    {
        // A collaborator must be able to persist the result, so require the same write
        // permission the REST save enforces.
        if (TryParseId(docId, TaskPrefix, out var taskId))
            return ProjectAccess.CanModifyTaskAsync(_context, taskId, userId); // owner or the assigned Editor

        // A project whiteboard: any owner or Editor member of the project may edit it.
        if (TryParseId(docId, BoardPrefix, out var projectId))
            return ProjectAccess.CanWriteAsync(_context, projectId, userId);

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

    /// <summary>Extracts the guid from a <c>"{prefix}{guid}"</c> document id.</summary>
    private static bool TryParseId(string docId, string prefix, out Guid id)
    {
        id = default;
        return docId is not null
            && docId.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParse(docId.AsSpan(prefix.Length), out id);
    }
}
