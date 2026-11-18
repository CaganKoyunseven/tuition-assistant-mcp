namespace TuitionAssistant.Api.Domain;

/// <summary>Payment status of a tuition record, derived from the assessed and paid amounts.</summary>
public static class TuitionStatuses
{
    public const string Unpaid = "UNPAID";
    public const string PartiallyPaid = "PARTIALLY_PAID";
    public const string Paid = "PAID";

    /// <summary>UNPAID when nothing is paid, PAID when the total is covered, otherwise PARTIALLY_PAID.</summary>
    public static string From(long amountCents, long paidCents) =>
        paidCents >= amountCents ? Paid
        : paidCents == 0 ? Unpaid
        : PartiallyPaid;
}
