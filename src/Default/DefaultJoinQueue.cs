using System.Collections.Generic;
using Vintagestory.Server;

namespace QueueAPI.Default;

public class DefaultJoinQueue(ServerMain server) : IJoinQueue
{
    private readonly Dictionary<int, int> _pendingPositionUpdates = new();

    public virtual bool IsQueueEnabled  => server.Config.MaxClientsInQueue > 0;
    public virtual int QueuePopulation => server.ConnectionQueue.Count;
    public virtual int QueueTotalCapacity => server.Config.MaxClientsInQueue;

    /// <inheritdoc />
    public virtual QueuedClient? Add(QueuedClient client)
    {
        var currentQueueSize = server.ConnectionQueue.Count;
        for (var i = 0; i < currentQueueSize; i++)
        {
            var existing = server.ConnectionQueue[i];
            if (client.Client.SentPlayerUid == existing.Client.SentPlayerUid)
            {
                server.ConnectionQueue[i] = client;
                SchedulePositionUpdate(client, i + 1);
                return existing;
            }
        }

        server.ConnectionQueue.Add(client);
        SchedulePositionUpdate(client, currentQueueSize + 1);
        return null;
    }

    /// <inheritdoc />
    public virtual bool Remove(int clientId, out QueuedClient? removed)
    {
        removed = null;
        var index = 0;
        for (; index < server.ConnectionQueue.Count; index++)
        {
            if (clientId == server.ConnectionQueue[index].Client.Id)
            {
                removed = server.ConnectionQueue[index];
                server.ConnectionQueue.RemoveAt(index);
                break;
            }
        }
        for (; index < server.ConnectionQueue.Count; index++)
        {
            SchedulePositionUpdate(server.ConnectionQueue[index], index + 1);
        }

        _pendingPositionUpdates.Remove(clientId);
        return removed != null;
    }

    /// <inheritdoc />
    public virtual bool Remove(string playerUid, out QueuedClient? removed)
    {
        removed = null;
        var index = 0;
        for (; index < server.ConnectionQueue.Count; index++)
        {
            if (playerUid == server.ConnectionQueue[index].Client.SentPlayerUid)
            {
                removed = server.ConnectionQueue[index];
                server.ConnectionQueue.RemoveAt(index);
                break;
            }
        }
        for (; index < server.ConnectionQueue.Count; index++)
        {
            SchedulePositionUpdate(server.ConnectionQueue[index], index + 1);
        }

        if (removed != null)
        {
            _pendingPositionUpdates.Remove(removed.Client.Id);
        }

        return removed != null;
    }

    /// <inheritdoc />
    public virtual QueuedClient? RemoveNext()
    {
        if (server.ConnectionQueue.Count == 0)
        {
            return null;
        }

        var result = server.ConnectionQueue[0];
        server.ConnectionQueue.RemoveAt(0);

        _pendingPositionUpdates.Remove(result.Client.Id);
        return result;
    }

    /// <inheritdoc />
    public virtual void SchedulePositionUpdate(QueuedClient client, int position)
    {
        _pendingPositionUpdates[client.Client.Id] = position;
    }

    /// <inheritdoc />
    public virtual void SendPendingPositionUpdates()
    {
        if (_pendingPositionUpdates.Count == 0) return;
        foreach (var (clientId, position) in _pendingPositionUpdates)
        {
            server.SendQueuePositionUpdate(clientId, position);
        }
        _pendingPositionUpdates.Clear();
    }

    /// <inheritdoc />
    public virtual void RemoveAll(string? message)
    {
        foreach (var client in server.ConnectionQueue)
        {
            server.DisconnectPlayer(client.Client, null, message);
        }

        server.ConnectionQueue.Clear();
        _pendingPositionUpdates.Clear();
    }

    /// <inheritdoc />
    public virtual int GetClientPosition(int clientId)
    {
        for (int index = 0; index < server.ConnectionQueue.Count; index++)
        {
            if (clientId == server.ConnectionQueue[index].Client.Id)
            {
                return index + 1;
            }
        }

        return -1;
    }

    /// <inheritdoc />
    public virtual ConnectedClient? GetClientAtPosition(int position)
    {
        if (server.ConnectionQueue.Count >= position)
        {
            return null;
        }
        return server.ConnectionQueue[position].Client;
    }
}