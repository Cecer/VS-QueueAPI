namespace QueueAPI.Default;

public enum AcceptanceResult
{
    /// <summary>
    /// Join the world immediately.
    /// </summary>
    Accept,

    /// <summary>
    /// Enter the queue. 
    /// </summary>
    Queue,

    /// <summary>
    /// Disconnect immediately due to the server being completely full.
    /// </summary>
    ServerFull
}