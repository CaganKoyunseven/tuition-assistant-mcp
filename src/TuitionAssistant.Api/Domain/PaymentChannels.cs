namespace TuitionAssistant.Api.Domain;

/// <summary>Where a payment came from, stored in <c>payments.channel</c>.</summary>
public static class PaymentChannels
{
    public const string BankApp = "BANK_APP";
    public const string AiAgent = "AI_AGENT";
    public const string Student = "STUDENT";
}
