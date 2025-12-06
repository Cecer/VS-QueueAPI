using Vintagestory.Server;

namespace QueueAPI;


public interface IJoinQueue
{
    /// <summary>
    /// Whether the queue is enabled.
    /// Disabled queues should skip all queue logic.
    /// </summary>
    bool IsQueueEnabled { get; }

    /// <summary>
    /// The number of clients currently waiting in the queue.
    /// This number may be higher than <see cref="QueueTotalCapacity"/> if the queue is over capacity.
    /// </summary>
    int QueuePopulation { get; }

    /// <summary>
    /// The maximum number of clients allowed in the queue concurrently.
    /// This number may be lower than <see cref="QueuePopulation"/> if the queue is over capacity.
    /// </summary>
    int QueueTotalCapacity { get; }
    /// <summary>
    /// The number of additional clients that may join the queue before it is full.
    /// This number may be negative if the queue is over capacity.
    /// </summary>
    int QueueRemainingCapacity => QueueTotalCapacity - QueuePopulation;

    /// <summary>
    /// Whether the queue is considered empty.
    /// Empty queues should skip queue draining logic.
    /// </summary>
    bool IsQueueEmpty => QueuePopulation == 0;

    /// <summary>
    /// Whether the queue is considered full.
    /// </summary>
    bool IsQueueFull => QueueRemainingCapacity <= 0;


    /// <summary>
    /// Add <paramref name="client"/> to the queue.
    /// If the player is already in the queue, <paramref name="client"/> replaces the existing instance in the queue.
    /// </summary>
    /// <param name="client">The client to add to the queue</param>
    /// <returns>The existing QueuedClient with the UID (or null if no such client is found)</returns>
    QueuedClient? Add(QueuedClient client);

    /// <summary>
    /// Remove <paramref name="client"/> from the queue.
    /// </summary>
    /// <param name="client">The client to remove from the queue</param>
    /// <returns>True if the client was present in the queue, false otherwise.</returns>
    bool Remove(QueuedClient client)
    {
        return Remove(client.Client.Id, out _);
    }

    /// <summary>
    /// Remove the client matching <paramref name="clientId"/> from the queue.
    /// </summary>
    /// <param name="clientId">The ID of the client to remove from the queue</param>
    /// <param name="removed">The removed QueuedClient instance (or null).</param>
    /// <returns>True if the client was present in the queue, false otherwise.</returns>
    bool Remove(int clientId, out QueuedClient? removed);

    /// <summary>
    /// Remove the client of <paramref name="playerUid"/> from the queue.
    /// </summary>
    /// <param name="playerUid">The player UID of the client to remove from the queue</param>
    /// <param name="removed">The removed QueuedClient instance (or null).</param>
    /// <returns>True if the client was present in the queue, false otherwise.</returns>
    bool Remove(string playerUid, out QueuedClient? removed);

    /// <summary>
    /// Removes the next <paramref name="client"/> in line from the queue.
    /// The queue implementation is responsible for determining what is considered "next in line".
    /// </summary>
    /// <returns>The client that was next in line or null if the queue was empty or disabled.</returns>
    QueuedClient? RemoveNext();

    /// <summary>
    /// Schedules a position update to be sent to <paramref name="client"/>.
    /// </summary>
    /// <param name="client">The client to send the update to</param>
    /// <param name="position">The queue position to display to the client</param>
    void SchedulePositionUpdate(QueuedClient client, int position);

    /// <summary>
    /// Sends all scheduled position updates.
    /// </summary>
    void SendPendingPositionUpdates();

    /// <summary>
    /// Disconnects all clients in the queue with the specified message
    /// </summary>
    void RemoveAll(string? message);

    /// <summary>
    /// Get the position of a client in the queue.
    /// </summary>
    /// <param name="clientId">The ID of the client to find the position of</param>
    /// <returns>The 1-indexed position of the client in the queue or -1 if the client is not currently in the queue</returns>
    int GetClientPosition(int clientId);

    /// <summary>
    /// Get the client at position <paramref name="position"/> in the queue.
    /// </summary>
    /// <param name="position">The 1-indexed position in the queue</param>
    /// <returns>The client at the specified position in the queue or null if there is no client at that position</returns>
    ConnectedClient? GetClientAtPosition(int position);
}