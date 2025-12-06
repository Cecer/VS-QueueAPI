using System.Threading;
using HarmonyLib;
using QueueAPI.Default;
using Vintagestory.Server;

namespace QueueAPI;

internal static class InternalHooks
{
    private static readonly ServerMain Server = (ServerMain) typeof(ServerProgram).DeclaredField("server").GetValue(null)!;

    private static Thread? _mainServerThread;
    private static bool IsMainServerThread => Thread.CurrentThread == _mainServerThread;

    /// <summary>
    /// Must be called at least once before <see cref="IsMainServerThread"/> is used.
    /// This should already be taken care of by <see cref="QueueAPIModSystem"/>.
    /// </summary>
    internal static void DetectMainServerThread()
    {
        Server.EnqueueMainThreadTask(() =>
        {
            _mainServerThread = Thread.CurrentThread;
            Server.Api.Logger.Debug($"[QueueAPI] Detected main server thread: {_mainServerThread.ManagedThreadId}");
        });
    }


    private static readonly object HandlerLock = new();
    private static IQueueAPIHandler _handler = new DefaultQueueAPIHandler(Server);
    internal static IQueueAPIHandler Handler
    {
        get => _handler;
        set
        {
            lock (HandlerLock)
            {
                var oldHandler = _handler;
                _handler = value;
                _handler.OnAttached(oldHandler);
                oldHandler.OnDetached(_handler);
            }
        }
    }

    /// <summary>
    /// Returns the number of players waiting in the queue.
    /// </summary>
    /// <remarks>This would make more sense as a readonly property, but a method call results in cleaner patching code.</remarks>
    internal static int GetQueueSize() => Handler.Queue.QueuePopulation;

    /// <summary>
    /// Returns the number of players joined into the world.
    /// </summary>
    /// <remarks>This would make more sense as a readonly property, but a method call results in cleaner patching code.</remarks>
    internal static int GetWorldPopulation() => Handler.WorldPopulation;

    /// <summary>
    /// The client's (1-indexed) position in the join queue.
    /// </summary>
    /// <param name="clientId">The ID of the client to get the position of.</param>
    /// <returns>The 1-indexed (the head of the queue is at position 1, there is no position 0) position of the specified client. If no such client ID is queued, -1 is returned..</returns>
    internal static int GetClientPosition(int clientId) => Handler.Queue.GetClientPosition(clientId);

    /// <summary>
    /// Get the client at a specified position in the join queue.
    /// </summary>
    /// <param name="position">The position of the player. This is 1-indexed (the head of the queue is at position 1, there is no position 0). Out of bounds values are ignored and simply return null.</param>
    /// <returns>The client at the specified position or null if there is no client at the specified position.</returns>
    internal static ConnectedClient? GetClientAtPosition(int position) => Handler.Queue.GetClientAtPosition(position);

    internal static void OnPlayerConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements)
    {
        Handler.OnClientConnect(clientIdentPacket, client, entitlements);
    }

    internal static void OnPlayerDisconnect(ConnectedClient client, string? othersReason, string? theirReason)
    {
        if (IsMainServerThread)
        {
            Handler.OnClientDisconnect(client, othersReason, theirReason);
        }
        else
        {
            Server.EnqueueMainThreadTask(() =>
            {
                Handler.OnClientDisconnect(client, othersReason, theirReason);
            });
        }
    }

    internal static void OnPlayerAccepted(string playerUid)
    {
        var client = Server.GetClientByUID(playerUid);
        if (client != null)
        {
            Handler.OnClientAccepted(client);
        }
    }
}