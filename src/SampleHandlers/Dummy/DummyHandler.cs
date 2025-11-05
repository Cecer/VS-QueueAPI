using Vintagestory.Server;

namespace QueueAPI.SampleHandlers.Dummy;

/// <summary>
/// A simple <see cref="AbstractHandler{TClient,TQueue}"/> implementation that simply kicks all joining players immediately.
/// </summary>
public class DummyHandler(ServerMain server) : IQueueAPIEventHandler
{
    public int WorldPopulation => 0;
    public int WorldTotalCapacity => 0;

    public IJoinQueue Queue { get; } = new DummyQueue(server);

    public void OnClientConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements)
    {
        server.DisconnectPlayer(client, null, "Dummy handler");
    }

    public void OnClientAccepted(ConnectedClient client)
    {
        throw new System.NotImplementedException();
    }

    public void OnClientDisconnect(int clientId)
    {
        throw new System.NotImplementedException();
    }

    public void OnAttached(IQueueAPIEventHandler? previousHandler)
    {
        throw new System.NotImplementedException();
    }

    public void OnDetached(IQueueAPIEventHandler? newHandler)
    {
        throw new System.NotImplementedException();
    }
}