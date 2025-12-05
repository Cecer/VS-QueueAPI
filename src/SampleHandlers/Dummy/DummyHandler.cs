using Vintagestory.Server;

namespace QueueAPI.SampleHandlers.Dummy;

/// <summary>
/// A simple <see cref="IQueueAPIHandler"/> implementation that simply kicks all joining players immediately.
/// </summary>
public class DummyHandler(ServerMain server) : IQueueAPIHandler
{
    public int WorldPopulation => 0;
    public int WorldTotalCapacity => 0;

    public IJoinQueue Queue { get; } = new DummyQueue();

    public void OnClientConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements)
    {
        server.DisconnectPlayer(client, null, "Dummy handler");
    }

    public void OnClientAccepted(ConnectedClient client)
    {

    }

    public void OnClientDisconnect(ConnectedClient client, string? othersReason, string? theirReason)
    {

    }

    public void OnAttached(IQueueAPIHandler? previousHandler)
    {
        // Kick all existing players when attached
        foreach (var client in server.Clients.Values)
        {
            server.DisconnectPlayer(client, null, "Dummy handler");
        }
    }

    public void OnDetached(IQueueAPIHandler? newHandler)
    {

    }
}