using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace QueueAPI;

public class QueueAPIModSystem : ModSystem
{
    private HarmonyLib.Harmony? _harmony;
    private ICoreServerAPI _api = null!; // Will be initialised in StartServerSide.

    /// <summary>
    /// The current queue handler. 
    /// Setting this to a new handler will reset the queue and cause all queuing players to be kicked. As such, this should probably only be done during server initialisation.
    /// </summary>
    public IQueueAPIEventHandler Handler
    {
        get => InternalHooks.Handler;
        set => InternalHooks.Handler = value;
    }

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        _api = api;
        InternalHooks.DetectMainServerThread(); // There may very well be a better way to do this already in the game.

        _harmony = new HarmonyLib.Harmony("queueapi");
        _harmony.PatchAll();
    }

    public override void Dispose()
    {
        _harmony?.UnpatchAll("queueapi");
        _harmony = null;
    }
}