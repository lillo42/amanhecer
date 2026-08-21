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
    /// Copies the tracing context of the message (baggage, trace parent and trace state)
    /// onto the activity.
    /// </summary>
    /// <param name="activity">The activity to enrich.</param>
    /// <param name="message">The message whose tracing context is copied onto the activity.</param>
    public static void Enrich(this Activity activity, Message message)
    {
        if (message.Baggage != null)
        {
            foreach (var keyPairValue in message.Baggage)
            {
                activity.AddBaggage(keyPairValue.Key, keyPairValue.Value);
            }
        }

        if (!string.IsNullOrEmpty(message.TraceParent))
        {
            activity.SetParentId(message.TraceParent!);
        }

        if (message.TraceState != null)
        {
            activity.TraceStateString = message.TraceState.ToString();
        }
    }
}