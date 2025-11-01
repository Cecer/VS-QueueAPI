using System;
using QueueAPI.Handlers;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI.SampleHandlers.Dummy;

public class DummyQueue(ServerMain server) : AbstractJoinQueue<QueuedClient>(server)
{
    public DummyQueue(ICoreServerAPI api) : this(api.GetInternalServer()) { }

    public override bool IsQueueEnabled => false;
    public override int QueuePopulation => 0;
    public override int QueueTotalCapacity => 0;
    public override int WorldPopulation => 0;
    public override int WorldTotalCapacity => 0;

    public override int GetClientPosition(int clientId) => -1;
    public override QueuedClient? GetClientAtPosition(int position) => null;

    protected override int AddToTail(QueuedClient client) => -1;

    protected override QueuedClient? RemoveFromHead() => null;

    protected override int RemoveByClientId(int clientId, out QueuedClient? client)
    {
        client = null;
        return -1;
    }

    protected override int RemoveByPlayerUid(string playerUid, out QueuedClient? client)
    {
        client = null;
        return -1;
    }

    public override void InvalidateQueuePositions(Range range) { }

    public override void Reset() { }
}