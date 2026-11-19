using Microsoft.Extensions.Options;
using TuitionAssistant.Agent.Configuration;
using TuitionAssistant.Agent.Orchestration;
using TuitionAssistant.Agent.Payments;
using TuitionAssistant.Agent.Security;

namespace TuitionAssistant.Agent.Endpoints;

/// <summary>Body of <c>POST /api/v1/agent/chat</c>.</summary>
public sealed record ChatRequest(string? SessionId, string? Message, string? Model);

/// <summary>Body of the confirm and cancel endpoints.</summary>
public sealed record PaymentActionRequest(string? SessionId);

public static class AgentEndpoints
{
    private const int MaxMessageLength = 2000;

    public static void MapAgentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/agent").RequireAuthorization(AgentAuthentication.StudentPolicy);

        group.MapGet("/models", (IOptions<LlmOptions> llm) =>
            Results.Ok(new { defaultModel = llm.Value.Model, models = llm.Value.AvailableModels(), provider = llm.Value.Provider }))
            .WithSummary("Lists the models a chat request may choose from.");

        group.MapPost("/chat", async (ChatRequest request, HttpContext http, AgentOrchestrator orchestrator, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > MaxMessageLength)
            {
                throw new AgentException(StatusCodes.Status400BadRequest, AgentErrorCodes.Validation, $"message is required and must be at most {MaxMessageLength} characters.");
            }

            var sessionId = ParseSession(request.SessionId);
            var result = await orchestrator.ChatAsync(sessionId, request.Message.Trim(), request.Model, http.ToToolContext(sessionId), ct);
            return Results.Ok(new { result.SessionId, result.Answer, result.Model, result.PendingPayment, result.Trace });
        }).WithSummary("Sends a message to the tuition assistant.");

        group.MapPost("/payments/{pendingId:guid}/confirm", async (Guid pendingId, PaymentActionRequest request, HttpContext http, PaymentConfirmationService payments, CancellationToken ct) =>
        {
            var sessionId = ParseSession(request.SessionId);
            var outcome = await payments.ConfirmAsync(pendingId, sessionId, http.ToToolContext(sessionId), ct);
            return Results.Ok(new { outcome.Status, outcome.Answer, outcome.Payment, outcome.Trace });
        }).WithSummary("Executes a prepared payment after the student pressed Confirm.");

        group.MapPost("/payments/{pendingId:guid}/cancel", (Guid pendingId, PaymentActionRequest request, HttpContext http, PaymentConfirmationService payments) =>
        {
            var sessionId = ParseSession(request.SessionId);
            payments.Cancel(pendingId, sessionId, http.ToToolContext(sessionId));
            return Results.Ok(new { status = "CANCELLED" });
        }).WithSummary("Cancels a prepared payment.");
    }

    private static string ParseSession(string? sessionId) =>
        Guid.TryParse(sessionId, out var id)
            ? id.ToString()
            : throw new AgentException(StatusCodes.Status400BadRequest, AgentErrorCodes.Validation, "sessionId must be a GUID.");
}
