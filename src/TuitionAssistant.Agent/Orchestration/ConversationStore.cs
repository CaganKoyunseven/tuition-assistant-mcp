using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TuitionAssistant.Agent.Configuration;
using TuitionAssistant.Agent.Llm;

namespace TuitionAssistant.Agent.Orchestration;

/// <summary>Short chat history of one session, bound to the student who started it.</summary>
public sealed class Conversation(string studentNo)
{
    private readonly List<LlmMessage> _messages = [];

    public string StudentNo { get; } = studentNo;

    public object SyncRoot { get; } = new();

    public IReadOnlyList<LlmMessage> Snapshot()
    {
        lock (SyncRoot)
        {
            return [.. _messages];
        }
    }

    public void Append(int maxMessages, params LlmMessage[] messages)
    {
        lock (SyncRoot)
        {
            _messages.AddRange(messages);
            if (_messages.Count > maxMessages)
            {
                _messages.RemoveRange(0, _messages.Count - maxMessages);
            }
        }
    }
}

/// <summary>In-memory sessions with sliding expiration. A session belongs to exactly one student.</summary>
public sealed class ConversationStore(IMemoryCache cache, IOptions<AgentOptions> options)
{
    public Conversation GetOrCreate(string sessionId, string studentNo)
    {
        var conversation = cache.GetOrCreate(Key(sessionId), entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(options.Value.SessionIdleMinutes);
            return new Conversation(studentNo);
        })!;

        if (conversation.StudentNo != studentNo)
        {
            throw new AgentException(StatusCodes.Status403Forbidden, AgentErrorCodes.SessionForbidden, "This session belongs to another student.");
        }

        return conversation;
    }

    private static string Key(string sessionId) => "session:" + sessionId;
}
