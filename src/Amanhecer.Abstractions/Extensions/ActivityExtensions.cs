using System.Diagnostics;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Abstractions.Extensions;

public static class ActivityExtensions
{
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