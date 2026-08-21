using System.Diagnostics;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Abstractions.Extensions;

public static class MessageExtensions
{
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

        message.Baggage ??= new Baggage(activity.Baggage);
    }
}