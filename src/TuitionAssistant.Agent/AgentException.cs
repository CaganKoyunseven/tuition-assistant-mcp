namespace TuitionAssistant.Agent;

/// <summary>An expected failure that maps to an HTTP status and a stable error code.</summary>
public sealed class AgentException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;

    public string Code { get; } = code;
}

public static class AgentErrorCodes
{
    public const string Validation = "VALIDATION_ERROR";
    public const string ModelNotAllowed = "MODEL_NOT_ALLOWED";
    public const string SessionForbidden = "SESSION_FORBIDDEN";
    public const string PendingNotFound = "PENDING_PAYMENT_NOT_FOUND";
    public const string PendingExpired = "PENDING_PAYMENT_EXPIRED";
    public const string PendingAlreadyUsed = "PENDING_PAYMENT_ALREADY_USED";
    public const string Upstream = "UPSTREAM_ERROR";
}
