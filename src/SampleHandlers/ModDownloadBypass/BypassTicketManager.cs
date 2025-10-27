using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace QueueAPI.SampleHandlers.ModDownloadBypass;

class BypassTicketManager(ICoreAPI api)
{
    private readonly Dictionary<string, QueueBypassTicket> _ticketsByPlayerUid = [];
    private readonly Dictionary<QueueBypassTicket, string> _playerUidsByTicket = [];
    
    public int ActiveTicketCount => _ticketsByPlayerUid.Count;

    public bool HasBypassTicket(string playerUid, bool invalidIfValid)
    {
        if (_ticketsByPlayerUid.TryGetValue(playerUid, out var ticket) && ticket.IsValid)
        {
            InvalidateTicket(ticket);
            return true;
        }
        return false;
    }

    internal void IssueTicket(string playerUid, TimeSpan expireAfter)
    {
        var ticket = new QueueBypassTicket(playerUid, DateTime.Now + expireAfter);
        lock (_ticketsByPlayerUid)
        {
            InvalidateAllTicketsByPlayer(playerUid);
            _ticketsByPlayerUid[playerUid] = ticket;
            _playerUidsByTicket[ticket] = playerUid;
        }
        ticket.ListenerId = api.Event.RegisterCallback(_ => InvalidateTicket(ticket), expireAfter.Milliseconds);
    }

    internal void InvalidateAllTicketsByPlayer(string playerUid)
    {
        lock (_ticketsByPlayerUid)
        {
            if (_ticketsByPlayerUid.Remove(playerUid, out var ticket))
            {
                InvalidateTicket(ticket);
            }
        }
    }

    internal void InvalidateTicket(QueueBypassTicket ticket)
    {
        lock (_ticketsByPlayerUid)
        {
            if (ticket.ListenerId == -1)
            {
                // Already invalidated
                return;
            }
            
            _playerUidsByTicket.Remove(ticket);
            api.Event.UnregisterCallback(ticket.ListenerId);
            ticket.ListenerId = -1;

            if (_ticketsByPlayerUid.TryGetValue(ticket.PlayerId, out var playerTicket) && playerTicket == ticket)
            {
                _ticketsByPlayerUid.Remove(ticket.PlayerId);
            }
        }
    }

    internal void Reset()
    {
        lock (_ticketsByPlayerUid)
        {
            _ticketsByPlayerUid.Clear();
            _playerUidsByTicket.Clear();
        }
    }
}