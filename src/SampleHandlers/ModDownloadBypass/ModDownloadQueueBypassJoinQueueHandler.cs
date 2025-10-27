using System;
using System.Collections.Generic;
using QueueAPI.Handlers;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI.SampleHandlers.ModDownloadBypass;

/// <summary>
/// This sample handler will allow players to skip the queue upon if they recently failed to join due to missing mods.
///
/// <para>
/// This works by detecting players who disconnect within <paramref name="quickDisconnectThreshold"/> of being sent the mod list.
/// For such players, their player slot is reserved for them for up to <paramref name="expireTicketsAfter" /> and they will skip the queue.
/// </para>
/// </summary>
/// <param name="api">The server API</param>
/// <param name="quickDisconnectThreshold">The maximum amount of time after being sent the mod list before a player is considered to have successfully joined.</param>
/// <param name="expireTicketsAfter">The maximum amount of time after disconnecting during join that a player may bypass the queue when rejoining</param>
class ModDownloadQueueBypassJoinQueueHandler(ICoreServerAPI api, TimeSpan quickDisconnectThreshold, TimeSpan expireTicketsAfter) : SimpleJoinQueueHandler(api), IJoinQueueHandler
{
    /// <summary>
    /// Responsible for keeping track of bypass tickets.
    /// </summary>
    private readonly BypassTicketManager _bypassTicketManager = new BypassTicketManager(api);
    
    /// <summary>
    /// Holds the IDs of recently joined connections. Connections are removed from this list after <paramref name="quickDisconnectThreshold"/> or when they disconnect. 
    /// </summary>
    private readonly ISet<int> _recentlyJoinedClients = new HashSet<int>();
    
    public override int QueueSize => base.QueueSize + _bypassTicketManager.ActiveTicketCount;

    public override IJoinQueueHandler.IPlayerConnectResult OnPlayerConnect(Packet_ClientIdentification clientIdentPacket, ConnectedClient client, string entitlements)
    {
        if (_bypassTicketManager.HasBypassTicket(client.SentPlayerUid, true))
        {
            // Has bypass ticket
            return new IJoinQueueHandler.IPlayerConnectResult.Join();
        }
        
        return base.OnPlayerConnect(clientIdentPacket, client, entitlements);
    }
    
    public virtual void OnPlayerJoined(string playerUid, int clientId)
    {
        // Keep track of the recently joined players.
        _recentlyJoinedClients.Add(clientId);
        api.Event.RegisterCallback(_ => _recentlyJoinedClients.Remove(clientId), quickDisconnectThreshold.Milliseconds);
    }

    public override void OnPlayerDisconnect(ConnectedClient disconnectingClient)
    {
        if (_recentlyJoinedClients.Remove(disconnectingClient.Id))
        {
            // Player only recently joined. Issue them a bypass ticket.
            _bypassTicketManager.IssueTicket(disconnectingClient.SentPlayerUid, expireTicketsAfter);
        }
    }

    public override void Reset()
    {
        base.Reset();
        _bypassTicketManager.Reset();
        _recentlyJoinedClients.Clear();
    }
}