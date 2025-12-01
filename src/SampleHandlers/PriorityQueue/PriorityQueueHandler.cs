using QueueAPI.Default;
using Vintagestory.Server;

namespace QueueAPI.SampleHandlers.PriorityQueue;

/// <summary>
/// A slight variation on the default join queue that prioritises certain players over others.
/// </summary>
/// <inheritdoc/>
public class PriorityQueueHandler(ServerMain server) : DefaultQueueAPIEventHandler(server)
{
    public override IJoinQueue Queue { get; } = new DefaultJoinQueue(server);
}