namespace Amanhecer.RabbitMq.Streams.Provisioners;

/// <summary>Base record for the super stream shapes a <see cref="CreateSuperStream"/> provisioner can create.</summary>
public abstract record SuperStreamType;

/// <summary>
/// Creates a super stream with a fixed number of partitions, named by appending the partition
/// number to the super stream name; messages are routed to a partition by hashing the routing key.
/// </summary>
/// <param name="Partition">The number of partitions to create; must be at least <c>1</c>.</param>
public record PartitionSuperStream(int Partition) : SuperStreamType
{
    /// <summary>Gets a <see cref="PartitionSuperStream"/> with a single partition.</summary>
    public static PartitionSuperStream Instance { get; } = new(1);
}

/// <summary>
/// Creates a super stream with one partition per binding key, named by appending the binding
/// key to the super stream name; messages are routed to a partition by matching the routing
/// key against the binding keys.
/// </summary>
/// <param name="BindingKey">The binding keys, which must be unique.</param>
public record BindingSuperStream(string[] BindingKey) : SuperStreamType;
