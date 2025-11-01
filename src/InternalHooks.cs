using HarmonyLib;
using QueueAPI.Handlers;
using QueueAPI.Handlers.Default;
using Vintagestory.Server;

namespace QueueAPI;

/*
 * Patch plan:
 * ServerMain.Process accesses ConnectionQueue.Count and should be replaced with a call to GetQueueSize()
 * ServerMain.PreFinalizePlayerIdentification should call OnPlayerConnect then return. The entire method should be delegated to our handler.
 * ServerMain.UpdateQueuedPlayersAfterDisconnect should call OnPlayerDisconnect then return. The entire method should be delegated to our handler.
 */

internal static class InternalHooks
{
    private static readonly ServerMain _server = (ServerMain) typeof(ServerProgram).DeclaredField("server").GetValue(null)!;

    private static readonly object _handlerLock = new();
    private static AbstractHandler _abstractHandler = new DefaultHandler(_server);
    internal static AbstractHandler AbstractHandler
    {
        get => _abstractHandler;
        set
        {
            lock (_handlerLock)
            {
                var oldHandler = _abstractHandler;
                _abstractHandler = value;

                if (!oldHandler.Queue.IsQueueEmpty)
                {
                    _server.Api.Logger.Warning($"The queue API handler was changed but the old queue was not empty. Resetting the old queue state! All players in the odl queue handler be kicked.");
                    _abstractHandler.Reset();
                }

                if (!_abstractHandler.Queue.IsQueueEmpty)
                {
                    _server.Api.Logger.Warning($"The queue API handler was changed but the new queue is not empty. Resetting the new queue! All players in the new queue handler will be kicked.");
                    _abstractHandler.Reset();
                }
            }
        }
    }

    /// <summary>
    /// Returns the number of players waiting in the queue.
    /// </summary>
    /// <remarks>This would make more sense as a readonly property, but a method call results in cleaner patching code.</remarks>
    internal static int GetQueueSize() => AbstractHandler.Queue.QueuePopulation;

    /// <summary>
    /// Returns the number of joined players in the queue.
    /// </summary>
    /// <remarks>This would make more sense as a readonly property, but a method call results in cleaner patching code.</remarks>
    internal static int GetWorldPopulation() => AbstractHandler.Queue.WorldPopulation;

    /// <summary>
    /// The client's (1-indexed) position in the join queue.
    /// </summary>
    /// <param name="clientId">The ID of the client to get the position of.</param>
    /// <returns>The 1-indexed (the head of the queue is at position 1, there is no position 0) position of the specified client. If no such client ID is queued, -1 is returned..</returns>
    internal static int GetClientPosition(int clientId) => AbstractHandler.Queue.GetClientPosition(clientId);

    /// <summary>
    /// Get the client at a specified position in the join queue.
    /// </summary>
    /// <param name="position">The position of the player. This is 1-indexed (the head of the queue is at position 1, there is no position 0). Out of bounds values are ignored and simply return null.</param>
    /// <returns>The client at the specified position or null if there is no client at the specified position.</returns>
    internal static QueuedClient? GetClientAtPosition(int position) => AbstractHandler.Queue.GetClientAtPosition(position);

    // Called by ServerMain.PreFinalizePlayerIdentification.
    internal static void OnPlayerConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements) => AbstractHandler.OnPlayerConnect(clientIdentPacket, client, entitlements);

    internal static void OnPlayerDisconnect(ConnectedClient client) => AbstractHandler.OnPlayerDisconnect(client);

    internal static void OnPlayerJoined(string playerUid)
    {
        var client = _server.GetClientByUID(playerUid);
        if (client != null)
        {
            AbstractHandler.OnPlayerJoined(playerUid, client.Id);
        }
    }
}