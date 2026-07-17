using System;
using AdaptivePriorities.Core;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.UI.Settings
{
    /// <summary>
    /// Passions tab: one row per discovered passion (the vanilla three, or VSE's full list with real
    /// icons), each with its bonus slider. Vanilla Minor/Major write the same minorPassionBonus /
    /// majorPassionBonus keys the scorer reads; modded passions get passionBonus@defName overrides
    /// whose *default* is derived live from their learn rate — so untouched rows keep tracking the
    /// Minor/Major sliders. The curve-shaping knobs (bad-passion penalty, curve cap) sit below in
    /// advanced mode.
    /// </summary>
    public static class PassionsTab
    {
        private const float IconSize = 24f;
        private const float IconGutter = 30f;

        private static Vector2 scrollPos;
        private static float lastHeight = 400f;

        public static void Draw(Rect rect)
        {
            var viewRect = new Rect(0f, 0f, rect.width - 20f, lastHeight);
            Widgets.BeginScrollView(rect, ref scrollPos, viewRect);
            var listing = new Listing_Standard { maxOneColumn = true };
            listing.Begin(viewRect);
            try
            {
                var d = ScoringTuningDef.Active;

                foreach (var row in PassionDiscoveryService.GetRows())
                    DrawPassionRow(listing, row, d);

                if (AdaptivePrioritiesMod.Settings.advancedMode)
                {
                    listing.GapLine();
                    SettingsWidgets.FloatRow(listing.GetRect(SettingsWidgets.RowHeight),
                        "AP_BadPassionPenalty".Translate(), "badPassionPenalty", d.badPassionPenalty, 0f, 1f,
                        SettingsWidgets.Tip(() => "AP_BadPassionPenaltyTip".Translate(),
                            () => d.badPassionPenalty.ToString("0.00")));
                    SettingsWidgets.FloatRow(listing.GetRect(SettingsWidgets.RowHeight),
                        "AP_MaxPassionBonus".Translate(), "maxPassionBonus", d.maxPassionBonus, 0f, 1f,
                        SettingsWidgets.Tip(() => "AP_MaxPassionBonusTip".Translate(),
                            () => d.maxPassionBonus.ToString("0.00")));
                }
            }
            finally
            {
                lastHeight = listing.CurHeight;
                listing.End();
                Widgets.EndScrollView();
            }
        }

        private static void DrawPassionRow(Listing_Standard listing, PassionRow row, ScoringTuningDef d)
        {
            var rect = listing.GetRect(SettingsWidgets.RowHeight);
            if (row.icon != null)
                GUI.DrawTexture(new Rect(rect.x, rect.y + (rect.height - IconSize) / 2f, IconSize, IconSize), row.icon);

            var rowRect = new Rect(rect.x + IconGutter, rect.y, rect.width - IconGutter, rect.height);
            float labelWidth = SettingsWidgets.LabelWidth - IconGutter;
            string label = row.label;
            if (row.kind == PassionRowKind.Modded && row.hasLearnRate)
                label += " (" + row.learnRate.ToString("0.0") + "x)";

            switch (row.kind)
            {
                case PassionRowKind.None:
                    SettingsWidgets.InfoRow(rowRect, label, "0.00",
                        SettingsWidgets.Tip(() => "AP_PassionNoneTip".Translate(), null), labelWidth);
                    break;

                case PassionRowKind.Minor:
                    SettingsWidgets.FloatRow(rowRect, label, "minorPassionBonus", d.minorPassionBonus, 0f, 1f,
                        VanillaTip("AP_MinorPassionBonusTip", () => d.minorPassionBonus,
                            () => ScoringConfig.MinorPassionBonus), labelWidth: labelWidth);
                    break;

                case PassionRowKind.Major:
                    SettingsWidgets.FloatRow(rowRect, label, "majorPassionBonus", d.majorPassionBonus, 0f, 1f,
                        VanillaTip("AP_MajorPassionBonusTip", () => d.majorPassionBonus,
                            () => ScoringConfig.MajorPassionBonus), labelWidth: labelWidth);
                    break;

                case PassionRowKind.Modded:
                    // The default is recomputed each frame: it follows the learn-rate curve and the
                    // live Minor/Major values until the player overrides this specific passion.
                    float defaultBonus = PassionScoreService.DefaultBonusFor(row.passion);
                    SettingsWidgets.FloatRow(rowRect, label, row.SettingsKey, defaultBonus, -1f, 1f,
                        ModdedTip(row), labelWidth: labelWidth);
                    break;
            }
        }

        private static Func<string> VanillaTip(string tipKey, Func<float> defaultValue, Func<float> currentValue)
        {
            return SettingsWidgets.Tip(
                () => tipKey.Translate(),
                () => defaultValue().ToString("0.00"),
                () => SkillLevelsLine(currentValue()));
        }

        private static Func<string> ModdedTip(PassionRow row)
        {
            return SettingsWidgets.Tip(
                () =>
                {
                    string text = PassionScoreService.HasXmlOverride(row.defName)
                        ? "AP_ModdedPassionTipPinned".Translate()
                        : "AP_ModdedPassionTip".Translate(row.hasLearnRate ? row.learnRate.ToString("0.0") : "?");
                    if (row.isBad)
                        text += "\n\n" + "AP_BadPassionNote".Translate();
                    return text;
                },
                () => PassionScoreService.DefaultBonusFor(row.passion).ToString("0.00"),
                () => SkillLevelsLine(EffectiveBonus(row)));
        }

        private static float EffectiveBonus(PassionRow row)
        {
            var settings = AdaptivePrioritiesMod.Settings;
            float fallback = PassionScoreService.DefaultBonusFor(row.passion);
            return settings != null ? settings.GetFloat(row.SettingsKey, fallback) : fallback;
        }

        /// <summary>"Worth ≈ N skill levels": bonus × 20 / skillWeight (see the ScoringTuningDef doc).</summary>
        private static string SkillLevelsLine(float bonus)
        {
            float skillWeight = ScoringConfig.SkillWeight;
            if (skillWeight < 0.01f)
                return null;
            return "AP_LivePassionLevels".Translate((bonus * 20f / skillWeight).ToString("0.0"));
        }
    }
}
