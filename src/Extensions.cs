using System.Reflection;
using Mono.Cecil;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI;

public static class Extensions
{

    /// <summary>
    /// A convenience method for getting the internal <see cref="ServerMain" /> instance.
    /// </summary>
    /// <param name="api"></param>
    /// <returns></returns>
    public static ServerMain GetInternalServer(this ICoreServerAPI api)
    {
        // The api.World instance is actually the ServerMain instance (at least in 1.21.5) 
        return (ServerMain) api.World; 
    }  
    
    /// <summary>
    /// A convenience method for sending a queue position update to a specific client.
    /// </summary>
    /// <param name="api">The server API instance.</param>
    /// <param name="clientId">The connection ID of the client to send the position update to.</param>
    /// <param name="position">The 0-indexed queue position to send to the client.</param>
    public static void SendQueuePositionUpdate(this ICoreServerAPI api, int clientId, int position)
    {
        var server = api.GetInternalServer();
        
        server.SendPacket(clientId, new Packet_Server
        {
            Id = 82,
            QueuePacket = new Packet_QueuePacket
            {
                Position = position + 1
            }
        });
    }
    
    /// <summary>
    /// Compares a FieldReference with a a FieldInfo.
    /// </summary>
    /// <returns>Returns true if both parameters refer to the same field, false otherwise.</returns>
    public static bool Matches(this FieldReference fieldRef, FieldInfo fieldInfo)
    {
        if (fieldRef.DeclaringType.FullName != fieldInfo.DeclaringType?.FullName?.Replace("+", "/")) return false;
        if (fieldRef.Name != fieldInfo.Name) return false;
        if (fieldRef.FieldType.FullName != fieldInfo.FieldType.FullName) return false;
        return true;
    }
}