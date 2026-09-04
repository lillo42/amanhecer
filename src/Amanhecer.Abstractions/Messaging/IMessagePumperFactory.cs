namespace Amanhecer.Abstractions.Messaging;

public interface IMessagePumperFactory
{
    IMessagePumper Create();
}