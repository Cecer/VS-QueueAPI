
using Vintagestory.Server;

namespace QueueAPI.SampleHandlers.Dummy;

/// <summary>
/// A simple <see cref="IJoinQueue"/> implementation that does nothing.
/// It is always empty and silently discards all additions.
/// </summary>
public class DummyQueue : IJoinQueue
{
    /// <inheritdoc />
    public bool IsQueueEnabled => false;
    /// <inheritdoc />
    public int QueuePopulation => 0;
    /// <inheritdoc />
    public int QueueTotalCapacity => 0;

    /// <inheritdoc />
    public QueuedClient? Add(QueuedClient client) => null;

    /// <inheritdoc />
    public bool Remove(int clientId, out QueuedClient? removed)
    {
        removed = null;
        return false;
    }

    /// <inheritdoc />
    public bool Remove(string playerUid, out QueuedClient? removed)
    {
        removed = null;
        return false;
    }

    /// <inheritdoc />
    public QueuedClient? RemoveNext() => null;

    /// <inheritdoc />
    public void SchedulePositionUpdate(QueuedClient client, int position) { }

    /// <inheritdoc />
    public void SendPendingPositionUpdates() { }

    /// <inheritdoc />
    public void RemoveAll(string? message) { }

    /// <inheritdoc />
    public int GetClientPosition(int clientId) => -1;

    /// <inheritdoc />
    public ConnectedClient? GetClientAtPosition(int position) => null;
}