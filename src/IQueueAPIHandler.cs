using Vintagestory.Server;

namespace QueueAPI;

/// <summary>
/// Handles the "low level" events from the opcode patches.
/// Implementations of this interface are responsible for handling critical states in a protocol-compliant manner.
/// </summary>
public interface IQueueAPIHandler
{
    /// <summary>
    /// The number of clients currently in the world.
    /// This number may be higher <see cref="WorldTotalCapacity"/> if the world is over capacity.
    /// </summary>
    int WorldPopulation { get; }

    /// <summary>
    /// The maximum number of clients allowed in the world concurrently.
    /// This number may be lower than <see cref="WorldPopulation"/> if the world is over capacity.
    /// </summary>
    int WorldTotalCapacity { get; }

    /// <summary>
    /// The number of additional clients that may join the world before it is full.
    /// This number may be negative if the world is over capacity.
    /// </summary>
    int WorldRemainingCapacity => WorldTotalCapacity - WorldPopulation;

    /// <summary>
    /// Whether the queue is considered full.
    /// </summary>
    bool IsWorldFull => WorldRemainingCapacity <= 0;

    IJoinQueue Queue { get; }

    /// <summary>
    /// Called when a client connects to the server.
    /// Implementations of this method MUST do one of the following:
    /// <list type="table">
    ///     <listheader>
    ///         <term>Action</term>
    ///         <description>Description</description>
    ///     </listheader>
    ///     <item>
    ///         <term>Accept the client</term>
    ///         <description>
    ///             The client is accepted into the server as an active player by calling
    ///             <see cref="ServerMain.FinalizePlayerIdentification"/>.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Queue the client</term>
    ///         <description>
    ///             The client is placed into a queue state. The <c>ConnectedClient.State</c> value
    ///             must be set to <c>Queued</c>. The client should be sent at least one queue position update.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Reject the client</term>
    ///         <description>
    ///             The client is denied entry into the server and <see cref="Main.DisconnectPlayer"/> is used
    ///             to disconnect the player.
    ///         </description>
    ///     </item>
    /// </list>
    /// </summary>
    void OnClientConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements);

    /// <summary>
    /// Called when a client is accepted into the server and is about to be sent the server data.
    /// </summary>
    /// <param name="client">The client that was accepted</param>
    /// <remarks>This method is always called on the main thread.</remarks>
    void OnClientAccepted(ConnectedClient client);

    /// <summary>
    /// Called when a client disconnects. This is called on matter what state the client is currently in.
    /// </summary>
    /// <param name="client">The client that disconnected</param>
    /// <param name="othersReason">The disconnect message shown in chat</param>
    /// <param name="theirReason">The disconnect message shown to the disconnected player</param>
    /// <remarks>This method is at risk of being called from a non-main thread. Be careful.</remarks>
    void OnClientDisconnect(ConnectedClient client, string? othersReason, string? theirReason);

    /// <summary>
    /// Called when this handler is attached to the Queue API.
    /// </summary>
    /// <param name="previousHandler">The previous handler</param>
    void OnAttached(IQueueAPIHandler? previousHandler);

    /// <summary>
    /// Called when this handler is detached from the Queue API.
    /// </summary>
    /// <param name="newHandler">The new handler</param>
    void OnDetached(IQueueAPIHandler? newHandler);
}