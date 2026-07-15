using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AdaptivePriorities
{
    /// <summary>
    /// Maps a work type to the pawn stat(s) that measure aptitude for it (Growing -> PlantWorkSpeed,
    /// Mining -> MiningSpeed, ...). The scorer folds these in to capture trait/gene/bionic modifiers a
    /// raw skill level misses. Pure XML. A def with an empty workTypeDef is the fallback for any work
    /// type without its own entry. Stat names resolve at runtime and are skipped silently if absent.
    /// </summary>
    public class WorkTypeStatsDef : Def
    {
        public string workTypeDef;

        /// <summary>Stat defNames; their values are averaged into one aptitude factor.</summary>
        public List<string> stats;

        private static Dictionary<WorkTypeDef, List<StatDef>> byWorkType;
        private static List<StatDef> fallbackStats;

        public static List<StatDef> StatsFor(WorkTypeDef workType)
        {
            if (byWorkType == null)
                Build();

            return byWorkType.TryGetValue(workType, out var stats) ? stats : fallbackStats;
        }

        private static void Build()
        {
            byWorkType = new Dictionary<WorkTypeDef, List<StatDef>>();
            fallbackStats = new List<StatDef>();

            foreach (var def in DefDatabase<WorkTypeStatsDef>.AllDefsListForReading)
            {
                var resolved = Resolve(def.stats);

                if (def.workTypeDef.NullOrEmpty())
                {
                    if (fallbackStats.Count == 0)
                        fallbackStats = resolved;
                    continue;
                }

                var workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(def.workTypeDef);
                if (workType != null)
                    byWorkType[workType] = resolved;
            }
        }

        private static List<StatDef> Resolve(List<string> statNames)
        {
            var result = new List<StatDef>();
            if (statNames == null)
                return result;

            foreach (var name in statNames)
            {
                var stat = DefDatabase<StatDef>.GetNamedSilentFail(name);
                if (stat != null)
                    result.Add(stat);
            }

            return result;
        }
    }
}
