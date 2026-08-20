namespace Amanhecer.Abstractions.Messaging;

public interface IPublicationFinder
{
    IPublication Find(string routingKey);
}