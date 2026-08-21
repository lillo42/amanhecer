using System;
using System.Collections.Generic;
using System.Net.Mime;

namespace Amanhecer.Abstractions.Messaging;

public abstract class Publication : IPublication
{
    public string Name { get; set; } = Uuid.NewGuid().ToString();

    public Dictionary<string, object> AdditionalCloudEvents { get; set; } = [];
    public ContentType DefaultContentType { get; set; } = new("text/plain");
    public Uri? DefaultDataSchema { get; set; }
    public Dictionary<string, object> DefaultHeaders { get; set; } = [];
    public required string RoutingKey { get; set; }
    public string? DefaultReplyTo { get; set; }
    public string? DefaultSubject { get; set; }
    public Uri DefaultSource { get; set; } = new("amanhecer", UriKind.RelativeOrAbsolute);
    public string DefaultSpecVersion { get; set; } = "1.0";
    public string? DefaultType { get; set; }


    public Type MessageMapperType { get; set; }
    public IPublicationProvisioner? Provisioner { get; set; }
}