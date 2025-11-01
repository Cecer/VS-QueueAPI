using System;
using System.Collections.Generic;
using QueueAPI.Handlers;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI.SampleHandlers.PriorityQueue;

// Please note: This sample is completely untested at this time.
//              There is a non-zero chance this doesn't work quite right. 

public class PriorityQueueJoinQueue(ServerMain server) : AbstractJoinQueue<QueuedClient>(server)
{
    PriorityQueueJoinQueue(ICoreServerAPI api) : this(api.GetInternalServer()) { }

    private readonly List<QueuedClient> _standardConnectionQueue = [];
    private readonly List<QueuedClient> _priorityConnectionQueue = [];

    /// <inheritdoc/>
    public override bool IsQueueEnabled => server.Config.MaxClientsInQueue > 0;
    /// <inheritdoc/>
    public override int QueuePopulation => _standardConnectionQueue.Count + _priorityConnectionQueue.Count;
    /// <inheritdoc/>
    public override int QueueTotalCapacity => server.Config.MaxClientsInQueue;

    /// <inheritdoc/>
    public override int WorldPopulation => server.Clients.Count - _standardConnectionQueue.Count - _priorityConnectionQueue.Count;
    /// <inheritdoc/>
    public override int WorldTotalCapacity => server.Config.MaxClients;

    /// <inheritdoc/>
    public override int GetClientPosition(int clientId)
    {
        lock (LockObject)
        {
            var index = _priorityConnectionQueue.FindIndex(c => c.Client.Id == clientId);
            if (index == -1)
            {
                // Not found in priority queue. Check the standard queue
                index = _standardConnectionQueue.FindIndex(c => c.Client.Id == clientId);

                if (index != -1)
                {
                    index += _priorityConnectionQueue.Count;
                }
            }

            return index + 1; // We +1 because the position is 1-indexed.
        }
    }

    /// <inheritdoc/>
    public override QueuedClient? GetClientAtPosition(int position)
    {
        lock (LockObject)
        {
            var priorityCount = _priorityConnectionQueue.Count;
            if (position <= priorityCount)
            {
                return _priorityConnectionQueue[position - 1];
            }

            position -= priorityCount;
            if (position <= _standardConnectionQueue.Count)
            {
                return _standardConnectionQueue[position - 1];
            }

            return null;
        }
    }

    /// <inheritdoc/>
    protected override int AddToTail(QueuedClient client)
    {
        if (HasPriority(client.Client.SentPlayerUid))
        {
            _priorityConnectionQueue.Add(client);
            return _priorityConnectionQueue.Count;
        }

        _standardConnectionQueue.Add(client);
        return _priorityConnectionQueue.Count + _standardConnectionQueue.Count;
    }

    /// <inheritdoc/>
    protected override QueuedClient? RemoveFromHead()
    {
        if (_priorityConnectionQueue.Count > 0)
        {
            var client = _priorityConnectionQueue[0];
            _priorityConnectionQueue.RemoveAt(0);
            return client;
        }
        if (_standardConnectionQueue.Count > 0)
        {
            var client = _standardConnectionQueue[0];
            _standardConnectionQueue.RemoveAt(0);
            return client;
        }

        return null;
    }

    /// <inheritdoc/>
    protected override int RemoveByClientId(int clientId, out QueuedClient? client)
    {
        // We search the standard queue first purely because it's more likely
        //  that a player leaving is less dedicated and willing to wait. It's
        //  also more likely because the player has likely been waiting longer.
        // The difference in performance this makes is so tiny that practically
        //  zero, but I just thought it was a fun bit of reasoning.

        for (var i = 0; i < _standardConnectionQueue.Count; i++)
        {
            if (_standardConnectionQueue[i].Client.Id == clientId)
            {
                client = _standardConnectionQueue[i];
                return i + _priorityConnectionQueue.Count;
            }
        }

        for (var i = 0; i < _priorityConnectionQueue.Count; i++)
        {
            if (_priorityConnectionQueue[i].Client.Id == clientId)
            {
                client = _priorityConnectionQueue[i];
                return i;
            }
        }

        client = null;
        return -1;
    }

    /// <inheritdoc/>
    protected override int RemoveByPlayerUid(string playerUid, out QueuedClient? client)
    {
        for (var i = 0; i < _standardConnectionQueue.Count; i++)
        {
            if (_standardConnectionQueue[i].Client.SentPlayerUid == playerUid)
            {
                client = _standardConnectionQueue[i];
                return i + _priorityConnectionQueue.Count;
            }
        }

        for (var i = 0; i < _priorityConnectionQueue.Count; i++)
        {
            if (_priorityConnectionQueue[i].Client.SentPlayerUid == playerUid)
            {
                client = _priorityConnectionQueue[i];
                return i;
            }
        }

        client = null;
        return -1;
    }

    /// <inheritdoc/>
    public override void InvalidateQueuePositions(Range positionRange)
    {
        var priorityCopy = _priorityConnectionQueue.ToArray();
        var standardCopy = _standardConnectionQueue.ToArray();
        var combinedLength = priorityCopy.Length + standardCopy.Length;

        var start = positionRange.Start.GetOffset(combinedLength);
        var end = positionRange.End.GetOffset(combinedLength);

        for (var i = start; i < end; i++)
        {
            var clientId = i < priorityCopy.Length ? priorityCopy[i].Client.Id : standardCopy[i].Client.Id;
            server.SendQueuePositionUpdate(clientId, i + 1);
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        lock (LockObject)
        {
            foreach (var client in _priorityConnectionQueue.ToArray())
            {
                server.DisconnectPlayer(client.Client, null, "Join queue reset");
            }
            foreach (var client in _standardConnectionQueue.ToArray())
            {
                server.DisconnectPlayer(client.Client, null, "Join queue reset");
            }
            _priorityConnectionQueue.Clear();
            _standardConnectionQueue.Clear();
        }
    }

    public bool HasPriority(string playerUid)
    {
        return false;
    }
}