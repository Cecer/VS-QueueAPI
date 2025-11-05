using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mono.Cecil;
using Vintagestory.Server;

namespace QueueAPI.Harmony.Patches;

[HarmonyPatch]
public static class CmdGlobalListPatches
{
    /// <summary>
    /// Replaces the player queue position calculation with a call to the configured handler.
    /// </summary>
    /// <remarks>
    /// Before:
    ///   <code>this.server.ConnectionQueue.FindIndex(c => c.Client.Id == client.Id)</code>
    ///
    /// After:
    ///   <code>PatchHooks.GetClientQueueIndex(client.Id)</code>
    /// </remarks>
    [HarmonyTranspiler]
    [HarmonyPatch("Vintagestory.Server.CmdGlobalList", "listClients")]
    private static IEnumerable<CodeInstruction> listClients_ReplaceQueuePositionCalculation(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        /*
            Because the code we want to patch calls a lambda, the patch is more complicated than it would otherwise be as we check the lambda body too.
            
            FindIndex call:
            IL_0087: ldarg.0      // this
            IL_0088: ldfld        class Vintagestory.Server.ServerMain Vintagestory.Server.CmdGlobalList::server
            IL_008d: ldfld        class [System.Collections]System.Collections.Generic.List`1<class Vintagestory.Server.QueuedClient> Vintagestory.Server.ServerMain::ConnectionQueue
            IL_0092: ldloc.2      // V_2
            IL_0093: ldftn        instance bool Vintagestory.Server.CmdGlobalList/'<>c__DisplayClass2_0'::'<listClients>b__0'(class Vintagestory.Server.QueuedClient)
            IL_0099: newobj       instance void class [System.Runtime]System.Predicate`1<class Vintagestory.Server.QueuedClient>::.ctor(object, native int)
            IL_009e: callvirt     instance int32 class [System.Collections]System.Collections.Generic.List`1<class Vintagestory.Server.QueuedClient>::FindIndex(class [System.Runtime]System.Predicate`1<!0/*class Vintagestory.Server.QueuedClient* />)
            
            Lambda body:
            IL_0000: ldarg.1      // c
            IL_0001: ldfld        class Vintagestory.Server.ConnectedClient Vintagestory.Server.QueuedClient::Client
            IL_0006: ldfld        int32 Vintagestory.Server.ConnectedClient::Id
            IL_000b: ldarg.0      // this
            IL_000c: ldfld        class Vintagestory.Server.ConnectedClient Vintagestory.Server.CmdGlobalList/'<>c__DisplayClass2_0'::client
            IL_0011: ldfld        int32 Vintagestory.Server.ConnectedClient::Id
            IL_0016: ceq
            IL_0018: ret
            
            How can I filter my CodeMatch rule to match only Ldftn when the references method matches?
         */

        var matcher = new CodeMatcher(instructions, generator);

        matcher.MatchStartForward(
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, AccessTools.TypeByName("Vintagestory.Server.CmdGlobalList").Field("server")), // Type is internal, field is private
            new CodeMatch(OpCodes.Ldfld, typeof(ServerMain).Field(nameof(ServerMain.ConnectionQueue))),
            CodeMatch.LoadsLocal(),
            new CodeMatch(instr =>
            {
                if (instr.opcode != OpCodes.Ldftn) return false;

                var method = (MethodInfo) instr.operand;
                if (method.ReturnType != typeof(bool)) return false;

                var lambdaType = method.DeclaringType;

                var methodParams = method.GetParameters();
                if (methodParams.Length != 1) return false;
                if (methodParams[0].ParameterType != typeof(QueuedClient)) return false;

                var module = ModuleDefinition.ReadModule(method.Module.FullyQualifiedName);
                var def = module.GetType(method.DeclaringType?.FullName.Replace("+", "/"))?.Methods.FirstOrDefault(m => m.Name == method.Name);

                if (def?.Body == null) return false;

                var instrs = def.Body.Instructions.ToArray();
                if (instrs[0].OpCode != Mono.Cecil.Cil.OpCodes.Ldarg_1) return false;

                if (instrs[1].OpCode != Mono.Cecil.Cil.OpCodes.Ldfld) return false;
                if (!((FieldReference)instrs[1].Operand).Matches(typeof(QueuedClient).Field(nameof(QueuedClient.Client)))) return false;

                if (instrs[2].OpCode != Mono.Cecil.Cil.OpCodes.Ldfld) return false;
                if (!((FieldReference)instrs[2].Operand).Matches(typeof(ConnectedClient).Field(nameof(ConnectedClient.Id)))) return false;

                if (instrs[3].OpCode != Mono.Cecil.Cil.OpCodes.Ldarg_0) return false;

                if (instrs[4].OpCode != Mono.Cecil.Cil.OpCodes.Ldfld) return false;

                if (!((FieldReference)instrs[4].Operand).Matches(lambdaType.Field("client"))) return false;

                if (instrs[5].OpCode != Mono.Cecil.Cil.OpCodes.Ldfld) return false;
                if (!((FieldReference)instrs[5].Operand).Matches(typeof(ConnectedClient).Field(nameof(ConnectedClient.Id)))) return false;

                if (instrs[6].OpCode != Mono.Cecil.Cil.OpCodes.Ceq) return false;

                if (instrs[7].OpCode != Mono.Cecil.Cil.OpCodes.Ret) return false;

                return true;
            }),
            new CodeMatch(OpCodes.Newobj, typeof(Predicate<QueuedClient>).Constructor([typeof(object), typeof(IntPtr)])),
            new CodeMatch(OpCodes.Callvirt, typeof(List<QueuedClient>).Method(nameof(List<QueuedClient>.FindIndex), [typeof(Predicate<QueuedClient>)]))
        );
        matcher.ThrowIfNotMatch("Could not rewrite ConnectionQueue usage to use the QueueAPI hooks in CmdGlobalList.listClients");

