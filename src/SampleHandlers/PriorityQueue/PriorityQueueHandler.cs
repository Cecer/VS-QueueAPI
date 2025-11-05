using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI.SampleHandlers.PriorityQueue;

public class PriorityQueueHandler(ServerMain server) : IQueueAPIEventHandler
{
    public int WorldPopulation { get; }
    public int WorldTotalCapacity { get; }
    public IJoinQueue Queue { get; }

    public void OnClientConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements)
    {
        throw new System.NotImplementedException();
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