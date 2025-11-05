using QueueAPI.Harmony.Accessors;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI.Default;


/// <inheritdoc />
/// <param name="server"></param>
public class DefaultQueueAPIEventHandler(ServerMain server) : IQueueAPIEventHandler
{
    public IJoinQueue Queue { get; } = new DefaultJoinQueue(server);

    /// <summary>
    /// The number of clients currently in the world.
    /// This number may be higher <see cref="WorldTotalCapacity"/> if the world is over capacity.
    /// </summary>
    public int WorldPopulation => server.Clients.Count - Queue.QueuePopulation;

    /// <summary>
    /// The maximum number of clients allowed in the world concurrently.
    /// This number may be lower than <see cref="WorldPopulation"/> if the world is over capacity.
    /// </summary>
    public int WorldTotalCapacity => server.Config.MaxClients;

    private string? ServerFullKickMessage => Lang.Get("Server is full ({0} max clients)", server.Config.MaxClients);
    private string? ConcurrentLoginKickMessage => null; // No kick message given (this matches the vanilla behaviour)

    /// <summary>
    /// Checks the state of the server and queue to determine whether the client should be accepted, placed in the queue or rejected.
    /// </summary>
    /// <param name="client">The client trying to join the server</param>
    /// <returns>An <see cref="AcceptanceResult"/> indicating the appropriate action to take on the client.</returns>
    private AcceptanceResult RequestAcceptance(ConnectedClient client)
    {
        // Because connecting clients count towards the total count, we subtract them.
        var capacityAdjustment = client.State == EnumClientState.Connecting ? 1 : 0;
        if ((this as IQueueAPIEventHandler).WorldRemainingCapacity + capacityAdjustment > 0)
        {
            // The world has capacity
            return AcceptanceResult.Accept;
        }

        var playerData = server.PlayerDataManager.GetOrCreateServerPlayerData(client.SentPlayerUid);
        if (playerData.HasPrivilege(Privilege.controlserver, server.Config.RolesByCode) || playerData.HasPrivilege("ignoremaxclients", server.Config.RolesByCode))
        {
            // Privilege based bypassing
            return AcceptanceResult.Accept;
        }

        if (!Queue.IsQueueEnabled)
        {
            return AcceptanceResult.ServerFull;
        }

        if (client.State != EnumClientState.Queued && Queue.IsQueueFull) // We ignore the queue limit when they're already in the queue
        {
            return AcceptanceResult.ServerFull;
        }

        return AcceptanceResult.Queue;
    }

    /// <inheritdoc />
    public void OnClientConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements)
    {
        var acceptResult = RequestAcceptance(client);
        switch (acceptResult)
        {
            case AcceptanceResult.Accept:
                server.FinalizePlayerIdentification(clientIdentPacket, client, entitlements);
                // No need to check for concurrent logins as this is handled by the vanilla server upon joining connection.
                break;
            
            case AcceptanceResult.Queue:
                var queuedClient = new QueuedClient(client, clientIdentPacket, entitlements);
                var replacedClient = Queue.Add(queuedClient);
                if (replacedClient != null)
                {
                    server.DisconnectPlayer(replacedClient.Client, null, ConcurrentLoginKickMessage);
                }
                client.State = EnumClientState.Queued;
                Queue.SendPendingPositionUpdates();
                break;
            
            case AcceptanceResult.ServerFull:
                server.DisconnectPlayer(client, null, ServerFullKickMessage);
                break;
            
            default:
                server.DisconnectPlayer(client, null, $"Server error: The queue event handler returned an invalid response to {nameof(RequestAcceptance)} (ERR_QAPI_RA_{acceptResult})");
                break;
        }
    }


    /// <inheritdoc />
    public void OnClientAccepted(ConnectedClient client) { }

    /// <inheritdoc />
    public void OnClientDisconnect(int clientId)
    {
        Queue.Remove(clientId, out _);

        QueuedClient? queuedClient;
        while (!((IQueueAPIEventHandler)this).IsWorldFull && (queuedClient = Queue.RemoveNext()) != null)
        {
            server.FinalizePlayerIdentification(queuedClient.Identification, queuedClient.Client, queuedClient.Entitlements);
        }
        Queue.SendPendingPositionUpdates();
    }
    
    /// <inheritdoc />
    public void OnAttached(IQueueAPIEventHandler? previousHandler)
    {
        if (!Queue.IsQueueEmpty)
        {
            server.Api.Logger.Warning("The Queue API handler was changed but the new queue is not empty. Resetting the new queue! All players currently in the new queue will be kicked.");
            Queue.RemoveAll("Queue reset");
        }
    }

    /// <inheritdoc />
    public void OnDetached(IQueueAPIEventHandler? newHandler)
    {
        if (!Queue.IsQueueEmpty)
        {
            server.Api.Logger.Warning($"The queue API handler was changed but the old queue was not empty. Resetting the old queue! All players in the old queue be kicked.");
            Queue.RemoveAll("Queue reset");
        }
    }
}