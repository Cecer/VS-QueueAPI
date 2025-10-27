using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.Server;

namespace QueueAPI.Harmony.Patches;

    
[HarmonyPatch]
public static class CmdStatsPatches
{
    /// <summary>
    /// Replaces the player queue size lookup with a call to the configured handler.
    /// </summary>
    /// <remarks>
    /// Before:
    ///   <code>this.server.ConnectionQueue.Count</code>
    ///
    /// After:
    ///   <code>PatchHooks.GetQueueSize()</code>
    /// </remarks>
    [HarmonyTranspiler]
    [HarmonyPatch("Vintagestory.Server.CmdStats", "genStats")]
    private static IEnumerable<CodeInstruction> genStats_ReplaceQueueSizeAccess(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        /*
            IL_0228: ldarg.0      // server
            IL_0229: ldfld        class [System.Collections]System.Collections.Generic.List`1<class Vintagestory.Server.QueuedClient> Vintagestory.Server.ServerMain::ConnectionQueue
            IL_022e: callvirt     instance int32 class [System.Collections]System.Collections.Generic.List`1<class Vintagestory.Server.QueuedClient>::get_Count()
         */
        
        var matcher = new CodeMatcher(instructions, generator);

        matcher.MatchStartForward(
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, typeof(ServerMain).Field(nameof(ServerMain.ConnectionQueue))),
            new CodeMatch(OpCodes.Callvirt, typeof(List<QueuedClient>).Property(nameof(List<QueuedClient>.Count)).GetGetMethod())
        );
        matcher.ThrowIfNotMatch("Could not rewrite ConnectionQueue usage to use the QueueAPI hooks in CmdStats.genStats");
        
        matcher.Repeat(matchAction: match =>
        {
            matcher.RemoveInstructions(3);
            matcher.Insert(new CodeInstruction(OpCodes.Call, typeof(InternalHooks).Method(nameof(InternalHooks.GetQueueSize))));
        });
        
        return matcher.Instructions();
    }
}