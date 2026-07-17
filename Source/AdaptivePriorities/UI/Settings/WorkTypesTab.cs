using System.Collections.Generic;
using System.Linq;
using AdaptivePriorities.Core;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.UI.Settings
{
    /// <summary>
    /// Work types tab: a sticky column header, then one row per visible work type (ordered by
    /// naturalPriority descending — the column order the player knows from the Work tab). With
    /// advanced mode on, a row expands to the specialist knobs; knobs the assigner ignores in the
    /// current configuration are greyed with the reason in their tooltip, so the UI tells the truth
    /// about the formula. The row-level revert clears the whole work type.
    /// </summary>
    public static class WorkTypesTab
    {
        private const float CaretWidth = 24f;
        private const float NameWidth = 170f;
        private const float EveryoneWidth = 90f;
        private const float SubIndent = 32f;

        private static Vector2 scrollPos;
        private static float lastHeight = 1000f;
        private static readonly HashSet<WorkTypeDef> expanded = new HashSet<WorkTypeDef>();

        public static void Draw(Rect rect)
        {
            // Header geometry matches the rows inside the scroll view, whose width is 20px narrower
            // (scrollbar gutter).
            var headerRect = new Rect(rect.x, rect.y, rect.width - 20f, 24f);
            DrawColumnHeader(headerRect);

            var scrollRect = new Rect(rect.x, headerRect.yMax + 4f, rect.width, rect.height - headerRect.height - 4f);
            var viewRect = new Rect(0f, 0f, scrollRect.width - 20f, lastHeight);
            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);
            var listing = new Listing_Standard { maxOneColumn = true };
            listing.Begin(viewRect);
            try
            {
                bool advanced = AdaptivePrioritiesMod.Settings.advancedMode;
                var workTypes = WorkTypeDiscoveryService.GetAllWorkTypes()
                    .Where(w => w.visible)
                    .OrderByDescending(w => w.naturalPriority);
                foreach (var workType in workTypes)
                    DrawWorkType(listing, workType, advanced);
            }
            finally
            {
                lastHeight = listing.CurHeight;
                listing.End();
                Widgets.EndScrollView();
            }
        }

        private static void DrawColumnHeader(Rect rect)
        {
            SplitMain(rect, out _, out var name, out var slider, out var value, out var everyone, out _);
            SettingsWidgets.DrawHeaderLabel(name, "AP_ColWorkType".Translate(), TextAnchor.MiddleLeft);
            var importanceRect = new Rect(slider.x, rect.y, value.xMax - slider.x, rect.height);
            SettingsWidgets.DrawHeaderLabel(importanceRect, "AP_Importance".Translate(), TextAnchor.MiddleCenter);
            SettingsWidgets.DrawHeaderLabel(everyone, "AP_ColEveryone".Translate(), TextAnchor.MiddleCenter);

            var prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.3f);
            Widgets.DrawLineHorizontal(rect.x, rect.yMax + 1f, rect.width);
            GUI.color = prevColor;
        }

        private static void SplitMain(Rect rect, out Rect caret, out Rect name, out Rect slider, out Rect value, out Rect everyone, out Rect revert)
        {
            caret = new Rect(rect.x, rect.y + (rect.height - CaretWidth) / 2f, CaretWidth, CaretWidth);
            name = new Rect(caret.xMax + 4f, rect.y, NameWidth, rect.height);
            revert = new Rect(rect.xMax - SettingsWidgets.RevertSize,
                rect.y + (rect.height - SettingsWidgets.RevertSize) / 2f,
                SettingsWidgets.RevertSize, SettingsWidgets.RevertSize);
            everyone = new Rect(revert.x - SettingsWidgets.Pad - EveryoneWidth, rect.y, EveryoneWidth, rect.height);
            value = new Rect(everyone.x - SettingsWidgets.Pad - SettingsWidgets.ValueWidth, rect.y, SettingsWidgets.ValueWidth, rect.height);
            slider = new Rect(name.xMax + SettingsWidgets.Pad, rect.y,
                value.x - name.xMax - 2f * SettingsWidgets.Pad, rect.height);
        }

        private static void DrawWorkType(Listing_Standard listing, WorkTypeDef workType, bool advanced)
        {
            var def = WorkTypePolicyDef.For(workType);
            var settings = AdaptivePrioritiesMod.Settings;
            var rect = listing.GetRect(SettingsWidgets.RowHeight);
            SplitMain(rect, out var caretRect, out var nameRect, out var sliderRect, out var valueRect, out var everyoneRect, out var revertRect);
            Widgets.DrawHighlightIfMouseover(rect);

            // The expand caret only exists in advanced mode: with it off there are no advanced knobs,
            // so the tab stays a flat Importance+Everyone table with no dead affordance.
            bool isExpanded = advanced && expanded.Contains(workType);
            if (advanced && Widgets.ButtonImage(caretRect, isExpanded ? TexButton.Collapse : TexButton.Reveal))
            {
                if (isExpanded)
                    expanded.Remove(workType);
                else
                    expanded.Add(workType);
                isExpanded = !isExpanded;
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(nameRect, workType.labelShort.CapitalizeFirst() ?? workType.defName);
            Text.Anchor = TextAnchor.UpperLeft;
            if (!workType.description.NullOrEmpty())
                TooltipHandler.TipRegion(nameRect, workType.description);

            // Importance slider + readout.
            string urgencyKey = AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.UrgencyKey, workType);
            float urgency = settings.GetFloat(urgencyKey, def.urgency);
            SettingsWidgets.DrawValue(valueRect, urgency.ToString("0.00"));
            // Rounding happens only on an actual change; rounding the current value every frame would
            // turn an off-grid derived default into a phantom override.
            float rawUrgency = SettingsWidgets.Slider(sliderRect, urgencyKey, urgency, 0f, 1f);
            if (!Mathf.Approximately(rawUrgency, urgency))
                settings.SetFloat(urgencyKey, Mathf.Round(rawUrgency * 100f) / 100f, def.urgency);
            var importanceTipRect = new Rect(sliderRect.x, rect.y, valueRect.xMax - sliderRect.x, rect.height);
            TooltipHandler.TipRegion(importanceTipRect, new TipSignal(
                SettingsWidgets.Tip(
                    () => "AP_ImportanceTip".Translate(),
                    () => def.urgency.ToString("0.00"),
                    () => "AP_LiveUrgency".Translate(
                        PriorityRangeService.FromNormalized(settings.GetFloat(urgencyKey, def.urgency)),
                        PriorityRangeService.LowestPriority)),
                urgencyKey.GetHashCode()));

            // Everyone checkbox, centered in its column.
            string everyoneKey = AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.AssignEveryoneKey, workType);
            bool everyoneOn = settings.GetBool(everyoneKey, def.assignEveryone);
            bool everyoneNew = everyoneOn;
            Widgets.Checkbox(new Vector2(everyoneRect.center.x - 12f, rect.y + (rect.height - 24f) / 2f), ref everyoneNew, 24f);
            if (everyoneNew != everyoneOn)
            {
                settings.SetBool(everyoneKey, everyoneNew, def.assignEveryone);
                everyoneOn = everyoneNew;
            }
            TooltipHandler.TipRegion(everyoneRect, new TipSignal(
                SettingsWidgets.Tip(
                    () => "AP_EveryoneTip".Translate(),
                    () => (def.assignEveryone ? "On" : "Off").Translate()),
                everyoneKey.GetHashCode()));

            // Row-level revert: clears every override of this work type.
            if (settings.HasWorkTypeOverride(workType))
                SettingsWidgets.DrawRevert(revertRect, "AP_RevertWorkTypeTip".Translate(),
                    () => settings.ClearWorkType(workType));

            if (isExpanded)
                DrawAdvancedRows(listing, workType, def, everyoneOn);
        }

        private static void DrawAdvancedRows(Listing_Standard listing, WorkTypeDef workType, WorkTypePolicyDef def, bool everyoneOn)
        {
            var settings = AdaptivePrioritiesMod.Settings;
            bool coverageOn = ScoringConfig.CoverageGuaranteeEnabled;
            string pinKey = AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.PinPriorityKey, workType);
            bool pinnedOn = settings.GetBool(pinKey, def.pinPriority);
            float labelWidth = SettingsWidgets.LabelWidth - SubIndent;

            var minRect = Indent(listing.GetRect(SettingsWidgets.RowHeight));
            SettingsWidgets.IntRow(minRect, "AP_MinWorkers".Translate(),
                AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.MinWorkersKey, workType),
                def.minWorkers, 0, 10,
                SettingsWidgets.Tip(
                    () => "AP_MinWorkersTip".Translate(),
                    () => def.minWorkers.ToString(),
                    disabledReason: () => coverageOn ? null : (string)"AP_IgnoredCoverageOff".Translate()),
                disabled: !coverageOn, labelWidth: labelWidth);

            var cutoffRect = Indent(listing.GetRect(SettingsWidgets.RowHeight));
            SettingsWidgets.FloatRow(cutoffRect, "AP_SpecialistCutoff".Translate(),
                AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.ScoreCutoffKey, workType),
                def.scoreCutoff, 0f, 1f,
                SettingsWidgets.Tip(
                    () => "AP_SpecialistCutoffTip".Translate(),
                    () => def.scoreCutoff.ToString("0.00"),
                    disabledReason: () => everyoneOn ? (string)"AP_IgnoredWhileEveryone".Translate() : null),
                disabled: everyoneOn, labelWidth: labelWidth);

            string maxKey = AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.MaxWorkersFractionKey, workType);
            var maxRect = Indent(listing.GetRect(SettingsWidgets.RowHeight));
            SettingsWidgets.FloatRow(maxRect, "AP_MaxWorkersShare".Translate(), maxKey,
                def.maxWorkersFraction, 0f, 1f,
                SettingsWidgets.Tip(
                    () => "AP_MaxWorkersShareTip".Translate(),
                    () => def.maxWorkersFraction.ToString("0.00"),
                    () => MaxWorkersLiveLine(workType, AdaptivePrioritiesMod.Settings.GetFloat(maxKey, def.maxWorkersFraction)),
                    () => everyoneOn ? (string)"AP_IgnoredWhileEveryone".Translate() : null),
                disabled: everyoneOn, labelWidth: labelWidth);

            var pinRect = Indent(listing.GetRect(SettingsWidgets.RowHeight));
            SettingsWidgets.BoolRow(pinRect, "AP_AlwaysTop".Translate(), pinKey, def.pinPriority,
                SettingsWidgets.Tip(
                    () => "AP_AlwaysTopTip".Translate(),
                    () => (def.pinPriority ? "On" : "Off").Translate(),
                    disabledReason: () => everyoneOn ? (string)"AP_IgnoredWhileEveryone".Translate() : null),
                disabled: everyoneOn, labelWidth: labelWidth);

            var falloffRect = Indent(listing.GetRect(SettingsWidgets.RowHeight));
            SettingsWidgets.BoolRow(falloffRect, "AP_PriorityFalloff".Translate(),
                AdaptivePrioritiesSettings.PolicyKey(WorkPolicyConfig.PriorityFalloffKey, workType),
                def.priorityFalloff,
                SettingsWidgets.Tip(
                    () => "AP_PriorityFalloffTip".Translate(),
                    () => (def.priorityFalloff ? "On" : "Off").Translate(),
                    disabledReason: () => everyoneOn || pinnedOn ? (string)"AP_IgnoredWhilePinnedOrEveryone".Translate() : null),
                disabled: everyoneOn || pinnedOn, labelWidth: labelWidth);

            listing.Gap(4f);
        }

        private static Rect Indent(Rect rect)
        {
            rect.xMin += SubIndent;
            return rect;
        }

        /// <summary>"At most X of Y capable colonists" — the live colony when one is loaded, else an
        /// illustrative ten (settings can open from the main menu with no game).</summary>
        private static string MaxWorkersLiveLine(WorkTypeDef workType, float fraction)
        {
            var map = Current.ProgramState == ProgramState.Playing ? Find.CurrentMap : null;
            var colonists = map != null ? WorkTypeDiscoveryService.GetColonistsOnMap(map) : null;
            if (colonists == null || colonists.Count == 0)
                return "AP_LiveMaxWorkersExample".Translate(Mathf.CeilToInt(fraction * 10f), 10);

            int capable = 0;
            foreach (var pawn in colonists)
                if (!pawn.WorkTypeIsDisabled(workType))
                    capable++;
            return "AP_LiveMaxWorkers".Translate(Mathf.CeilToInt(fraction * capable), capable);
        }
    }
}
