using System.Collections.Generic;
using QueueAPI.Default;
using Vintagestory.Server;

namespace QueueAPI.SampleHandlers.PriorityQueue;

// Please note: This sample is completely untested at this time.
//              There is a non-zero chance this doesn't work quite right.
//              (It also probably isn't even fully implemented yet)

/// <summary>
/// A slight variation on the default join queue that prioritises certain players over others based on the return value of <see cref="HasPriority(string)"/>.
/// By default, only Cecer is given priority.
/// </summary>
/// <inheritdoc/>
public class PriorityQueueJoinQueue(ServerMain server) : DefaultJoinQueue(server)
{
    private readonly ServerMain _server = server;

    private readonly List<QueuedClient> _priorityConnectionQueue = [];
    private readonly List<QueuedClient> _standardConnectionQueue = [];

    /// <inheritdoc/>
    public override int QueuePopulation => _standardConnectionQueue.Count + _priorityConnectionQueue.Count;

    /// <inheritdoc />
    public override QueuedClient? Add(QueuedClient client)
    {
        var hasPriority = HasPriority(client.Client.SentPlayerUid);

        var priorityCount = _priorityConnectionQueue.Count;
        for (var i = 0; i < priorityCount; i++)
        {
            var existing = _priorityConnectionQueue[i];
            if (client.Client.SentPlayerUid == existing.Client.SentPlayerUid)
            {
                _priorityConnectionQueue[i] = client;
                SchedulePositionUpdate(client, i + 1);
                return existing;
            }
        }

        int standardCount = _standardConnectionQueue.Count;
        for (var i = 0; i < standardCount; i++)
        {
            var existing = _standardConnectionQueue[i];
            if (client.Client.SentPlayerUid == existing.Client.SentPlayerUid)
            {
                if (hasPriority)
                {
                    // Remove from standard, add to priority
                    _standardConnectionQueue.RemoveAt(i);
                    _priorityConnectionQueue.Add(client);
                    for (i = 0; i < standardCount; i++)
                    {
                        SchedulePositionUpdate(_standardConnectionQueue[i], priorityCount + i + 2); // +2 because we added to the priority queue
                    }

                    return existing;
                }
                else
                {
                    _standardConnectionQueue[i] = client;
                    SchedulePositionUpdate(client, priorityCount + i + 1);
                    return existing;
                }
            }
        }

        _standardConnectionQueue.Add(client);
        SchedulePositionUpdate(client, priorityCount + standardCount + 1);

        return null;
    }

    /// <inheritdoc />
    public override bool Remove(int clientId, out QueuedClient? removed)
    {
        removed = null;
        var priorityIndex = 0;
        for (; priorityIndex < _priorityConnectionQueue.Count; priorityIndex++)
        {
            if (clientId == _priorityConnectionQueue[priorityIndex].Client.Id)
            {
                removed = _priorityConnectionQueue[priorityIndex];
                _priorityConnectionQueue.RemoveAt(priorityIndex);
                break;
            }
        }
        for (; priorityIndex < _priorityConnectionQueue.Count; priorityIndex++)
        {
            SchedulePositionUpdate(_priorityConnectionQueue[priorityIndex], priorityIndex + 1);
        }

        var standardIndex = 0;
        if (removed == null)
        {
            for (; standardIndex < _standardConnectionQueue.Count; standardIndex++)
            {
                if (clientId == _standardConnectionQueue[standardIndex].Client.Id)
                {
                    removed = _standardConnectionQueue[standardIndex];
                    _standardConnectionQueue.RemoveAt(standardIndex);
                    break;
                }
            }
        }
        for (; standardIndex < _standardConnectionQueue.Count; standardIndex++)
        {
            SchedulePositionUpdate(_standardConnectionQueue[standardIndex], _standardConnectionQueue.Count + standardIndex + 1);
        }

        return removed != null;
    }

