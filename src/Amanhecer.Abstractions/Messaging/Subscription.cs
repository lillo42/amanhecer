using System;

namespace Amanhecer.Abstractions.Messaging;

public abstract class Subscription: ISubscription
{
    public required string RoutingKey { get; set; }
    public string DefaultSpecVersion { get; set; } = "1.0";
    public Uri DefaultSource { get; set; } = new Uri("amanhecer", UriKind.RelativeOrAbsolute);
    public string DefaultType { get; set; } = "default";
    public ISubscriptionProvisoner? Provisioner { get; set; }
}