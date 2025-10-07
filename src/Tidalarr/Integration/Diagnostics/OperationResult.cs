using System.Collections.Generic;

namespace Tidalarr.Integration.Diagnostics;

internal sealed class OperationResult
{
    public bool Success { get; init; }
    public string Code { get; init; } = string.Empty;
    public string? Message { get; init; }
    public Dictionary<string, object?> Metadata { get; init; } = new();

    public static OperationResult Ok(string code, string? message = null, Dictionary<string, object?>? metadata = null)
        => new OperationResult { Success = true, Code = code, Message = message, Metadata = metadata ?? new() };

    public static OperationResult Fail(string code, string? message = null, Dictionary<string, object?>? metadata = null)
        => new OperationResult { Success = false, Code = code, Message = message, Metadata = metadata ?? new() };
}

