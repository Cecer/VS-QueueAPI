using System;
using System.Collections.Generic;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI.Handlers.Default;

public class DefaultJoinQueue(ServerMain server) : AbstractJoinQueue<QueuedClient>(server)
{
    public DefaultJoinQueue(ICoreServerAPI api) : this(api.GetInternalServer()) { }

    private readonly List<QueuedClient> _queue = server.ConnectionQueue;

    /// <inheritdoc/>
    public override bool IsQueueEnabled => server.Config.MaxClientsInQueue > 0;
    /// <inheritdoc/>
    public override int QueuePopulation => _queue.Count;
    /// <inheritdoc/>
    public override int QueueTotalCapacity => server.Config.MaxClientsInQueue;

    /// <inheritdoc/>
    public override int WorldPopulation => server.Clients.Count - _queue.Count;
    /// <inheritdoc/>
    public override int WorldTotalCapacity => server.Config.MaxClients;

    /// <inheritdoc/>
    public override int GetClientPosition(int clientId) => _queue.FindIndex(c => c.Client.Id == clientId) + 1;

    /// <inheritdoc/>
    public override QueuedClient? GetClientAtPosition(int position)
    {
        lock (LockObject)
        {
            if (_queue.Count >= position)
            {
                return _queue[position - 1];
            }
            return null;
        }
    }
    /// <inheritdoc/>
    protected override int AddToTail(QueuedClient client)
    {
        _queue.Add(client);
        return _queue.Count;
    }

    /// <inheritdoc/>
    protected override QueuedClient? RemoveFromHead()
    {
        if (server.ConnectionQueue.Count == 0) return null;
        var queue = server.ConnectionQueue[0];
        server.ConnectionQueue.RemoveAt(0);
        return queue;
    }

    /// <inheritdoc/>
    protected override int RemoveByClientId(int clientId, out QueuedClient? client)
    {
        for (var i = 0; i < server.ConnectionQueue.Count; i++)
        {
            if (server.ConnectionQueue[i].Client.Id == clientId)
            {
                client = server.ConnectionQueue[i];
                return i;
            }
        }

        client = null;
        return -1;
    }

    /// <inheritdoc/>
    protected override int RemoveByPlayerUid(string playerUid, out QueuedClient? client)
    {
        for (var i = 0; i < server.ConnectionQueue.Count; i++)
        {
            if (server.ConnectionQueue[i].Client.SentPlayerUid == playerUid)
            {
                client = server.ConnectionQueue[i];
                return i;
            }
        }

        client = null;
        return -1;
    }

    /// <summary>
    /// Sends a queue position update to all players in the queue with a position equal to or greater than <paramref name="positionRange"/>.
    /// </summary>
    /// <param name="positionRange">The range of positions that will receive a queue update.</param>
    public override void InvalidateQueuePositions(Range positionRange)
    {
        var queueCopy = _queue.ToArray();
        var start = positionRange.Start.GetOffset(queueCopy.Length) - 1;
        var end = positionRange.End.GetOffset(queueCopy.Length) - 1;

        if (start == -1)
        {
            start = 0;
        }
        if (end == -1)
        {
            end = queueCopy.Length - 1;
        }

        for (var i = start; i <= end; i++)
        {
            var clientId = queueCopy[i].Client.Id;
            server.SendQueuePositionUpdate(clientId, i + 1);
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        lock (LockObject)
        {
            foreach (var client in _queue.ToArray())
            {
                server.DisconnectPlayer(client.Client, null, "Join queue reset");
            }
            _queue.Clear();
        }
    }
}