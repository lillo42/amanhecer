using System.Diagnostics;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Abstractions.Extensions;

/// <summary>
/// Extension methods to propagate tracing context from a <see cref="Message"/> to an
/// <see cref="Activity"/>.
/// </summary>
public static class ActivityExtensions
{
    /// <summary>
    /// Copies the baggage carried by the message onto the activity.
    /// </summary>
    /// <remarks>
    /// The trace parent is deliberately not set here: parenting only takes effect when passed to
    /// <c>ActivitySource.StartActivity</c> at start time, so setting it afterwards would be a
    /// silent no-op.
    /// </remarks>
    /// <param name="activity">The activity to enrich.</param>
    /// <param name="message">The message whose baggage is copied onto the activity.</param>
    public static void Enrich(this Activity activity, Message message)
    {
        if (message.Baggage != null)
        {
            foreach (var keyPairValue in message.Baggage)
            {
                activity.AddBaggage(keyPairValue.Key, keyPairValue.Value);
            }
        }
    }
}