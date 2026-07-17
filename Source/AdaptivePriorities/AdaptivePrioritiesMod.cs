using HarmonyLib;
using UnityEngine;
using Verse;

namespace AdaptivePriorities
{
    public class AdaptivePrioritiesMod : Mod
    {
        public static AdaptivePrioritiesSettings Settings;

        public AdaptivePrioritiesMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<AdaptivePrioritiesSettings>();
            var harmony = new Harmony("encoded.adaptivepriorities");
            harmony.PatchAll();
            // Grid UI hooks are applied manually (not via attributes) so they can patch every runtime
            // subclass of the vanilla work columns/window, covering Fluffy's Work Tab without a hard
            // reference.
            Patches.WorkGridPatches.Apply(harmony);
        }

        public override string SettingsCategory() => "Adaptive Priorities";

        public override void DoSettingsWindowContents(Rect inRect) => UI.Settings.SettingsWindowContents.Draw(inRect);
    }
}
