using HarmonyLib;
using UnityEngine;
using Verse;

namespace AdaptivePriorities
{
    public class AdaptivePrioritiesMod : Mod
    {
        public static AdaptivePrioritiesSettings Settings;

        // Shared instance so the deferred grid-UI hooks can patch with the same id.
        public static Harmony HarmonyInstance;

        public AdaptivePrioritiesMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<AdaptivePrioritiesSettings>();
            HarmonyInstance = new Harmony("encoded.adaptivepriorities");
            HarmonyInstance.PatchAll();
            // Grid UI hooks are applied manually (not via attributes) so they can patch every runtime
            // subclass of the vanilla work columns/window, covering Fluffy's Work Tab without a hard
            // reference. They are NOT applied here: mod constructors run on a worker thread, and
            // patching a subclass forces its static constructor to run on that thread. Foreign columns
            // (e.g. AI Robots' X2_PawnColumnWorker_IsInRecharge) load textures in theirs, which Unity
            // rejects off the main thread and which leaves them with null textures. Applied instead
            // from WorkGridPatchLoader, a [StaticConstructorOnStartup] class -> main thread, after
            // content load.
        }

        public override string SettingsCategory() => "Adaptive Priorities";

        public override void DoSettingsWindowContents(Rect inRect) => UI.Settings.SettingsWindowContents.Draw(inRect);
    }
}
