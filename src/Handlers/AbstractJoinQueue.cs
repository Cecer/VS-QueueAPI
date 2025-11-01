using System;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI.Handlers;

/// <summary>
/// Do not extend this class directly. Extend the generic <see cref="AbstractJoinQueue{TClient}"/> instead.
/// </summary>
public abstract class AbstractJoinQueue
{
    /// <summary>
    /// Whether the queue is enabled.
    /// Disabled queues should skip all queue logic.
    /// </summary>
    public abstract bool IsQueueEnabled { get; }

    /// <summary>
    /// The number of clients currently waiting in the queue.
    /// This number may be higher than <see cref="QueueTotalCapacity"/> if the queue is over capacity.
    /// </summary>
    public abstract int QueuePopulation { get; }
    /// <summary>
    /// The maximum number of clients allowed in the queue concurrently.
    /// This number may be lower than <see cref="QueuePopulation"/> if the queue is over capacity.
    /// </summary>
    public abstract int QueueTotalCapacity { get; }
    /// <summary>
    /// The number of additional clients that may join the queue before it is full.
    /// This number may be negative if the queue is over capacity.
    /// </summary>
    public virtual int QueueRemainingCapacity => QueueTotalCapacity - QueuePopulation;

    /// <summary>
    /// Whether the queue is considered empty.
    /// Empty queues should skip queue draining logic.
    /// </summary>
    public virtual bool IsQueueEmpty => QueuePopulation == 0;

    /// <summary>
    /// Whether the queue is considered full.
    /// </summary>
    public virtual bool IsQueueFull => QueueRemainingCapacity <= 0;

    /// <summary>
    /// The number of clients currently in the world. 
    /// This number may be higher <see cref="WorldTotalCapacity"/> if the world is over capacity.
    /// </summary>
    public abstract int WorldPopulation { get; }

    /// <summary>
    /// The maximum number of clients allowed in the world concurrently.
    /// This number may be lower than <see cref="WorldPopulation"/> if the world is over capacity.
    /// </summary>
    public abstract int WorldTotalCapacity { get; }

    /// <summary>
    /// The number of additional clients that may join the world before it is full.
    /// This number may be negative if the world is over capacity.
    /// </summary>
    public virtual int WorldRemainingCapacity => WorldTotalCapacity - WorldPopulation;

    /// <summary>
    /// Whether the queue is considered full.
    /// </summary>
    public virtual bool IsWorldFull => WorldRemainingCapacity <= 0;

    /// <summary>
    /// The client's (1-indexed) position in the join queue.
    /// </summary>
    /// <param name="clientId">The ID of the client to get the position of.</param>
    /// <returns>The 1-indexed (the head of the queue is at position 1, there is no position 0) position of the specified client. If no such client ID is queued, -1 is returned..</returns>
    public abstract int GetClientPosition(int clientId);

    /// <summary>
    /// Get the client at a specified position in the join queue.
    /// </summary>
    /// <param name="position">The position of the player. This is 1-indexed (the head of the queue is at position 1, there is no position 0). Out of bounds values are ignored and simply return null.</param>
    /// <returns>The client at the specified position or null if there is no client at the specified position.</returns>
    public abstract QueuedClient? GetClientAtPosition(int position);

    /// <summary>
    /// Removes the client (if any) at the head of the queue, skipping all possible checks and locks.
    /// This method should only be called by <see cref="AbstractJoinQueue{TClient}.TryRemoveFromHead(out TClient)"/> after locking and completing applicable checks.
    /// </summary>
    /// <returns>The removed head of the queue or null if the queue was empty.</returns>
    protected abstract QueuedClient? RemoveFromHead();

    /// <summary>
    /// Sends a queue position update to all queued connections in <paramref name="positionRange"/>.
    /// </summary>
    /// <param name="positionRange">The range of (1-indexed) queue positions to send updates for.</param>
    public abstract void InvalidateQueuePositions(Range positionRange);

    /// <summary>
    /// Kicks all players in the queue and resets the queue to an empty state.
    /// </summary>
    public abstract void Reset();
}


public abstract class AbstractJoinQueue<TClient>(ServerMain server) : AbstractJoinQueue where TClient : QueuedClient
{
    public AbstractJoinQueue(ICoreServerAPI api) : this(api.GetInternalServer()) { }

    /// <summary>
    /// The <see cref="ServerMain"/> instance.
    /// </summary>
    protected readonly ServerMain Server = server;

    /// <summary>
    /// The common object that should be used for locking.
    /// This is the same lock object used by the vanilla code.
    /// </summary>
    public readonly object LockObject = server.ConnectionQueue;

    /// <inheritdoc />
    public abstract override bool IsQueueEnabled { get; }

    /// <inheritdoc />
    public abstract override int QueuePopulation { get; }

    /// <inheritdoc />
    public abstract override int QueueTotalCapacity { get; }

    /// <inheritdoc />
    public override int QueueRemainingCapacity => QueueTotalCapacity - QueuePopulation;

    /// <inheritdoc />
    public override bool IsQueueEmpty => QueuePopulation == 0;

