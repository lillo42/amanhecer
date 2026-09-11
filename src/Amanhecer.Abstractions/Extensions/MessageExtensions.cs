using System.Diagnostics;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Abstractions.Extensions;

/// <summary>
/// Extension methods to propagate tracing context from an <see cref="Activity"/> to a
/// <see cref="Message"/>.
/// </summary>
public static class MessageExtensions
{
    /// <summary>
    /// Copies the tracing context of the activity (trace state, trace parent and baggage) onto
    /// the message, without overwriting values already set on the message. Does nothing when
    /// the activity is <see langword="null"/>.
    /// </summary>
    /// <param name="message">The message to enrich.</param>
    /// <param name="activity">The activity whose tracing context is copied onto the message, if any.</param>
    public static void Enrich(this Message message, Activity? activity)
    {
        if (activity == null)
        {
            return;
        }

        if (message.TraceState == null && !string.IsNullOrEmpty(activity.TraceStateString))
        {
            message.TraceState = TraceState.FromString(activity.TraceStateString!);
        }

        if (string.IsNullOrEmpty(message.TraceParent))
        {
            message.TraceParent = activity.Id;
        }

        if (message.Baggage == null)
        {
            // Activity.Baggage enumerates the whole activity chain and can yield the same key
            // twice; the most local (first enumerated) value wins, as in the merge below.
            var baggage = new Baggage();
            foreach (var item in activity.Baggage)
            {
                if (!baggage.ContainsKey(item.Key))
                {
                    baggage.Add(item.Key, item.Value);
                }
            }

            message.Baggage = baggage;
        }
        else
        {
            foreach (var item in activity.Baggage)
            {
                if (!message.Baggage.ContainsKey(item.Key))
                {
                    message.Baggage.Add(item.Key, item.Value);
                }
            }
        }
    }
}