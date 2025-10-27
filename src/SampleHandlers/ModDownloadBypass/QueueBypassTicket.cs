using System;

namespace QueueAPI.SampleHandlers.ModDownloadBypass; 

public class QueueBypassTicket(string playerId, DateTime expiry)
{
    public string PlayerId => playerId;
    public DateTime Expiry => expiry;
    
    internal long ListenerId;
    
    public bool IsValid => ListenerId != -1;
}