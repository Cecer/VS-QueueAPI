using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace QueueAPI.Harmony.Patches;

    
[HarmonyPatch]
public static class ServerMainPatches
{
    /// <summary>
    /// Intercepts all calls to PreFinalizePlayerIdentification and redirects them to use the configured handler.
    /// The original method contents are skipped entirely and are not executed.
    /// </summary>
    /// <remarks>
    /// While I am not happy about completely replacing this method due to mod compatibility issues, I can't think of
    /// any way to even begin to approach compatibility with another mod that touches this.
    /// </remarks>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ServerMain), "PreFinalizePlayerIdentification")]
    private static bool PreFinalizePlayerIdentification_ReplaceCompletely(Packet_ClientIdentification packet, ConnectedClient client, string entitlements)
    {
        InternalHooks.OnPlayerConnect(packet, client, entitlements);
        return false;
    }
    
    /// <summary>
    /// Intercepts all calls to UpdateQueuedPlayersAfterDisconnect and redirects them to use the configured handler.
    /// The original method contents are skipped entirely and are not executed.
    /// </summary>
    /// <remarks>
    /// While I am not happy about completely replacing this method due to mod compatibility issues, I can't think of
    /// any way to even begin to approach compatibility with another mod that touches this.
    /// </remarks>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ServerMain), "UpdateQueuedPlayersAfterDisconnect")]
    private static bool UpdateQueuedPlayersAfterDisconnect_ReplaceCompletely(ConnectedClient client)
    {
        InternalHooks.OnPlayerDisconnect(client);
        return false;
    }
    
    /// <summary>
    /// Replaces the joined player count calculation with a call to the configured handler.
    /// </summary>
    /// <remarks>
    /// Before:
    ///   <code>this.Clients.Count - this.ConnectionQueue.Count</code>
    ///
    /// After:
    ///   <code>PatchHooks.GetJoinedPlayerCount()</code>
    /// </remarks>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ServerMain), "Process")]
    private static IEnumerable<CodeInstruction> Process_ReplaceJoinedPlayerCountCalculation(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        /*
            IL_003e: ldarg.0      // this
            IL_003f: ldfld        class [VintagestoryAPI]Vintagestory.API.Datastructures.CachingConcurrentDictionary`2<int32, class Vintagestory.Server.ConnectedClient> Vintagestory.Server.ServerMain::Clients
            IL_0044: callvirt     instance int32 class [System.Collections.Concurrent]System.Collections.Concurrent.ConcurrentDictionary`2<int32, class Vintagestory.Server.ConnectedClient>::get_Count()
            IL_0049: ldarg.0      // this
            IL_004a: ldfld        class [System.Collections]System.Collections.Generic.List`1<class Vintagestory.Server.QueuedClient> Vintagestory.Server.ServerMain::ConnectionQueue
            IL_004f: callvirt     instance int32 class [System.Collections]System.Collections.Generic.List`1<class Vintagestory.Server.QueuedClient>::get_Count()
            IL_0054: sub
         */
        
        var matcher = new CodeMatcher(instructions, generator);
        
        matcher.MatchStartForward(
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, typeof(ServerMain).Field(nameof(ServerMain.Clients))),
            new CodeMatch(OpCodes.Callvirt, typeof(ConcurrentDictionary<int, ConnectedClient>).Property(nameof(ConcurrentDictionary<int, ConnectedClient>.Count)).GetGetMethod()),
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, typeof(ServerMain).Field(nameof(ServerMain.ConnectionQueue))),
            new CodeMatch(OpCodes.Callvirt, typeof(List<QueuedClient>).Property(nameof(List<QueuedClient>.Count)).GetGetMethod())
        );
        matcher.ThrowIfNotMatch("Could not rewrite ConnectionQueue usage to use the QueueAPI hooks in ServerMain.Process");
        
        matcher.Repeat(matchAction: match =>
        {
            matcher.RemoveInstructions(7);
            matcher.Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(InternalHooks), nameof(InternalHooks.GetJoinedPlayerCount))));
        });
        
        return matcher.Instructions();
    }
    
    /// <summary>
    /// Injects a call to InternalHooks.OnPlayerJoined when a player actually joins. Being in the queue does not count
    /// as having joined.
    /// </summary>
    /// <remarks>
    /// Before:
    /// <code>
    /// SendPacket(player, CreatePacketIdentification(player.HasPrivilege("controlserver")));
    /// </code>
    ///   
    /// After:
    /// <code>
    /// InternalHooks.OnPlayerJoined(player.PlayerUID);
    /// SendPacket(player, CreatePacketIdentification(player.HasPrivilege("controlserver")));
    /// </code>
    ///
    /// This could maybe be done via the API event, but this will do for now.
    /// </remarks>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ServerMain), "SendServerIdentification")]
    private static IEnumerable<CodeInstruction> SendServerIdentification_CallOnPlayerJoined(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        /*
             // [4462 7 - 4462 117]
             IL_002e: ldarg.0      // this
             IL_002f: ldarg.1      // player
             IL_0030: ldarg.0      // this
             IL_0031: ldarg.1      // player
             IL_0032: ldstr        "controlserver"
             IL_0037: callvirt     instance bool Vintagestory.Server.ServerPlayer::HasPrivilege(string)
             IL_003c: call         instance class Packet_Server Vintagestory.Server.ServerMain::CreatePacketIdentification(bool)
             IL_0041: call         instance void Vintagestory.Server.ServerMain::SendPacket(class [VintagestoryAPI]Vintagestory.API.Server.IServerPlayer, class Packet_Server)
         */
        
        var matcher = new CodeMatcher(instructions, generator);

        matcher.MatchStartForward(
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldarg_1),
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldarg_1),
            new CodeMatch(OpCodes.Ldstr, "controlserver"),
            new CodeMatch(OpCodes.Callvirt, typeof(ServerPlayer).Method(nameof(ServerPlayer.HasPrivilege), [ typeof(string) ])),
            new CodeMatch(OpCodes.Call, typeof(ServerMain).Method("CreatePacketIdentification")),
            new CodeMatch(OpCodes.Call, typeof(ServerMain).Method(nameof(ServerMain.SendPacket), [ typeof(IServerPlayer), typeof(Packet_Server) ]))
        );
        matcher.ThrowIfNotMatch("Could not inject OnPlayerJoined call into ServerMain.SendServerIdentification");

        matcher.Insert(
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Callvirt, typeof(ServerPlayer).Property(nameof(ServerPlayer.PlayerUID)).GetGetMethod()),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(InternalHooks), nameof(InternalHooks.OnPlayerJoined)))
        );

        return matcher.Instructions();
    }
}