using QueueAPI.Handlers;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI.SampleHandlers.Dummy;

/// <summary>
/// A simple <see cref="AbstractHandler{TClient,TQueue}"/> implementation that simply kicks all joining players immediately.
/// </summary>
public class DummyHandler(ServerMain server) : AbstractHandler<QueuedClient, DummyQueue>(server)
{
    public DummyHandler(ICoreServerAPI api) : this(api.GetInternalServer()) { }

    public override DummyQueue Queue { get; } = new DummyQueue(server);
    protected override QueuedClient CreateQueuedClient(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements) => new QueuedClient(client, clientIdentPacket, entitlements);
}