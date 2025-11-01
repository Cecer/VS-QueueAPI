namespace QueueAPI.Handlers;

public enum RequestJoinResult
{
    /// <summary>
    /// Join the world immediately.
    /// </summary>
    Join,

    /// <summary>
    /// Enter the queue. 
    /// </summary>
    Queue,

    /// <summary>
    /// Disconnect immediately due to the server being completely full.
    /// </summary>
    ServerFull
}