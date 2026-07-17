using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.UI.Settings
{
    /// <summary>
    /// The settings window shell: header bar (advanced toggle + reset-all, reachable from every tab),
    /// the four tabs, and dispatch to the active tab. Tab selection and per-tab scroll/expansion state
    /// are transient UI state, deliberately not persisted in ExposeData.
    /// </summary>
    public static class SettingsWindowContents
    {
        private const float HeaderHeight = 28f;

        private enum SettingsTab
        {
            General,
            Scoring,
            WorkTypes,
            ExternalWorkers,
            Passions,
        }

        private static SettingsTab current = SettingsTab.General;
        private static List<TabRecord> tabs;

        public static void Draw(Rect inRect)
        {
            var settings = AdaptivePrioritiesMod.Settings;

            var header = new Rect(inRect.x, inRect.y, inRect.width, HeaderHeight);
            Widgets.CheckboxLabeled(new Rect(header.x, header.y + 2f, 240f, 24f),
                "AP_AdvancedMode".Translate(), ref settings.advancedMode);
            if (settings.HasAnyOverride &&
                Widgets.ButtonText(new Rect(header.xMax - 180f, header.y, 180f, header.height), "AP_ResetAll".Translate()))
            {
                settings.ClearAll();
            }

            // DrawTabs renders the tab strip *above* the rect it's given (rect.y -= TabHeight
            // internally), so the framed content rect starts one TabHeight below the header.
            var content = new Rect(inRect.x, header.yMax + TabDrawer.TabHeight,
                inRect.width, inRect.height - HeaderHeight - TabDrawer.TabHeight);
            Widgets.DrawMenuSection(content);
            TabDrawer.DrawTabs(content, Tabs);

            var inner = content.ContractedBy(12f);
            switch (current)
            {
                case SettingsTab.General:
                    GeneralTab.Draw(inner);
                    break;
                case SettingsTab.Scoring:
                    ScoringTab.Draw(inner);
                    break;
                case SettingsTab.WorkTypes:
                    WorkTypesTab.Draw(inner);
                    break;
                case SettingsTab.ExternalWorkers:
                    ExternalWorkersTab.Draw(inner);
                    break;
                case SettingsTab.Passions:
                    PassionsTab.Draw(inner);
                    break;
            }
        }

        private static List<TabRecord> Tabs => tabs ??= new List<TabRecord>
        {
            new TabRecord("AP_TabGeneral".Translate(), () => current = SettingsTab.General, () => current == SettingsTab.General),
            new TabRecord("AP_TabScoring".Translate(), () => current = SettingsTab.Scoring, () => current == SettingsTab.Scoring),
            new TabRecord("AP_TabWorkTypes".Translate(), () => current = SettingsTab.WorkTypes, () => current == SettingsTab.WorkTypes),
            new TabRecord("AP_TabExternalWorkers".Translate(), () => current = SettingsTab.ExternalWorkers, () => current == SettingsTab.ExternalWorkers),
            new TabRecord("AP_TabPassions".Translate(), () => current = SettingsTab.Passions, () => current == SettingsTab.Passions),
        };
    }
}
