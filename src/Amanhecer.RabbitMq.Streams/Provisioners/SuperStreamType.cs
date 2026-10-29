namespace Amanhecer.RabbitMq.Streams.Provisioners;

public abstract record SuperStreamType;

public record PartitionSuperStream(int Partition) : SuperStreamType
{
    public static PartitionSuperStream Instance { get; } = new(1);
}


public record BindingSuperStream(string[] BindingKey) : SuperStreamType;