    /// <inheritdoc />
    public override bool Remove(string playerUid, out QueuedClient? removed)
    {
        removed = null;
        var priorityIndex = 0;
        for (; priorityIndex < _priorityConnectionQueue.Count; priorityIndex++)
        {
            if (playerUid == _priorityConnectionQueue[priorityIndex].Client.SentPlayerUid)
            {
                removed = _priorityConnectionQueue[priorityIndex];
                _priorityConnectionQueue.RemoveAt(priorityIndex);
                break;
            }
        }
        for (; priorityIndex < _priorityConnectionQueue.Count; priorityIndex++)
        {
            SchedulePositionUpdate(_priorityConnectionQueue[priorityIndex], priorityIndex + 1);
        }

        var standardIndex = 0;
        if (removed == null)
        {
            for (; standardIndex < _standardConnectionQueue.Count; standardIndex++)
            {
                if (playerUid == _standardConnectionQueue[standardIndex].Client.SentPlayerUid)
                {
                    removed = _standardConnectionQueue[standardIndex];
                    _standardConnectionQueue.RemoveAt(standardIndex);
                    break;
                }
            }
        }
        for (; standardIndex < _standardConnectionQueue.Count; standardIndex++)
        {
            SchedulePositionUpdate(_standardConnectionQueue[standardIndex], _standardConnectionQueue.Count + standardIndex + 1);
        }

        return removed != null;
    }

    /// <inheritdoc />
    public override QueuedClient? RemoveNext()
    {
        if (_priorityConnectionQueue.Count > 0)
        {
            var result = _priorityConnectionQueue[0];
            _priorityConnectionQueue.RemoveAt(0);
            for (int i = 0; i < _priorityConnectionQueue.Count; i++)
            {
                SchedulePositionUpdate(_priorityConnectionQueue[i], i + 1);
            }
            for (int i = 0; i < _standardConnectionQueue.Count; i++)
            {
                SchedulePositionUpdate(_standardConnectionQueue[i], _standardConnectionQueue.Count + i + 1);
            }
            return result;
        }

        if (_standardConnectionQueue.Count > 0)
        {
            var result = _standardConnectionQueue[0];
            _standardConnectionQueue.RemoveAt(0);
            for (int i = 0; i < _standardConnectionQueue.Count; i++)
            {
                SchedulePositionUpdate(_standardConnectionQueue[i], _standardConnectionQueue.Count + i + 1);
            }
            return result;
        }

        return null;
    }

    /// <inheritdoc />
    public override void RemoveAll(string? message)
    {
        foreach (var client in _priorityConnectionQueue)
        {
            _server.DisconnectPlayer(client.Client, null, message);
        }
        _priorityConnectionQueue.Clear();

        foreach (var client in _standardConnectionQueue)
        {
            _server.DisconnectPlayer(client.Client, null, message);
        }
        _standardConnectionQueue.Clear();
    }

    /// <inheritdoc />
    public override int GetClientPosition(int clientId)
    {
        for (int index = 0; index < _priorityConnectionQueue.Count; index++)
        {
            if (clientId == _priorityConnectionQueue[index].Client.Id)
            {
                return index + 1;
            }
        }

        for (int index = 0; index < _standardConnectionQueue.Count; index++)
        {
            if (clientId == _standardConnectionQueue[index].Client.Id)
            {
                return _priorityConnectionQueue.Count + index + 1;
            }
        }

        return -1;
    }

    /// <inheritdoc />
    public override ConnectedClient? GetClientAtPosition(int position)
    {
        if (_priorityConnectionQueue.Count >= position)
        {
            position -= _priorityConnectionQueue.Count;
        }
        if (_standardConnectionQueue.Count >= position)
        {
            return null;
        }
        return _standardConnectionQueue[position].Client;
    }

    protected virtual bool HasPriority(string playerUid)
    {
        switch (playerUid)
        {
            case "zAHbSBErC90g6dqn0pTWNRUB": return true; // Give Cecer priority
            default: return false;
        }
    }
}