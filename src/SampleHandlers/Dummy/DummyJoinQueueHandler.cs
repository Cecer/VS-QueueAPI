using QueueAPI.Handlers;
using Vintagestory.Server;

namespace QueueAPI.SampleHandlers.Dummy;

/// <summary>
/// A simply <see cref="IJoinQueueHandler"/> implementation that simply kicks all joining players immediately.
/// </summary>
public class DummyJoinQueueHandler : IJoinQueueHandler
{
    public int JoinedPlayerCount => 0;
    
    public int QueueSize => 0;
    
    public int GetClientQueueIndex(int clientId) => -1;
    
    public QueuedClient? GetClientAtQueueIndex(int index) => null;

    public QueuedClient? GetNextQueuedClient(bool remove) => null;

    public IJoinQueueHandler.IPlayerConnectResult OnPlayerConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements)
    {
        return new IJoinQueueHandler.IPlayerConnectResult.Disconnect("Kicked by the DummyJoinQueueHandler.");
    }

    public void OnPlayerDisconnect(ConnectedClient disconnectingClient) { }

    public void SendPositionUpdate() { }

    public void Reset() { }
}