using System;
using HarmonyLib;
using Vintagestory.Server;

namespace QueueAPI.Harmony.Accessors;

[HarmonyPatch]
public static class ServerMainAccessor
{
    [HarmonyPriority(Priority.Last)]
    [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
    [HarmonyPatch(typeof(ServerMain), "FinalizePlayerIdentification")]
    private static void FinalizePlayerIdentification_Accessor(ServerMain instance, Packet_ClientIdentification packet, ConnectedClient client, string entitlements) => throw new Exception("Unreachable code! Did Harmony not apply the reserve patch?");

    /// <summary>
    /// Exposes access to ServerMain.FinalizePlayerIdentification method. 
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="packet"></param>
    /// <param name="client"></param>
    /// <param name="entitlements"></param>
    public static void FinalizePlayerIdentification(this ServerMain instance, Packet_ClientIdentification packet, ConnectedClient client, string entitlements) => FinalizePlayerIdentification_Accessor(instance, packet, client, entitlements);
}