using System.Collections.Generic;
using System.Linq;
using QueueAPI.Harmony.Accessors;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI.Handlers;

/// <summary>
/// While rewritten for clarity, this handler implementation is functionality equivalent to the vanilla implementation in 1.21.5.
/// </summary>
public class SimpleJoinQueueHandler : IJoinQueueHandler
{
    private readonly ServerMain _server;

    /// <summary>
    /// While rewritten for clarity, this handler implementation is functionality equivalent to the vanilla implementation in 1.21.5.
    /// </summary>
    public SimpleJoinQueueHandler(ServerMain server)
    {
        _server = server;
    }

    public SimpleJoinQueueHandler(ICoreServerAPI api) : this(api.GetInternalServer()) { }

    public virtual int JoinedPlayerCount => _server.Clients.Count - _server.ConnectionQueue.Count;

    public virtual int QueueSize => _server.ConnectionQueue.Count;

    public virtual int GetClientQueueIndex(int clientId) => _server.ConnectionQueue.FindIndex(c => c.Client.Id == clientId);

    public virtual QueuedClient? GetClientAtQueueIndex(int index)
    {
        lock (_server.ConnectionQueue)
        {
            if (_server.ConnectionQueue.Count < index)
            {
                return _server.ConnectionQueue[index];
            }

            return null;
        }
    }

    public virtual IJoinQueueHandler.IPlayerConnectResult OnPlayerConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements)
    {
        if (JoinedPlayerCount < _server.Config.MaxClients)
        {
            // Server has capacity
            return new IJoinQueueHandler.IPlayerConnectResult.Join();
        }

        var playerData = _server.PlayerDataManager.GetOrCreateServerPlayerData(client.SentPlayerUid);
        if (playerData.HasPrivilege(Privilege.controlserver, _server.Config.RolesByCode) && playerData.HasPrivilege("ignoremaxclients", _server.Config.RolesByCode))
        {
            // Privilege based bypassing
            return new IJoinQueueHandler.IPlayerConnectResult.Join();
        }

        if (_server.Config.MaxClientsInQueue <= 0)
        {
            // Queue is disabled
            return new IJoinQueueHandler.IPlayerConnectResult.Disconnect(Lang.Get("Server is full ({0} max clients)", _server.Config.MaxClients));
        }

        if (QueueSize >= _server.Config.MaxClientsInQueue)
        {
            // Queue is full
            return new IJoinQueueHandler.IPlayerConnectResult.Disconnect(Lang.Get("Server is full ({0} max clients)", _server.Config.MaxClients));
        }

        int position;
        lock (_server.ConnectionQueue)
        {
            // Add the player to the queue
            _server.ConnectionQueue.Add(new QueuedClient(client, clientIdentPacket, entitlements));
            position = QueueSize;
        }

        ServerMain.Logger.Notification($"Player {clientIdentPacket.Playername} was put into the connection queue at position {position}");
        
        // Display the queue screen to the player
        return new IJoinQueueHandler.IPlayerConnectResult.Queue(position);
    }

    public virtual QueuedClient? GetNextQueuedClient(bool remove)
    {
        lock (_server.ConnectionQueue)
        {
            var queuedClient = _server.ConnectionQueue.FirstOrDefault(null as QueuedClient);
            if (queuedClient != null && remove)
            {
                // Remove the next client from the queue.
                _server.ConnectionQueue.RemoveAll(e => e.Client.Id == queuedClient.Client.Id);
            }
            return queuedClient;
        }
    }
    
    public virtual void OnPlayerDisconnect(ConnectedClient disconnectingClient)
    {
        if (_server.Config.MaxClientsInQueue <= 0 || _server.stopped)
        {
            // Queue is disabled or server has been stopped
            return;
        }

        List<QueuedClient> joiningClients;
        lock (_server.ConnectionQueue)
        {
            if (disconnectingClient.State == EnumClientState.Queued)
            {
                // Client was in the queue. Remove them from it.
                _server.ConnectionQueue.RemoveAll(e => e.Client.Id == disconnectingClient.Id);
            }

            var queueSize = QueueSize;
            if (queueSize == 0)
            {
                // Nobody is in the queue. Nothing to do.
                return;
            }

            var availableSlots = _server.Config.MaxClients - JoinedPlayerCount;
            if (availableSlots <= 0)
            {
                // Still no room for anybody to join. Update their position at least.
                SendPositionUpdate();
                return;
            }

            // Find some queued clients to join.
            joiningClients = [];
            
            
            for (var i = 0; i < availableSlots; i++)
            {
                var nextInQueue = GetNextQueuedClient(true);
                if (nextInQueue == null)
                {
                    // No more players in the queue.
                    break;
                }
                joiningClients.Add(nextInQueue);
            }
        }

        // Actually join the clients that we now have capacity for.
        foreach (var joiningClient in joiningClients)
        {
            _server.FinalizePlayerIdentification(joiningClient.Identification, joiningClient.Client, joiningClient.Entitlements);
        }

        SendPositionUpdate();
    }

    public virtual void SendPositionUpdate()
    {
        // We clone the collection to an array so that we do not need to worry about locking while sending queue position updates to clients in the queue.
        var queuedClients = _server.ConnectionQueue.ToArray();
            
        for (var index = 0; index < queuedClients.Length; index++)
        {
            ((ICoreServerAPI) _server.Api).SendQueuePositionUpdate(queuedClients[index].Client.Id, index);
        }
    }
    
    public virtual void Reset()
    {
        lock (_server.ConnectionQueue)
        {
            foreach (var queuedClient in _server.ConnectionQueue.ToArray())
            {
                _server.DisconnectPlayer(queuedClient.Client, null, "Join queue reset");
            }
            _server.ConnectionQueue.Clear();
        }
    }
}