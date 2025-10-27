using HarmonyLib;
using QueueAPI.Handlers;
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
    private static ServerMain _server = (ServerMain) typeof(ServerProgram).DeclaredField("server").GetValue(null)!;
    
    private static object _handlerLock = new();
    private static IJoinQueueHandler _handler = new SimpleJoinQueueHandler(_server);
    internal static IJoinQueueHandler Handler
    {
        get => _handler;
        set
        {
            lock (_handlerLock)
            {
                var oldHandler = _handler;
                _handler = value;
                
                if (oldHandler.QueueSize > 0)
                {
                    _server.Api.Logger.Warning($"The queue API handler was changed but the old queue was not empty. Resetting the old queue state! All players in the odl queue handler be kicked.");
                    _handler.Reset();
                }
                
                if (_handler.QueueSize > 0)
                {
                    _server.Api.Logger.Warning($"The queue API handler was changed but the new queue is not empty. Resetting the new queue! All players in the new queue handler will be kicked.");
                    _handler.Reset();
                }
            }
        }
    }

    /// <summary>
    /// Returns the number of players waiting in the queue.
    /// </summary>
    /// <remarks>This would make more sense as a readonly property, but a method call results in cleaner patching code.</remarks>
    internal static int GetQueueSize() => Handler.QueueSize;

    /// <summary>
    /// Returns the number of joined players in the queue.
    /// </summary>
    /// <remarks>This would make more sense as a readonly property, but a method call results in cleaner patching code.</remarks>
    internal static int GetJoinedPlayerCount() => Handler.JoinedPlayerCount;

    /// <summary>
    /// Returns the client's position in the join queue.
    /// </summary>
    internal static int GetClientQueueIndex(int clientId) => Handler.GetClientQueueIndex(clientId);

    /// <summary>
    /// Returns the client's position in the join queue.
    /// </summary>
    internal static QueuedClient? GetClientAtQueueIndex(int index) => Handler.GetClientAtQueueIndex(index);

    // Called by ServerMain.PreFinalizePlayerIdentification.
    internal static void OnPlayerConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements) => Handler.OnPlayerConnect(clientIdentPacket, client, entitlements).Handle(_server, clientIdentPacket, client, entitlements);

    internal static void OnPlayerDisconnect(ConnectedClient client) => Handler.OnPlayerDisconnect(client);

    internal static void OnPlayerJoined(string playerUid)
    {
        var client = _server.GetClientByUID(playerUid);
        if (client != null)
        {
            Handler.OnPlayerJoined(playerUid, client.Id);
        }
    }
}