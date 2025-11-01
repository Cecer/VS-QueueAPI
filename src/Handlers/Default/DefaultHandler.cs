using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI.Handlers.Default;

public class DefaultHandler(ServerMain server) : AbstractHandler<QueuedClient, DefaultJoinQueue>(server)
{
    public DefaultHandler(ICoreServerAPI api) : this(api.GetInternalServer()) { }

    /// <inheritdoc/>
    public override DefaultJoinQueue Queue { get; } = new DefaultJoinQueue(server);

    /// <inheritdoc/>
    protected override QueuedClient CreateQueuedClient(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements) => new QueuedClient(client, clientIdentPacket, entitlements);
}