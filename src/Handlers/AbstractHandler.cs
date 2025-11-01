using QueueAPI.Harmony.Accessors;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI.Handlers;

public abstract class AbstractHandler
{
    public abstract AbstractJoinQueue Queue { get; }

    protected abstract bool TryAcceptFromHead();

    public abstract RequestJoinResult RequestJoin(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements);

    public abstract void OnPlayerConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements);

    public abstract void OnPlayerDisconnect(ConnectedClient client);

    public abstract void OnPlayerJoined(string playerUid, int clientId);

    public abstract void AcceptClientIntoWorld(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements);

    public abstract void Reset();
}

public abstract class AbstractHandler<TClient, TQueue>(ServerMain server) : AbstractHandler
    where TClient : QueuedClient
    where TQueue : AbstractJoinQueue<TClient>
{
    protected AbstractHandler(ICoreServerAPI api) : this(api.GetInternalServer()) { }

    public abstract override TQueue Queue { get; }

    /// <summary>
    /// Constructs a new <see cref="TClient"/> instance.
    /// </summary>
    protected abstract TClient CreateQueuedClient(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements);

    /// <summary>
    /// Determines whether the client can connect directly, requires waiting in a queue, or should be immediately disconnected.
    /// </summary>
    /// <param name="queuedClient">The queued client</param>
    public virtual RequestJoinResult RequestJoin(TClient queuedClient) => RequestJoin(queuedClient.Identification, queuedClient.Client, queuedClient.Entitlements);

    /// <summary>
    /// Determines whether the client can connect directly, requires waiting in a queue, or should be immediately disconnected.
    /// This method is NOT thread safe and must be called within with a lock on the Queue.LockObject.
    /// </summary>
    /// <param name="clientIdentPacket">The client's Packet_ClientIdentification packet</param>
    /// <param name="client">The connected client instance sending the join request</param>
    /// <param name="entitlements">The privileges or entitlements associated with the client</param>
    public override RequestJoinResult RequestJoin(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements)
    {
        if (!Queue.IsWorldFull)
        {
            // The world has capacity
            return RequestJoinResult.Join;
        }

        var playerData = server.PlayerDataManager.GetOrCreateServerPlayerData(client.SentPlayerUid);
        if (playerData.HasPrivilege(Privilege.controlserver, server.Config.RolesByCode) && playerData.HasPrivilege("ignoremaxclients", server.Config.RolesByCode))
        {
            // Privilege based bypassing
            return RequestJoinResult.Join;
        }

        if (!Queue.IsQueueEnabled)
        {
            return RequestJoinResult.ServerFull;
        }

        if (Queue.IsQueueFull && client.State != EnumClientState.Queued) // We ignore the queue limit when they're already in the queue
        {
            return RequestJoinResult.ServerFull;
        }

        return RequestJoinResult.Queue;
    }

    public override void OnPlayerConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements)
    {
        lock (Queue.LockObject)
        {
            var result = RequestJoin(clientIdentPacket, client, entitlements);
            switch (result)
            {
                case RequestJoinResult.Join:
                    AcceptClientIntoWorld(clientIdentPacket, client, entitlements);
                    break;

                case RequestJoinResult.Queue:
                    var position = Queue.TryAddToTail(CreateQueuedClient(clientIdentPacket, client, entitlements));
                    if (position == -1)
                    {
                        server.DisconnectPlayer(client, null, Lang.Get("Server is full ({0} max clients)", server.Config.MaxClients));
                    }
                    break;

                case RequestJoinResult.ServerFull:
                    server.DisconnectPlayer(client, null, Lang.Get("Server is full ({0} max clients)", server.Config.MaxClients));
                    break;

                default:
                    server.DisconnectPlayer(client, null, Lang.Get("Error while joining the server: INVALID_REQUEST_JOIN_RESULT"));
                    break;
            }
        }
    }

    protected override bool TryAcceptFromHead()
    {
        TClient? acceptedClient;
        lock (Queue.LockObject)
        {
            acceptedClient = Queue.GetClientAtPosition(1);
            if (acceptedClient == null) return false; // Nobody in the queue

            var result = RequestJoin(acceptedClient);
            switch (result)
            {
                case RequestJoinResult.Join:
                    if (Queue.TryRemoveByClientId(acceptedClient.Client.Id, out _) == -1)
                    {

                    }
                    break;

                case RequestJoinResult.Queue:
                    // We're already in the queue. Nothing to do.
                    return false;

                case RequestJoinResult.ServerFull:
                    server.DisconnectPlayer(acceptedClient.Client, null, Lang.Get("Server is full ({0} max clients)", server.Config.MaxClients));
                    return false;

                default:
                    server.DisconnectPlayer(acceptedClient.Client, null, Lang.Get("Error while joining the server: INVALID_REQUEST_JOIN_RESULT"));
                    return false;
            }
        }

        // If we reach this line, the result must have been join as all other paths return. 
        AcceptClientIntoWorld(acceptedClient);
        return true;
    }

    public override void OnPlayerDisconnect(ConnectedClient client)
    {
        Queue.TryRemoveByPlayerUid(client.SentPlayerUid, out _);
        TryAcceptFromHead();
    }

    public override void OnPlayerJoined(string playerUid, int clientId) { }

    public virtual void AcceptClientIntoWorld(TClient client) => AcceptClientIntoWorld(client.Identification, client.Client, client.Entitlements);
    public override void AcceptClientIntoWorld(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements) => server.FinalizePlayerIdentification(clientIdentPacket, client, entitlements);

    public override void Reset() => Queue.Reset();
}