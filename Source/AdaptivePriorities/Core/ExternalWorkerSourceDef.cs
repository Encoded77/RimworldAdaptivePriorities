using System.Collections.Generic;
using Verse;

namespace AdaptivePriorities
{
    /// <summary>A resolved non-mech external-worker source: its settings category and the work it covers.</summary>
    public struct ResolvedExternalSource
    {
        public string category;
        public List<WorkTypeDef> workTypes;
    }

    /// <summary>
    /// Maps a non-mech automaton ThingDef (a VQE drone, say) to the work type(s) it takes off colonists.
    /// Mechs are discovered generically via RaceProps.mechEnabledWorkTypes and need no entry here. Pure
    /// XML: the extension point any mod or the player can add to. ThingDef/WorkTypeDef names resolve at
    /// runtime and are skipped silently if absent, so a def targeting an uninstalled mod is inert.
    /// </summary>
    public class ExternalWorkerSourceDef : Def
    {
        /// <summary>Settings toggle group this source belongs to ("Drones", "Mechs"...). Drives the tab checkbox.</summary>
        public string category = "Drones";

        /// <summary>The automaton race/thing defName. Either this or <see cref="thingDefs"/> (or both).</summary>
        public string thingDef;

        /// <summary>Several automaton defNames sharing one work mapping.</summary>
        public List<string> thingDefs;

        /// <summary>Work types these automatons cover.</summary>
        public List<string> workTypeDefs;

        private static Dictionary<ThingDef, ResolvedExternalSource> resolved;
        private static List<string> categories;

        /// <summary>ThingDef -> the category and work types it covers, built once from the loaded defs.</summary>
        public static Dictionary<ThingDef, ResolvedExternalSource> Resolved
        {
            get
            {
                if (resolved == null)
                    Build();
                return resolved;
            }
        }

        /// <summary>Distinct categories across all source defs (for the settings tab), in source order.</summary>
        public static List<string> Categories
        {
            get
            {
                if (categories == null)
                    Build();
                return categories;
            }
        }

        private static void Build()
        {
            resolved = new Dictionary<ThingDef, ResolvedExternalSource>();
            categories = new List<string>();

            foreach (var def in DefDatabase<ExternalWorkerSourceDef>.AllDefsListForReading)
            {
                var workTypes = new List<WorkTypeDef>();
                if (def.workTypeDefs != null)
                {
                    foreach (var name in def.workTypeDefs)
                    {
                        var wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(name);
                        if (wt != null && !workTypes.Contains(wt))
                            workTypes.Add(wt);
                    }
                }
                if (workTypes.Count == 0)
                    continue;

                string category = def.category.NullOrEmpty() ? "Drones" : def.category;
                if (!categories.Contains(category))
                    categories.Add(category);

                foreach (var thing in EnumerateThings(def))
                {
                    if (thing != null)
                        // Last def wins for a duplicated thing; merging two mappings would surprise more
                        // than help, and duplicates are not expected.
                        resolved[thing] = new ResolvedExternalSource { category = category, workTypes = workTypes };
                }
            }
        }

        private static IEnumerable<ThingDef> EnumerateThings(ExternalWorkerSourceDef def)
        {
            if (!def.thingDef.NullOrEmpty())
                yield return DefDatabase<ThingDef>.GetNamedSilentFail(def.thingDef);
            if (def.thingDefs != null)
                foreach (var name in def.thingDefs)
                    if (!name.NullOrEmpty())
                        yield return DefDatabase<ThingDef>.GetNamedSilentFail(name);
        }
    }
}
