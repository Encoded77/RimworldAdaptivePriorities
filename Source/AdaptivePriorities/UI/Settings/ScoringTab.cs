using System;
using AdaptivePriorities.Core;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.UI.Settings
{
    /// <summary>Scoring tab: the global formula weights. Passion knobs live on the Passions tab.</summary>
    public static class ScoringTab
    {
        private static Vector2 scrollPos;
        private static float lastHeight = 500f;

        public static void Draw(Rect rect)
        {
            var viewRect = new Rect(0f, 0f, rect.width - 20f, lastHeight);
            Widgets.BeginScrollView(rect, ref scrollPos, viewRect);
            var listing = new Listing_Standard { maxOneColumn = true };
            listing.Begin(viewRect);
            try
            {
                var d = ScoringTuningDef.Active;

                FloatRow(listing, "AP_SkillImportance", "skillWeight", d.skillWeight, 0f, 2f,
                    Tip("AP_SkillImportanceTip", d.skillWeight));
                FloatRow(listing, "AP_RewardSpecialists", "qualityWeight", d.qualityWeight, 0f, 1f,
                    Tip("AP_RewardSpecialistsTip", d.qualityWeight));
                BoolRow(listing, "AP_InspirationBoost", "inspirationBonusEnabled", d.inspirationBonusEnabled,
                    Tip("AP_InspirationBoostTip", d.inspirationBonusEnabled));
                BoolRow(listing, "AP_AssignOpposed", "assignOpposedWhenNeeded", d.assignOpposedWhenNeeded,
                    Tip("AP_AssignOpposedTip", d.assignOpposedWhenNeeded));

                if (AdaptivePrioritiesMod.Settings.advancedMode)
                {
                    listing.GapLine();
                    FloatRow(listing, "AP_UnskilledBaseline", "noSkillWorkScore", d.noSkillWorkScore, 0f, 1f,
                        Tip("AP_UnskilledBaselineTip", d.noSkillWorkScore));
                    FloatRow(listing, "AP_CompareToBest", "relativeWeight", d.relativeWeight, 0f, 1f,
                        Tip("AP_CompareToBestTip", d.relativeWeight));
                    FloatRow(listing, "AP_UseWorkSpeed", "workStatWeight", d.workStatWeight, 0f, 1f,
                        SettingsWidgets.Tip(
                            () => "AP_UseWorkSpeedTip".Translate(),
                            () => d.workStatWeight.ToString("0.00"),
                            () => "AP_LiveWorkStat".Translate((ScoringConfig.WorkStatWeight * 40f).ToString("0"))));
                    FloatRow(listing, "AP_OpposedWork", "opposedWorkFactor", d.opposedWorkFactor, 0f, 1f,
                        Tip("AP_OpposedWorkTip", d.opposedWorkFactor));
                    FloatRow(listing, "AP_InspirationStrength", "inspirationBonus", d.inspirationBonus, 0f, 1f,
                        Tip("AP_InspirationStrengthTip", d.inspirationBonus));
                    FloatRow(listing, "AP_InspirationPriority", "inspirationUrgency", d.inspirationUrgency, 0f, 1f,
                        Tip("AP_InspirationPriorityTip", d.inspirationUrgency));
                }
            }
            finally
            {
                lastHeight = listing.CurHeight;
                listing.End();
                Widgets.EndScrollView();
            }
        }

        private static void FloatRow(Listing_Standard listing, string labelKey, string key, float def, float min, float max, Func<string> tip)
        {
            SettingsWidgets.FloatRow(listing.GetRect(SettingsWidgets.RowHeight), labelKey.Translate(), key, def, min, max, tip);
        }

        private static void BoolRow(Listing_Standard listing, string labelKey, string key, bool def, Func<string> tip)
        {
            SettingsWidgets.BoolRow(listing.GetRect(SettingsWidgets.RowHeight), labelKey.Translate(), key, def, tip);
        }

        private static Func<string> Tip(string tipKey, float def)
        {
            return SettingsWidgets.Tip(() => tipKey.Translate(), () => def.ToString("0.00"));
        }

        private static Func<string> Tip(string tipKey, bool def)
        {
            return SettingsWidgets.Tip(() => tipKey.Translate(), () => (def ? "On" : "Off").Translate());
        }
    }
}
