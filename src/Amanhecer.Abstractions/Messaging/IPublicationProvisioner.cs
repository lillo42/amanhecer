using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

public interface IPublicationProvisioner
{
    Task ExecuteAsync(IGateway gateway, IPublication publication);
}