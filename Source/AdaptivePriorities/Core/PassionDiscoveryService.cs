using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.Core
{
    public enum PassionRowKind
    {
        /// <summary>No passion: informational row, always 0, not tunable.</summary>
        None,
        Minor,
        Major,
        /// <summary>A VSE/Alpha Skills passion, keyed per defName.</summary>
        Modded,
    }

    /// <summary>One passion the settings UI can show/tune. Identity data only; defaults are computed
    /// live at draw time so untouched modded passions keep tracking the Minor/Major sliders.</summary>
    public class PassionRow
    {
        public PassionRowKind kind;
        public Passion passion;
        /// <summary>VSE PassionDef defName; null for the vanilla-fallback rows.</summary>
        public string defName;
        public string label;
        /// <summary>May be null (e.g. no-passion has no icon); the UI draws nothing then.</summary>
        public Texture2D icon;
        public float learnRate;
        public bool hasLearnRate;
        public bool isBad;

        /// <summary>Settings override key; null for the informational None row.</summary>
        public string SettingsKey
        {
            get
            {
                switch (kind)
                {
                    case PassionRowKind.Minor: return "minorPassionBonus";
                    case PassionRowKind.Major: return "majorPassionBonus";
                    case PassionRowKind.Modded: return AdaptivePrioritiesSettings.PassionKey(defName);
                    default: return null;
                }
            }
        }
    }

    /// <summary>
    /// Every passion the settings UI can list, in display order. With VSE active this is its full
    /// registered list (vanilla three first, real icons); without it, just the vanilla three. The set
    /// of passions is session-fixed so rows are built once — but icons resolve via ContentFinder, so
    /// the first call must come from the main (OnGUI) thread.
    /// </summary>
    public static class PassionDiscoveryService
    {
        private static List<PassionRow> cached;

        public static List<PassionRow> GetRows() => cached ??= Build();

        private static List<PassionRow> Build()
        {
            var rows = new List<PassionRow>();
            var vseDefs = VsePassionBridge.AllPassionDefs();

            if (vseDefs.Count == 0)
            {
                rows.Add(new PassionRow { kind = PassionRowKind.None, passion = Passion.None, label = "PassionNone".Translate() });
                rows.Add(new PassionRow { kind = PassionRowKind.Minor, passion = Passion.Minor, label = "PassionMinor".Translate(), icon = SkillUI.PassionMinorIcon });
                rows.Add(new PassionRow { kind = PassionRowKind.Major, passion = Passion.Major, label = "PassionMajor".Translate(), icon = SkillUI.PassionMajorIcon });
                return rows;
            }

            var noneDef = VsePassionBridge.PassionToDef(Passion.None);
            var minorDef = VsePassionBridge.PassionToDef(Passion.Minor);
            var majorDef = VsePassionBridge.PassionToDef(Passion.Major);

            foreach (var def in vseDefs)
            {
                if (def == null)
                    continue;

                var row = new PassionRow
                {
                    defName = def.defName,
                    label = def.LabelCap,
                    icon = VsePassionBridge.IconFor(def),
                };

                if (def == noneDef)
                {
                    row.kind = PassionRowKind.None;
                    row.passion = Passion.None;
                }
                else if (def == minorDef)
                {
                    row.kind = PassionRowKind.Minor;
                    row.passion = Passion.Minor;
                    if (row.icon == null)
                        row.icon = SkillUI.PassionMinorIcon;
                }
                else if (def == majorDef)
                {
                    row.kind = PassionRowKind.Major;
                    row.passion = Passion.Major;
                    if (row.icon == null)
                        row.icon = SkillUI.PassionMajorIcon;
                }
                else
                {
                    row.kind = PassionRowKind.Modded;
                    // VSE assigns each PassionDef's index to its Passion byte, so the def's index *is*
                    // the enum value (PassionManager.AllPassions does the same cast).
                    row.passion = (Passion)def.index;
                    if (VsePassionBridge.TryGetData(def, out float learnRate, out bool isBad))
                    {
                        row.learnRate = learnRate;
                        row.hasLearnRate = true;
                        row.isBad = isBad;
                    }
                }

                rows.Add(row);
            }

            return rows;
        }
    }
}
