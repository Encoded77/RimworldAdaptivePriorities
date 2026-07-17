using RimWorld;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.UI.Settings
{
    /// <summary>General tab: the auto-recalc interval and the colony-wide coverage guarantee.</summary>
    public static class GeneralTab
    {
        private static Vector2 scrollPos;
        private static float lastHeight = 200f;

        public static void Draw(Rect rect)
        {
            var viewRect = new Rect(0f, 0f, rect.width - 20f, lastHeight);
            Widgets.BeginScrollView(rect, ref scrollPos, viewRect);
            var listing = new Listing_Standard { maxOneColumn = true };
            listing.Begin(viewRect);
            try
            {
                DrawIntervalRow(listing.GetRect(SettingsWidgets.RowHeight));

                var d = ScoringTuningDef.Active;
                SettingsWidgets.BoolRow(listing.GetRect(SettingsWidgets.RowHeight),
                    "AP_CoverageGuarantee".Translate(), "coverageGuaranteeEnabled", d.coverageGuaranteeEnabled,
                    SettingsWidgets.Tip(
                        () => "AP_CoverageGuaranteeTip".Translate(),
                        () => (d.coverageGuaranteeEnabled ? "On" : "Off").Translate()));
            }
            finally
            {
                lastHeight = listing.CurHeight;
                listing.End();
                Widgets.EndScrollView();
            }
        }

        /// <summary>
        /// Bespoke row: the interval is a plain settings field, not an implicit-override key, so it
        /// uses the kit's primitives directly instead of a keyed row.
        /// </summary>
        private static void DrawIntervalRow(Rect rect)
        {
            var settings = AdaptivePrioritiesMod.Settings;
            SettingsWidgets.Split(rect, SettingsWidgets.LabelWidth,
                out var labelRect, out var sliderRect, out var valueRect, out var revertRect);
            SettingsWidgets.RowChrome(rect, labelRect, "AP_ReassignEvery".Translate(), "autoRecalcIntervalTicks",
                SettingsWidgets.Tip(() => "AP_AutoIntervalTip".Translate(), () => "1h"), disabled: false);

            int hours = Mathf.Max(1, Mathf.RoundToInt(settings.autoRecalcIntervalTicks / (float)GenDate.TicksPerHour));
            SettingsWidgets.DrawValue(valueRect, hours + "h");

            int v = Mathf.RoundToInt(SettingsWidgets.Slider(sliderRect, "autoRecalcIntervalTicks", hours, 1f, 72f));
            if (v != hours)
                settings.autoRecalcIntervalTicks = v * GenDate.TicksPerHour;

            if (hours != 1)
                SettingsWidgets.DrawRevert(revertRect, "AP_RevertTip".Translate(),
                    () => settings.autoRecalcIntervalTicks = GenDate.TicksPerHour);
        }
    }
}
