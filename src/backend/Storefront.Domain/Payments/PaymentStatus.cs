namespace Storefront.Domain.Payments;

public enum PaymentStatus
{
    /// <summary>The session is open and the provider has not answered yet.</summary>
    Pending,

    /// <summary>The provider confirmed the money and the order is paid.</summary>
    Succeeded,

    /// <summary>The provider reported that the payment did not go through. The buyer may try again until the deadline.</summary>
    Failed,

    /// <summary>The money came after the order was cancelled and the stock could not be taken again. It is returned by hand (decision 0035).</summary>
    PaidWithoutOrder,
}
