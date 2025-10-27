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
    
    public IJoinQueueHandler Handler
    {
        get => InternalHooks.Handler;
        set => InternalHooks.Handler = value;
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
        if (Handler.QueueSize > 0)
        {
            _api.Logger.Warning($"The queue API is being disposed but the queue was not empty. Resetting the queue state! All players in the old queue handler be kicked.");
            Handler.Reset();
        }
        _harmony?.UnpatchAll("queueapi");
        _harmony = null;
    }
}