        matcher.Repeat(matchAction: match =>
        {
            matcher.RemoveInstructions(3); // Skip the loading of ConnectionQueue onto the stack

            matcher.Advance(1); // Preserve the loading of the lambda onto the stack

            var lambdaType = ((MethodInfo)matcher.Operand).DeclaringType; // Get the lambda type from the ldftn instruction. We'll use it to access the client field.

            matcher.RemoveInstructions(3); // Skip the remainder of the match

            matcher.Insert(
                new CodeInstruction(OpCodes.Ldfld, lambdaType.Field("client")),
                new CodeInstruction(OpCodes.Ldfld, typeof(ConnectedClient).Field(nameof(ConnectedClient.Id))),
                new CodeInstruction(OpCodes.Call, typeof(InternalHooks).Method(nameof(InternalHooks.GetClientPosition)))
            );
        });

        return matcher.Instructions();
    }

    /// <summary>
    /// Replaces the player queue position access with a call to the configured handler.
    /// </summary>
    /// <remarks>
    /// Before:
    ///   <code>this.server.ConnectionQueue[index]</code>
    ///
    /// After:
    ///   <code>PatchHooks.GetClientAtQueuePosition(index + 1)</code>
    /// </remarks>
    [HarmonyTranspiler]
    [HarmonyPatch("Vintagestory.Server.CmdGlobalList", "listClients")]
    private static IEnumerable<CodeInstruction> listClients_ReplaceQueuePositionAccess(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        /*
            IL_00a8: ldarg.0      // this
            IL_00a9: ldfld        class Vintagestory.Server.ServerMain Vintagestory.Server.CmdGlobalList::server
            IL_00ae: ldfld        class [System.Collections]System.Collections.Generic.List`1<class Vintagestory.Server.QueuedClient> Vintagestory.Server.ServerMain::ConnectionQueue
            IL_00b3: ldloc.3      // index
            IL_00b4: callvirt     instance !0/*class Vintagestory.Server.QueuedClient* / class [System.Collections]System.Collections.Generic.List`1<class Vintagestory.Server.QueuedClient>::get_Item(int32)
         */

        var matcher = new CodeMatcher(instructions, generator);

        matcher.MatchStartForward(
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, AccessTools.TypeByName("Vintagestory.Server.CmdGlobalList").Field("server")), // Type is internal, field is private
            new CodeMatch(OpCodes.Ldfld, typeof(ServerMain).Field(nameof(ServerMain.ConnectionQueue))),
            CodeMatch.LoadsLocal(),
            new CodeMatch(OpCodes.Callvirt, typeof(List<QueuedClient>).Indexer([ typeof(int) ]).GetMethod)
        );
        matcher.ThrowIfNotMatch("Could not rewrite ConnectionQueue usage to use the QueueAPI hooks in CmdGlobalList.listClients");

        matcher.Repeat(matchAction: match =>
        {
            matcher.RemoveInstructions(3); // Skip the loading of ConnectionQueue onto the stack

            matcher.Advance(1); // Preserve the loading of the index onto the stack

            matcher.RemoveInstructions(1); // Skip indexer access

            matcher.Insert(
                new CodeInstruction(OpCodes.Ldc_I4_1),
                new CodeInstruction(OpCodes.Add),
                new CodeInstruction(OpCodes.Call, typeof(InternalHooks).Method(nameof(InternalHooks.GetClientAtPosition))));
        });

        return matcher.Instructions();
    }
}