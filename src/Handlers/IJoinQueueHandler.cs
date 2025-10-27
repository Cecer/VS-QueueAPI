using QueueAPI.Harmony.Accessors;
using Vintagestory.Server;

namespace QueueAPI.Handlers;

public interface IJoinQueueHandler
{
    int JoinedPlayerCount { get; }
    int QueueSize { get; }

    int GetClientQueueIndex(int clientId);
    QueuedClient? GetClientAtQueueIndex(int index);

    QueuedClient? GetNextQueuedClient(bool remove);
    
    IPlayerConnectResult OnPlayerConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements);
    void OnPlayerDisconnect(ConnectedClient disconnectingClient);
    
    void SendPositionUpdate();

    /// <summary>
    /// Called when a client actually joins. The queue does not count.
    /// </summary>
    /// <param name="playerUid">The UID of the player who joined</param>
    /// <param name="clientId">The ID of the client of the player who just joined</param>
    public void OnPlayerJoined(string playerUid, int clientId);

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