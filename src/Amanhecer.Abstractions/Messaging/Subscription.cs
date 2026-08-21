using System;

namespace Amanhecer.Abstractions.Messaging;

public abstract class Subscription: ISubscription
{
    public string Name { get; } = Uuid.NewGuid().ToString();
    
    public required string ToRoutingKey { get; set; }
    public string DefaultSpecVersion { get; set; } = "1.0";
    public Uri DefaultSource { get; set; } = new Uri("amanhecer", UriKind.RelativeOrAbsolute);
    public string DefaultType { get; set; } = "default";

    public Type MessageMapperType { get; }
    public ISubscriptionProvisoner? Provisioner { get; set; }
}