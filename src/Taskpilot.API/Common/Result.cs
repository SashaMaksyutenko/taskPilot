namespace Taskpilot.API.Common;

/// <summary>
/// Simple operation result used by services to report success or failure
/// without throwing exceptions for expected (business) errors.
/// </summary>
public class Result
{
    /// <summary>True when the operation succeeded.</summary>
    public bool Succeeded { get; }

    /// <summary>Human-readable error message when <see cref="Succeeded"/> is false; otherwise null.</summary>
    public string? Error { get; }

    /// <summary>Base constructor. Use the static factory methods instead.</summary>
    protected Result(bool succeeded, string? error)
    {
        Succeeded = succeeded;
        Error = error;
    }

    /// <summary>Creates a successful result.</summary>
    public static Result Ok() => new(true, null);

    /// <summary>Creates a failed result with an error message.</summary>
    public static Result Fail(string error) => new(false, error);
}

/// <summary>
/// Operation result that also carries a value on success.
/// </summary>
/// <typeparam name="T">Type of the value returned on success.</typeparam>
public class Result<T> : Result
{
    /// <summary>The value produced on success; default when the operation failed.</summary>
    public T? Value { get; }

    private Result(bool succeeded, T? value, string? error)
        : base(succeeded, error)
    {
        Value = value;
    }

    /// <summary>Creates a successful result holding <paramref name="value"/>.</summary>
    public static Result<T> Ok(T value) => new(true, value, null);

    /// <summary>Creates a failed result with an error message and no value.</summary>
    public static new Result<T> Fail(string error) => new(false, default, error);
}
