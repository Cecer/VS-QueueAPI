using QueueAPI.Harmony.Accessors;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI.Handlers;

public interface IJoinQueueHandler
{
    /// <summary>
    /// The number of players currently joined to the server. This does not include players waiting in the queue.
    /// </summary>
    int JoinedPlayerCount { get; }
    
    /// <summary>
    /// The number of players currently waiting in the queue.
    /// </summary>
    int QueueSize { get; }

    /// <summary>
    /// Retrieves the index of the specified client within the queue.
    /// </summary>
    /// <param name="clientId">The connection ID of the client whose position within the queue is being looked up.</param>
    /// <returns>
    /// The 0-based index of the client in th queue, or -1 if the client is not found.
    /// </returns>
    int GetClientQueueIndex(int clientId);
    
    /// <summary>
    /// Retrieves the client at the specified position in the queue.
    /// </summary>
    /// <param name="index">The 0-based index of the client within the queue.</param>
    /// <returns>
    /// The client located at the given index in the queue if the index is valid; otherwise, null.
    /// </returns>
    QueuedClient? GetClientAtQueueIndex(int index);

    /// <summary>
    /// Retrieves the client at the front of the queue.
    /// </summary>
    /// <param name="remove">A boolean value indicating whether the client should be removed from the queue after retrieval.</param>
    /// <returns>
    /// The next client in the queue if one exists; otherwise, null.
    /// </returns>
    QueuedClient? GetNextQueuedClient(bool remove);
    
    /// <summary>
    /// Called when a player initially connects to the server prior to any queue processing.
    /// The handler is responsible for tracking the connection data if <see cref="IPlayerConnectResult.Queue"/> is returned.
    /// </summary>
    /// <param name="clientIdentPacket">The Packet_ClientIdentification sent by the client.</param>
    /// <param name="client"></param>
    /// <param name="entitlements"></param>
    /// <returns>
    /// To allow the player to join the server, return a <see cref="IPlayerConnectResult.Join"/>.<para />
    /// To reject the player joining completely, return a <see cref="IPlayerConnectResult.Disconnect"/> with reason that will be displayed to the player.<para />
    /// To display the queue screen to the player, return a <see cref="IPlayerConnectResult.Queue"/> with the initial queue position.<para />
    /// </returns>
    IPlayerConnectResult OnPlayerConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements);

    /// <summary>
    /// Called when a player disconnects from the server for any reason.
    /// Mostly used to remove the client from the queue if they were waiting.
    /// </summary>
    /// <param name="disconnectingClient">The client that disconnected</param>
    void OnPlayerDisconnect(ConnectedClient disconnectingClient);
    
    /// <summary>
    /// Sends a queue position update to all clients in the queue.
    /// <para />
    /// The <see cref="Extensions.SendQueuePositionUpdate(ICoreServerAPI, int, int)" /> convenience method may be useful for implementations of this method.
    /// </summary> 
    void SendPositionUpdate();

    /// <summary>
    /// Called when a client actually joins. The queue does not count.
    /// </summary>
    /// <param name="playerUid">The UID of the player who joined</param>
    /// <param name="clientId">The connection ID of the client of the player who just joined</param>
    public void OnPlayerJoined(string playerUid, int clientId) { }
    
    /// <summary>
    /// Resets all queue state of this handler. Any players currently in the queue should probably be kicked as they will no longer tracked..
    /// </summary>
    public void Reset();

    interface IPlayerConnectResult
    {
        void Handle(ServerMain server, Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements);

        /// <summary>
        /// Allow the player to join the server immediately.
        /// </summary>
        readonly struct Join : IPlayerConnectResult
        {
            public void Handle(ServerMain server, Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements) => server.FinalizePlayerIdentification(clientIdentPacket, client, entitlements);
        }

        /// <summary>
        /// Hold the player on the queue screen.
        /// </summary>
        /// <param name="position">The initial queue position to show to the player.</param>
        readonly struct Queue(int position) : IPlayerConnectResult
        {
            public void Handle(ServerMain server, Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements)
            {
                var queuePacket = new Packet_Server
                {
                    Id = 82,
                    QueuePacket = new Packet_QueuePacket
                    {
                        Position = position
                    }
                };
                server.SendPacket(client.Id, queuePacket);
            }
        }

        /// <summary>
        /// Prevent the player from joining the server entirely by closing the connection.
        /// </summary>
        /// <param name="reason">The reason to display to the user when they are disconnected.</param>
        readonly struct Disconnect(string reason) : IPlayerConnectResult
        {
            public void Handle(ServerMain server, Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements) => server.DisconnectPlayer(client, null, reason);
        }
    }
}