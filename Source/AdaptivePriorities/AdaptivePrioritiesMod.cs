using System.Linq;
using HarmonyLib;
using RimWorld;
using AdaptivePriorities.Core;
using UnityEngine;
using Verse;

namespace AdaptivePriorities
{
    public class AdaptivePrioritiesMod : Mod
    {
        public static AdaptivePrioritiesSettings Settings;

        private Vector2 scrollPos;
        private float lastContentHeight = 2000f;

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

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Top bar: advanced toggle + reset-all, outside the scroll area.
            var topBar = new Rect(inRect.x, inRect.y, inRect.width, 28f);
            Widgets.CheckboxLabeled(new Rect(topBar.x, topBar.y, 220f, topBar.height),
                "AP_AdvancedMode".Translate(), ref Settings.advancedMode);
            if (Settings.HasAnyOverride &&
                Widgets.ButtonText(new Rect(topBar.xMax - 180f, topBar.y, 180f, topBar.height), "AP_ResetAll".Translate()))
            {
                Settings.ClearAll();
            }

            var outRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 36f);
            var viewRect = new Rect(0f, 0f, outRect.width - 20f, lastContentHeight);

            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            var listing = new Listing_Standard { maxOneColumn = true };
            listing.Begin(viewRect);
            try
            {
                DrawAutoSection(listing);
                listing.GapLine();
                DrawScoringSection(listing);
                listing.GapLine();
                DrawWorkTypesSection(listing);
            }
            finally
            {
                lastContentHeight = listing.CurHeight;
                listing.End();
                Widgets.EndScrollView();
            }
        }

        private void DrawAutoSection(Listing_Standard l)
        {
            Text.Font = GameFont.Medium;
            l.Label("AP_AutoHeader".Translate());
            Text.Font = GameFont.Small;

            int hours = Mathf.Max(1, Mathf.RoundToInt(Settings.autoRecalcIntervalTicks / (float)GenDate.TicksPerHour));
            l.Label("AP_AutoInterval".Translate(hours), tooltip: "AP_AutoIntervalTip".Translate());
            hours = Mathf.RoundToInt(l.Slider(hours, 1f, 72f));
            Settings.autoRecalcIntervalTicks = hours * GenDate.TicksPerHour;
        }

        private void DrawScoringSection(Listing_Standard l)
        {
            Text.Font = GameFont.Medium;
            l.Label("AP_ScoringHeader".Translate());
            Text.Font = GameFont.Small;

            var d = ScoringTuningDef.Active;
            FloatRow(l, "AP_SkillImportance".Translate(), "skillWeight", d.skillWeight, 0f, 2f);
            FloatRow(l, "AP_MinorPassionBonus".Translate(), "minorPassionBonus", d.minorPassionBonus, 0f, 1f);
            FloatRow(l, "AP_MajorPassionBonus".Translate(), "majorPassionBonus", d.majorPassionBonus, 0f, 1f);
            FloatRow(l, "AP_RewardSpecialists".Translate(), "qualityWeight", d.qualityWeight, 0f, 1f);
            BoolRow(l, "AP_InspirationBoost".Translate(), "inspirationBonusEnabled", d.inspirationBonusEnabled);
            BoolRow(l, "AP_AssignOpposed".Translate(), "assignOpposedWhenNeeded", d.assignOpposedWhenNeeded);

            if (Settings.advancedMode)
            {
                FloatRow(l, "AP_BadPassionPenalty".Translate(), "badPassionPenalty", d.badPassionPenalty, 0f, 1f);
                FloatRow(l, "AP_MaxPassionBonus".Translate(), "maxPassionBonus", d.maxPassionBonus, 0f, 1f);
                FloatRow(l, "AP_UnskilledBaseline".Translate(), "noSkillWorkScore", d.noSkillWorkScore, 0f, 1f);
                FloatRow(l, "AP_CompareToBest".Translate(), "relativeWeight", d.relativeWeight, 0f, 1f);
                FloatRow(l, "AP_UseWorkSpeed".Translate(), "workStatWeight", d.workStatWeight, 0f, 1f);
                FloatRow(l, "AP_OpposedWork".Translate(), "opposedWorkFactor", d.opposedWorkFactor, 0f, 1f);
                FloatRow(l, "AP_InspirationStrength".Translate(), "inspirationBonus", d.inspirationBonus, 0f, 1f);
                FloatRow(l, "AP_InspirationPriority".Translate(), "inspirationUrgency", d.inspirationUrgency, 0f, 1f);
            }
        }

        private void DrawWorkTypesSection(Listing_Standard l)
        {
            Text.Font = GameFont.Medium;
            l.Label("AP_WorkTypesHeader".Translate());
            Text.Font = GameFont.Small;

            var workTypes = WorkTypeDiscoveryService.GetAllWorkTypes()
                .Where(w => w.visible)
                .OrderByDescending(w => w.naturalPriority);

            foreach (var wt in workTypes)
            {
                var def = WorkTypePolicyDef.For(wt);

                l.Gap(10f);
                Text.Font = GameFont.Small;
                var headerRect = l.GetRect(24f);
                Widgets.Label(headerRect.LeftPartPixels(headerRect.width - 90f), wt.labelShort.CapitalizeFirst() ?? wt.defName);
                if (WorkTypeHasOverride(wt) &&
                    Widgets.ButtonText(headerRect.RightPartPixels(85f), "AP_Reset".Translate()))
                {
                    Settings.ClearWorkType(wt);
                }

                FloatRow(l, "AP_Importance".Translate(), AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.UrgencyKey, wt), def.urgency, 0f, 1f, indent: true);
                BoolRow(l, "AP_Everyone".Translate(), AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.AssignEveryoneKey, wt), def.assignEveryone, indent: true);

                if (Settings.advancedMode)
                {
                    IntRow(l, "AP_MinWorkers".Translate(), AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.MinWorkersKey, wt), def.minWorkers, 0, 10, indent: true);
                    FloatRow(l, "AP_SpecialistCutoff".Translate(), AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.ScoreCutoffKey, wt), def.scoreCutoff, 0f, 1f, indent: true);
                    FloatRow(l, "AP_MaxWorkersShare".Translate(), AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.MaxWorkersFractionKey, wt), def.maxWorkersFraction, 0f, 1f, indent: true);
                    BoolRow(l, "AP_AlwaysTop".Translate(), AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.PinPriorityKey, wt), def.pinPriority, indent: true);
                }

                l.GapLine(6f);
            }
        }

        private bool WorkTypeHasOverride(WorkTypeDef wt)
        {
            return Settings.IsOverridden(AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.UrgencyKey, wt))
                   || Settings.IsOverridden(AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.AssignEveryoneKey, wt))
                   || Settings.IsOverridden(AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.MinWorkersKey, wt))
                   || Settings.IsOverridden(AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.ScoreCutoffKey, wt))
                   || Settings.IsOverridden(AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.MaxWorkersFractionKey, wt))
                   || Settings.IsOverridden(AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.PinPriorityKey, wt));
        }

        private void FloatRow(Listing_Standard l, string label, string key, float def, float min, float max, bool indent = false)
        {
            float cur = Settings.GetFloat(key, def);
            string mark = Settings.IsOverridden(key) ? " *" : "";
            string prefix = indent ? "    " : "";
            l.Label($"{prefix}{label}: {cur:0.00}{mark}");
            float v = l.Slider(cur, min, max);
            if (!Mathf.Approximately(v, cur))
                Settings.SetFloat(key, v, def);
        }

        private void IntRow(Listing_Standard l, string label, string key, int def, int min, int max, bool indent = false)
        {
            int cur = Settings.GetInt(key, def);
            string mark = Settings.IsOverridden(key) ? " *" : "";
            string prefix = indent ? "    " : "";
            l.Label($"{prefix}{label}: {cur}{mark}");
            int v = Mathf.RoundToInt(l.Slider(cur, min, max));
            if (v != cur)
                Settings.SetInt(key, v, def);
        }

        private void BoolRow(Listing_Standard l, string label, string key, bool def, bool indent = false)
        {
            bool cur = Settings.GetBool(key, def);
            string mark = Settings.IsOverridden(key) ? " *" : "";
            string prefix = indent ? "    " : "";
            bool v = cur;
            l.CheckboxLabeled($"{prefix}{label}{mark}", ref v);
            if (v != cur)
                Settings.SetBool(key, v, def);
        }
    }
}
