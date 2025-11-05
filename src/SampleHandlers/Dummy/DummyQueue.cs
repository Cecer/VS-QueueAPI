
using Vintagestory.Server;

namespace QueueAPI.SampleHandlers.Dummy;

public class DummyQueue(ServerMain server) : IJoinQueue
{
    public bool IsQueueEnabled => false;
    public int QueuePopulation => 0;
    public int QueueTotalCapacity => 0;

    public QueuedClient? Add(QueuedClient client) => null;

    public bool Remove(int clientId, out QueuedClient? removed)
    {
        removed = null;
        return false;
    }

    public bool Remove(string playerUid, out QueuedClient? removed)
    {
        removed = null;
        return false;
    }

    public QueuedClient? RemoveNext() => null;

    public void SchedulePositionUpdate(QueuedClient client, int position) { }

    public void SendPendingPositionUpdates() { }

    public void RemoveAll(string message) { }

    public int GetClientPosition(int clientId) => -1;

    public ConnectedClient? GetClientAtPosition(int position) => null;
}