    /// <inheritdoc />
    public override bool IsQueueFull => QueueRemainingCapacity <= 0;

    /// <inheritdoc />
    public override int WorldPopulation { get; }

    /// <inheritdoc />
    public override int WorldTotalCapacity { get; }

    /// <inheritdoc />
    public override int WorldRemainingCapacity => WorldTotalCapacity - WorldPopulation;

    /// <inheritdoc />
    public override bool IsWorldFull => WorldRemainingCapacity <= 0;


    /// <inheritdoc />
    public abstract override int GetClientPosition(int clientId);

    /// <inheritdoc />
    public abstract override TClient? GetClientAtPosition(int position);

    /// <summary>
    /// Attempts to add <paramref name="client"/> to the tail of the queue.
    /// </summary>
    /// <returns>The (1-indexed) position the client was inserted at in the queue or -1 if the client failed to be added.</returns>
    public virtual int TryAddToTail(TClient client)
    {
        int position;
        lock (LockObject)
        {
            if (!IsQueueEnabled) return -1;
            if (IsQueueFull) return -1;
            position = AddToTail(client);
        }

        InvalidateQueuePositions(position..);

        return position;
    }

    /// <summary>
    /// Adds <paramref name="client"/> to the tail of the queue, skipping all possible checks and locks.
    /// This method should only be called by <see cref="AddToTail(TClient)"/> after locking anc completing applicable checks.
    /// </summary>
    protected abstract int AddToTail(TClient client);

    /// <summary>
    /// Removes the client (if any) at the head of the queue.
    /// </summary>
    /// <param name="client"></param>
    /// <returns>True if the queue was empty, false otherwise.</returns>
    public bool TryRemoveFromHead(out TClient? client)
    {
        lock (LockObject)
        {
            if (!IsQueueEnabled)
            {
                client = null;
                return false;
            }

            client = RemoveFromHead();
        }
        InvalidateQueuePositions(..);
        return client != null;
    }

    /// <summary>
    /// Removes the client (if any) at the head of the queue, skipping all possible checks and locks.
    /// This method should only be called by <see cref="TryRemoveFromHead(out TClient)"/> after locking and completing applicable checks.
    /// </summary>
    /// <returns>The removed head of the queue or null if the queue was empty.</returns>
    protected abstract override TClient? RemoveFromHead();

    /// <summary>
    /// Removes the client with ID <paramref name="clientId"/> from the queue. 
    /// </summary>
    /// <param name="clientId">The ID of the client to remove.</param>
    /// <param name="client">The removed client</param>
    /// <returns>The zero-indexed position of the removed client in the queue prior to removal or -1 if no client was removed.</returns>
    public int TryRemoveByClientId(int clientId, out TClient? client)
    {
        int position;
        lock (LockObject)
        {
            if (!IsQueueEnabled)
            {
                client = null;
                return -1;
            }
            position = RemoveByClientId(clientId, out client);
        }

        if (position != -1)
        {
            InvalidateQueuePositions(position..);
        }
        return position;
    }

    /// <summary>
    /// Removes the client with ID <paramref name="clientId"/> from the queue, skipping all possible checks and locks.
    /// This method should only be called by <see cref="TryRemoveByClientId(int, out TClient?)"/> after locking and completing applicable checks.
    /// </summary>
    /// <param name="clientId">The ID of the client to remove</param>
    /// <param name="client">The removed client</param>
    /// <returns>The zero-indexed position of the removed client in the queue prior to removal or -1 if no client was removed.</returns>
    protected abstract int RemoveByClientId(int clientId, out TClient? client);

    /// <summary>
    /// Removes the client with <paramref name="playerUid"/> from the queue. 
    /// </summary>
    /// <param name="playerUid">The UID of the player to remove.</param>
    /// <param name="client">The removed client.</param>
    /// <returns>The zero-indexed position of the removed client in the queue prior to removal or -1 if no client was removed.</returns>
    public virtual int TryRemoveByPlayerUid(string playerUid, out TClient? client)
    {
        int position;
        lock (LockObject)
        {
            if (!IsQueueEnabled)
            {
                client = null;
                return -1;
            }
            position = RemoveByPlayerUid(playerUid, out client);
        }

        if (position != -1)
        {
            InvalidateQueuePositions(position..);
        }
        return position;
    }

    /// <summary>
    /// Removes the client with <paramref name="playerUid"/> from the queue, skipping all possible checks and locks.
    /// This method should only be called by <see cref="TryRemoveByPlayerUid(string, out TClient?)"/> after locking and completing applicable checks.
    /// </summary>
    /// <param name="playerUid">The UID of the player to remove.</param>
    /// <param name="client">The removed client.</param>
    /// <returns>The zero-indexed position of the removed client in the queue prior to removal or -1 if no client was removed.</returns>
    protected abstract int RemoveByPlayerUid(string playerUid, out TClient? client);

    /// <inheritdoc />
    public abstract override void InvalidateQueuePositions(Range positionRange);

    /// <summary>
    /// Kicks all players in the queue and resets the queue to an empty state.
    /// </summary>
    public abstract override void Reset();
}