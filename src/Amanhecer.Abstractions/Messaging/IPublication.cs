using System;
using System.Collections.Generic;
using System.Net.Mime;

namespace Amanhecer.Abstractions.Messaging;

public interface IPublication
{
    Dictionary<string, object> AdditionalCloudEvents { get; set; }
    ContentType DefaultContentType { get; set; }
    Uri? DefaultDataSchema { get; set; }
    Dictionary<string, object> DefaultHeaders { get; set; }
    string? DefaultReplyTo { get; set; }
    string? DefaultSubject { get; set; }
    Uri DefaultSource { get; set; }
    string DefaultSpecVersion { get; set; }
    string? DefaultType { get; set; }
    Type MessageMapperType { get; set; }
    
    
    string Name { get; set; }
    string RoutingKey { get; set; }
    IPublicationProvisioner? Provisioner { get; set; }
}