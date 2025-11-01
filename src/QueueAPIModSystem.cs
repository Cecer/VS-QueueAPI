using QueueAPI.Handlers;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

[assembly: ModInfo("Queue API", "queueapi",
    Authors = ["Cecer"],
    Description = "Provides an API for other mods to interact with the player join queue.",
    Version = "1.0.0",
    RequiredOnServer = true,
    RequiredOnClient = false)]

namespace QueueAPI;

public class QueueAPIModSystem : ModSystem
{
    private HarmonyLib.Harmony? _harmony;
    private ICoreServerAPI _api;

    /// <summary>
    /// The current queue handler. 
    /// Setting this to a new handler will reset the queue and cause all queuing players to be kicked. As such, this should probably only be done during server initialisation.
    /// </summary>
    public AbstractHandler AbstractHandler
    {
        get => InternalHooks.AbstractHandler;
        set => InternalHooks.AbstractHandler = value;
    }

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        _api = api;

        _harmony = new HarmonyLib.Harmony("queueapi");
        _harmony.PatchAll();
    }

    public override void Dispose()
    {
        if (!AbstractHandler.Queue.IsQueueEmpty)
        {
            _api.Logger.Warning($"The Queue API is being disposed but the queue was not empty. Resetting the queue state! All players in the old queue handler be kicked.");
            AbstractHandler.Queue.Reset();
        }
        _harmony?.UnpatchAll("queueapi");
        _harmony = null;
    }
}