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
/// <param name="server"></param>
public class SimpleJoinQueueHandler(ServerMain server) : IJoinQueueHandler
{
    public virtual int JoinedPlayerCount => server.Clients.Count - server.ConnectionQueue.Count;
    public virtual int QueueSize => server.ConnectionQueue.Count;

    public virtual int GetClientQueueIndex(int clientId) => server.ConnectionQueue.FindIndex(c => c.Client.Id == clientId);

    public virtual QueuedClient? GetClientAtQueueIndex(int index)
    {
        lock (server.ConnectionQueue)
        {
            if (server.ConnectionQueue.Count < index)
            {
                return server.ConnectionQueue[index];
            }

            return null;
        }
    }


    public virtual IJoinQueueHandler.IPlayerConnectResult OnPlayerConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements)
    {
        if (server.Clients.Count - 1 < server.Config.MaxClients)
        {
            // Server has capacity
            return new IJoinQueueHandler.IPlayerConnectResult.Join();
        }

        var playerData = server.PlayerDataManager.GetOrCreateServerPlayerData(client.SentPlayerUid);
        if (playerData.HasPrivilege(Privilege.controlserver, server.Config.RolesByCode) && playerData.HasPrivilege("ignoremaxclients", server.Config.RolesByCode))
        {
            // Privilege based bypassing
            return new IJoinQueueHandler.IPlayerConnectResult.Join();
        }

        if (server.Config.MaxClientsInQueue <= 0)
        {
            // Queue is disabled
            return new IJoinQueueHandler.IPlayerConnectResult.Disconnect(Lang.Get("Server is full ({0} max clients)", server.Config.MaxClients));
        }

        if (server.ConnectionQueue.Count >= server.Config.MaxClientsInQueue)
        {
            // Queue is full
            return new IJoinQueueHandler.IPlayerConnectResult.Disconnect(Lang.Get("Server is full ({0} max clients)", server.Config.MaxClients));
        }

        int position;
        lock (server.ConnectionQueue)
        {
            // Add the player to the queue
            server.ConnectionQueue.Add(new QueuedClient(client, clientIdentPacket, entitlements));
            position = server.ConnectionQueue.Count;
        }

        ServerMain.Logger.Notification($"Player {clientIdentPacket.Playername} was put into the connection queue at position {position}");
        
        // Display the queue screen to the player
        return new IJoinQueueHandler.IPlayerConnectResult.Queue(position);
    }

    public virtual QueuedClient? GetNextQueuedClient(bool remove)
    {
        lock (server.ConnectionQueue)
        {
            var queuedClient = server.ConnectionQueue.FirstOrDefault(null as QueuedClient);
            if (queuedClient != null && remove)
            {
                // Remove the next client from the queue.
                server.ConnectionQueue.RemoveAll(e => e.Client.Id == queuedClient.Client.Id);
            }
            return queuedClient;
        }
    }
    
    public virtual void OnPlayerDisconnect(ConnectedClient disconnectingClient)
    {
        if (server.Config.MaxClientsInQueue <= 0 || server.stopped)
        {
            // Queue is disabled or server has been stopped
            return;
        }

        List<QueuedClient> joiningClients;
        lock (server.ConnectionQueue)
        {
            if (disconnectingClient.State == EnumClientState.Queued)
            {
                // Client was in the queue. Remove them from it.
                server.ConnectionQueue.RemoveAll(e => e.Client.Id == disconnectingClient.Id);
            }

            var queueSize = server.ConnectionQueue.Count;
            if (queueSize == 0)
            {
                // Nobody is in the queue. Nothing to do.
                return;
            }

            var availableSlots = server.Config.MaxClients - server.Clients.Count;
            if (availableSlots <= 0)
            {
                // No room for anybody to join. Nothing to do.
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
            server.FinalizePlayerIdentification(joiningClient.Identification, joiningClient.Client, joiningClient.Entitlements);
        }

        SendPositionUpdate();
    }

    public virtual void SendPositionUpdate()
    {
        // We clone the collection to an array so that we do not need to worry about locking while sending queue position updates to clients in the queue.
        var queuedClients = server.ConnectionQueue.ToArray();
            
        for (var index = 0; index < queuedClients.Length; index++)
        {
            server.SendPacket(queuedClients[index].Client.Id, new Packet_Server
            {
                Id = 82,
                QueuePacket = new Packet_QueuePacket
                {
                    Position = index + 1
                }
            });
        }
    }

    public virtual void OnPlayerJoined(string playerUid, int clientId)
    {
        // Nothing by default
    }

    public virtual void Reset()
    {
        lock (server.ConnectionQueue)
        {
            foreach (var queuedClient in server.ConnectionQueue.ToArray())
            {
                server.DisconnectPlayer(queuedClient.Client, null, "Join queue reset");
            }
            server.ConnectionQueue.Clear();
        }
    }
}