using System.Collections.Generic;
using System.Linq;
using AdaptivePriorities.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.UI.Settings
{
    /// <summary>
    /// External Workers tab: the mech/drone category toggles, then one row per work type with an offload
    /// mode (Off/Reduce/Full) and a backup floor, plus an advanced uptime factor. Each row shows its live
    /// state - greyed with a reason when it can't apply, and a tooltip readout of the current map's
    /// capacity - so the tab tells the truth about what will actually happen.
    /// </summary>
    public static class ExternalWorkersTab
    {
        private const float ModeWidth = 92f;
        private const float BackupWidth = 96f;
        private const float StepButton = 20f;
        private const float SubIndent = 32f;

        private static Vector2 scrollPos;
        private static float lastHeight = 1000f;

        // Capacity for the currently loaded map, rebuilt each frame the tab is open so the readouts track
        // live automatons and the category toggles. Null off the map.
        private static Dictionary<WorkTypeDef, float> capacityPreview;

        public static void Draw(Rect rect)
        {
            capacityPreview = Current.ProgramState == ProgramState.Playing && Find.CurrentMap != null
                ? ExternalWorkerService.BuildCapacity(Find.CurrentMap)
                : null;

            var categories = Categories();
            bool anyCategoryOn = categories.Any(c => ScoringConfig.AccountForCategory(c));
            bool advanced = AdaptivePrioritiesMod.Settings.advancedMode;

            var viewRect = new Rect(0f, 0f, rect.width - 20f, lastHeight);
            Widgets.BeginScrollView(rect, ref scrollPos, viewRect);
            var listing = new Listing_Standard { maxOneColumn = true };
            listing.Begin(viewRect);
            try
            {
                listing.Label("AP_ExtIntro".Translate());
                listing.GapLine(6f);

                foreach (var category in categories)
                    DrawCategoryToggle(listing.GetRect(SettingsWidgets.RowHeight), category);

                listing.GapLine(6f);
                DrawColumnHeader(listing.GetRect(22f));

                var workTypes = WorkTypeDiscoveryService.GetAllWorkTypes()
                    .Where(w => w.visible)
                    .OrderByDescending(w => w.naturalPriority);
                foreach (var workType in workTypes)
                    DrawWorkType(listing, workType, anyCategoryOn, advanced);
            }
            finally
            {
                lastHeight = listing.CurHeight;
                listing.End();
                Widgets.EndScrollView();
            }
        }

        /// <summary>Mechs first (the built-in generic path), then any category a source def declares.</summary>
        private static List<string> Categories()
        {
            var list = new List<string> { ExternalWorkerService.MechCategory };
            foreach (var category in ExternalWorkerSourceDef.Categories)
                if (!list.Contains(category))
                    list.Add(category);
            return list;
        }

        private static void DrawCategoryToggle(Rect rect, string category)
        {
            SettingsWidgets.BoolRow(rect, CategoryLabel(category),
                AdaptivePrioritiesSettings.AccountKey(category), defaultValue: true,
                SettingsWidgets.Tip(() => CategoryTip(category), () => "On".Translate()));
        }

        private static void DrawColumnHeader(Rect rect)
        {
            SplitRow(rect, out var nameRect, out var modeRect, out var backupRect, out _);
            SettingsWidgets.DrawHeaderLabel(nameRect, "AP_ColWorkType".Translate(), TextAnchor.MiddleLeft);
            SettingsWidgets.DrawHeaderLabel(modeRect, "AP_ExtOffload".Translate(), TextAnchor.MiddleCenter);
            SettingsWidgets.DrawHeaderLabel(backupRect, "AP_ExtBackup".Translate(), TextAnchor.MiddleCenter);

            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.3f);
            Widgets.DrawLineHorizontal(rect.x, rect.yMax + 1f, rect.width);
            GUI.color = prev;
        }

        private static void DrawWorkType(Listing_Standard listing, WorkTypeDef workType, bool anyCategoryOn, bool advanced)
        {
            var settings = AdaptivePrioritiesMod.Settings;
            var def = WorkTypePolicyDef.For(workType);

            string modeKey = AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.ExternalOffloadKey, workType);
            string backupKey = AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.ExternalBackupKey, workType);
            string uptimeKey = AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.ExternalUptimeKey, workType);

            var mode = (ExternalOffloadMode)settings.GetInt(modeKey, (int)def.externalOffload);

            var rect = listing.GetRect(SettingsWidgets.RowHeight);
            Widgets.DrawHighlightIfMouseover(rect);
            SplitRow(rect, out var nameRect, out var modeRect, out var backupRect, out var revertRect);

            // Name + live readout tooltip.
            Text.Anchor = TextAnchor.MiddleLeft;
            var prev = GUI.color;
            if (!anyCategoryOn)
                GUI.color = prev * new Color(1f, 1f, 1f, 0.4f);
            Widgets.Label(nameRect, workType.labelShort.CapitalizeFirst() ?? workType.defName);
            GUI.color = prev;
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(nameRect, new TipSignal(() => ReadoutLine(workType, mode, anyCategoryOn), modeKey.GetHashCode()));

            // Offload mode: a button opening a three-way menu. Inert (greyed label) when no category is on.
            if (anyCategoryOn)
            {
                var btn = new Rect(modeRect.x, modeRect.y + 3f, modeRect.width, modeRect.height - 6f);
                if (Widgets.ButtonText(btn, ModeLabel(mode)))
                    OpenModeMenu(modeKey, (int)def.externalOffload);
                TooltipHandler.TipRegion(modeRect, new TipSignal(() => "AP_ExtOffloadTip".Translate(), modeKey.GetHashCode() ^ 1));
            }
            else
            {
                SettingsWidgets.DrawValue(modeRect, ModeLabel(mode), disabled: true);
            }

            // Backup floor: only used under Reduce (Off ignores automatons, Full drops to zero).
            bool backupActive = anyCategoryOn && mode == ExternalOffloadMode.Reduce;
            DrawBackupStepper(backupRect, backupKey, def.externalBackup, backupActive,
                () => mode == ExternalOffloadMode.Reduce ? null : (string)"AP_ExtBackupIgnored".Translate());

            // Row-level revert clears all three keys of this work type.
            if (settings.IsOverridden(modeKey) || settings.IsOverridden(backupKey) || settings.IsOverridden(uptimeKey))
                SettingsWidgets.DrawRevert(revertRect, "AP_RevertWorkTypeTip".Translate(), () =>
                {
                    settings.ClearKey(modeKey);
                    settings.ClearKey(backupKey);
                    settings.ClearKey(uptimeKey);
                });

            // Advanced: the uptime factor as an indented full-width row.
            if (advanced)
            {
                bool uptimeActive = anyCategoryOn && mode != ExternalOffloadMode.Off;
                var uptimeRect = listing.GetRect(SettingsWidgets.RowHeight);
                uptimeRect.xMin += SubIndent;
                SettingsWidgets.FloatRow(uptimeRect, "AP_ExtUptime".Translate(), uptimeKey,
                    def.externalUptimeFactor, 0.5f, 3f,
                    SettingsWidgets.Tip(
                        () => "AP_ExtUptimeTip".Translate(),
                        () => def.externalUptimeFactor.ToString("0.##"),
                        disabledReason: () => uptimeActive ? null : (string)"AP_ExtUptimeIgnored".Translate()),
                    disabled: !uptimeActive, labelWidth: SettingsWidgets.LabelWidth - SubIndent, roundTo: 0.1f);
                listing.Gap(2f);
            }
        }

        private static void DrawBackupStepper(Rect rect, string key, int defaultValue, bool active, System.Func<string> disabledReason)
        {
            var settings = AdaptivePrioritiesMod.Settings;
            int cur = settings.GetInt(key, defaultValue);

            var prev = GUI.color;
            if (!active)
                GUI.color = prev * new Color(1f, 1f, 1f, 0.4f);

            var minus = new Rect(rect.x, rect.y + (rect.height - StepButton) / 2f, StepButton, StepButton);
            var plus = new Rect(rect.xMax - StepButton, rect.y + (rect.height - StepButton) / 2f, StepButton, StepButton);
            var valueRect = new Rect(minus.xMax, rect.y, plus.x - minus.xMax, rect.height);

            if (active)
            {
                if (Widgets.ButtonText(minus, "-") && cur > 0)
                    settings.SetInt(key, cur - 1, defaultValue);
                if (Widgets.ButtonText(plus, "+") && cur < 10)
                    settings.SetInt(key, cur + 1, defaultValue);
            }
            else
            {
                Widgets.DrawAtlas(minus, Widgets.ButtonBGAtlas);
                Widgets.DrawAtlas(plus, Widgets.ButtonBGAtlas);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(minus, "-");
                Widgets.Label(plus, "+");
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(valueRect, cur.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = prev;

            TooltipHandler.TipRegion(rect, new TipSignal(
                SettingsWidgets.Tip(() => "AP_ExtBackupTip".Translate(), () => defaultValue.ToString(),
                    disabledReason: disabledReason),
                key.GetHashCode()));
        }

        private static void OpenModeMenu(string key, int defaultValue)
        {
            var options = new List<FloatMenuOption>();
            foreach (ExternalOffloadMode m in System.Enum.GetValues(typeof(ExternalOffloadMode)))
            {
                var chosen = m;
                options.Add(new FloatMenuOption(ModeLabel(m),
                    () => AdaptivePrioritiesMod.Settings.SetInt(key, (int)chosen, defaultValue)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>Live "here is the capacity and what it does" line for the row tooltip.</summary>
        private static string ReadoutLine(WorkTypeDef workType, ExternalOffloadMode mode, bool anyCategoryOn)
        {
            if (!anyCategoryOn)
                return "AP_ExtNoCategories".Translate();
            if (mode == ExternalOffloadMode.Off)
                return "AP_ExtModeOffNote".Translate();

            float cap = 0f;
            capacityPreview?.TryGetValue(workType, out cap);
            if (cap <= 0f)
                return "AP_ExtLiveNoCapacity".Translate();

            var policy = WorkPolicyConfig.For(workType);
            int reduction = Mathf.FloorToInt(cap * Mathf.Max(0f, policy.externalUptimeFactor));
            int floor = mode == ExternalOffloadMode.Full ? 0 : Mathf.Max(0, policy.externalBackup);
            return "AP_ExtLiveCapacity".Translate(cap.ToString("0.0"),
                policy.externalUptimeFactor.ToString("0.##"), reduction, floor);
        }

        private static void SplitRow(Rect rect, out Rect name, out Rect mode, out Rect backup, out Rect revert)
        {
            revert = new Rect(rect.xMax - SettingsWidgets.RevertSize,
                rect.y + (rect.height - SettingsWidgets.RevertSize) / 2f, SettingsWidgets.RevertSize, SettingsWidgets.RevertSize);
            backup = new Rect(revert.x - SettingsWidgets.Pad - BackupWidth, rect.y, BackupWidth, rect.height);
            mode = new Rect(backup.x - SettingsWidgets.Pad - ModeWidth, rect.y, ModeWidth, rect.height);
            name = new Rect(rect.x, rect.y, mode.x - SettingsWidgets.Pad - rect.x, rect.height);
        }

        private static string ModeLabel(ExternalOffloadMode mode) => ("AP_ExtMode" + mode).Translate();

        private static string CategoryLabel(string category)
        {
            switch (category)
            {
                case ExternalWorkerService.MechCategory: return "AP_AccountForMechs".Translate();
                case "Drones": return "AP_AccountForDrones".Translate();
                default: return "AP_AccountForGeneric".Translate(category);
            }
        }

        private static string CategoryTip(string category)
        {
            switch (category)
            {
                case ExternalWorkerService.MechCategory: return "AP_AccountForMechsTip".Translate();
                case "Drones": return "AP_AccountForDronesTip".Translate();
                default: return "AP_AccountForGenericTip".Translate(category);
            }
        }
    }
}
