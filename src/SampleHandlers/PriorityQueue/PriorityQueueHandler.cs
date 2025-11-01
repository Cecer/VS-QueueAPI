using QueueAPI.Handlers;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI.SampleHandlers.PriorityQueue;

public class PriorityQueueHandler(ServerMain server) : AbstractHandler<QueuedClient, PriorityQueueJoinQueue>(server)
{
    public PriorityQueueHandler(ICoreServerAPI api) : this(api.GetInternalServer()) { }

    public override PriorityQueueJoinQueue Queue { get; } = new PriorityQueueJoinQueue(server);

    protected override QueuedClient CreateQueuedClient(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements) => new QueuedClient(client, clientIdentPacket, entitlements);
}