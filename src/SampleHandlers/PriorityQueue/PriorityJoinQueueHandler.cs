using System.Collections.Generic;
using System.Linq;
using QueueAPI.Handlers;
using QueueAPI.Harmony.Accessors;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI.SampleHandlers.PriorityQueue;

/// <summary>
/// This sample handler demonstrates how to implement a two-tier queue based on the return value of <see cref="HasPriority(string)"/>.
/// </summary>
class PriorityJoinQueueHandler(ServerMain server) : IJoinQueueHandler
{
    private readonly List<QueuedClient> StandardConnectionQueue = new();
    private readonly List<QueuedClient> PriorityConnectionQueue = new();
    
    public virtual int JoinedPlayerCount => server.Clients.Count - StandardConnectionQueue.Count - PriorityConnectionQueue.Count;
    public virtual int QueueSize => StandardConnectionQueue.Count + PriorityConnectionQueue.Count;

    public virtual int GetClientQueueIndex(int clientId)
    {
        lock (StandardConnectionQueue)
        {
            int index = PriorityConnectionQueue.FindIndex(c => c.Client.Id == clientId);
            if (index >= 0)
            {
                return index;       
            }
            return StandardConnectionQueue.FindIndex(c => c.Client.Id == clientId) + PriorityConnectionQueue.Count;
        }
    }

    public virtual QueuedClient? GetClientAtQueueIndex(int index)
    {
        lock (StandardConnectionQueue)
        {
            var priorityCount = PriorityConnectionQueue.Count;
            if (index < priorityCount)
            {
                return PriorityConnectionQueue[index];
            }
            index -= priorityCount;
            if (index < StandardConnectionQueue.Count)
            {
                return StandardConnectionQueue[index - priorityCount];
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

        if (QueueSize >= server.Config.MaxClientsInQueue)
        {
            // Queue is full
            return new IJoinQueueHandler.IPlayerConnectResult.Disconnect(Lang.Get("Server is full ({0} max clients)", server.Config.MaxClients));
        }

        int position;
        lock (StandardConnectionQueue)
        {
            // Add the player to the queue
            if (HasPriority(clientIdentPacket.Playername))
            {
                PriorityConnectionQueue.Add(new QueuedClient(client, clientIdentPacket, entitlements));
                position = PriorityConnectionQueue.Count;                
                ServerMain.Logger.Notification($"Player {clientIdentPacket.Playername} was put into the priority connection queue at position {position}");
            }
            else
            {
                StandardConnectionQueue.Add(new QueuedClient(client, clientIdentPacket, entitlements));
                position = QueueSize;               
                ServerMain.Logger.Notification($"Player {clientIdentPacket.Playername} was put into the standard connection queue at position {position}");
            }
        }

        
        // Display the queue screen to the player
        return new IJoinQueueHandler.IPlayerConnectResult.Queue(position);
    }

    public virtual QueuedClient? GetNextQueuedClient(bool remove)
    {
        lock (StandardConnectionQueue)
        {
            List<QueuedClient> queue = PriorityConnectionQueue;
            
            var queuedClient = queue.FirstOrDefault(null as QueuedClient);
            if (queuedClient == null)
            {
                queue = StandardConnectionQueue;
                queuedClient = queue.FirstOrDefault(null as QueuedClient);
            }
            if (queuedClient == null)
            {
                // No more players in either queue.
                return null;
            }
            
            if (remove) {
                // Remove the next client from the queue.
                queue.RemoveAll(e => e.Client.Id == queuedClient.Client.Id);
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
        lock (StandardConnectionQueue)
        {
            if (disconnectingClient.State == EnumClientState.Queued)
            {
                // Client was in a queue. Remove them from it.
                PriorityConnectionQueue.RemoveAll(e => e.Client.Id == disconnectingClient.Id);
                StandardConnectionQueue.RemoveAll(e => e.Client.Id == disconnectingClient.Id);
            }

            var queueSize = QueueSize;
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
        var queuedClients = PriorityConnectionQueue.ToArray();
            
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
        
        queuedClients = StandardConnectionQueue.ToArray();
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

    public virtual void OnPlayerJoined(string playerUid, int clientId) { }

    public virtual void Reset()
    {
        lock (StandardConnectionQueue)
        {
            foreach (var queuedClient in PriorityConnectionQueue.ToArray())
            {
                server.DisconnectPlayer(queuedClient.Client, null, "Join queue reset");
            }
            PriorityConnectionQueue.Clear();
            
            foreach (var queuedClient in StandardConnectionQueue.ToArray())
            {
                server.DisconnectPlayer(queuedClient.Client, null, "Join queue reset");
            }
            StandardConnectionQueue.Clear();
        }
    }

    public bool HasPriority(string playerUid)
    {
        return false;
    } 
}