using System;

namespace Amanhecer.Abstractions.Messaging;

public interface ISubscription
{
    string ToRoutingKey { get; }
    string DefaultSpecVersion { get; }
    Uri DefaultSource { get; }
    string DefaultType { get; }


    string Name { get; }
    Type MessageMapperType { get; }
    ISubscriptionProvisoner? Provisioner { get; }
}