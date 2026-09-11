using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Provisions the transport resources (exchanges, bindings, ...) a publication needs
/// before messages can be published through it.
/// </summary>
public interface IPublicationProvisioner
{
    /// <summary>
    /// Provisions the transport resources required by the publication.
    /// </summary>
    /// <param name="gateway">The gateway the publication belongs to.</param>
    /// <param name="publication">The publication to provision.</param>
    /// <returns>A <see cref="Task"/> that completes when provisioning has finished.</returns>
    Task ExecuteAsync(IGateway gateway, IPublication publication);
}