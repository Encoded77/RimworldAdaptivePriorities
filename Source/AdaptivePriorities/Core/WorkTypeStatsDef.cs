using System.Collections.Generic;
using System.Xml;
using RimWorld;
using Verse;

namespace AdaptivePriorities
{
    /// <summary>
    /// One stat in a <see cref="WorkTypeStatsDef"/>. Shorthand when the stat is already a multiplier
    /// neutral at 1, long form when it needs a baseline:
    ///
    ///   <li>WorkSpeedGlobal</li>
    ///   <li><stat>CarryingCapacity</stat><baseline>75</baseline></li>
    /// </summary>
    public class WorkStatEntry
    {
        /// <summary>Stat defName. Resolved at runtime and skipped silently if absent.</summary>
        public string stat;

        /// <summary>
        /// The value of this stat that counts as average; the scorer divides by it. Absolute stats are
        /// not centred on 1 the way work-speed multipliers are - a baseline colonist carries 75 and walks
        /// at 4.6 - and feeding those in raw would swamp every other term.
        /// </summary>
        public float baseline = 1f;

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            // A <stat> child element distinguishes the two forms; the first child's node type would
            // misread whitespace between elements.
            var statNode = xmlRoot["stat"];
            if (statNode == null)
            {
                stat = xmlRoot.InnerText?.Trim();
                return;
            }

            stat = statNode.InnerText?.Trim();
            var baselineNode = xmlRoot["baseline"];
            if (baselineNode != null)
                baseline = ParseHelper.FromString<float>(baselineNode.InnerText.Trim());
        }
    }

    /// <summary>A resolved stat and the value that counts as average for it.</summary>
    public struct WorkStat
    {
        public StatDef stat;
        public float baseline;
    }

    /// <summary>The resolved aptitude configuration for one work type.</summary>
    public struct WorkStats
    {
        public List<WorkStat> stats;

        /// <summary>Multiplier on the global workStatWeight; see WorkTypeStatsDef.weightFactor.</summary>
        public float weightFactor;
    }

    /// <summary>
    /// Maps a work type to the pawn stat(s) that measure aptitude for it (Growing -> PlantWorkSpeed,
    /// Mining -> MiningSpeed, ...). The scorer folds these in to capture trait/gene/bionic modifiers a
    /// raw skill level misses. Pure XML. A def with an empty workTypeDef is the fallback for any work
    /// type without its own entry.
    /// </summary>
    public class WorkTypeStatsDef : Def
    {
        public string workTypeDef;

        /// <summary>Stats averaged into one aptitude factor, each measured against its own baseline.</summary>
        public List<WorkStatEntry> stats;

        /// <summary>
        /// Multiplies the global workStatWeight for this work type. 1 where the stats only nudge a score
        /// the skill already decides; raise it where the stats are the job, as for skill-less Hauling.
        /// </summary>
        public float weightFactor = 1f;

        private static Dictionary<WorkTypeDef, WorkStats> byWorkType;
        private static WorkStats fallback;

        public static WorkStats For(WorkTypeDef workType)
        {
            if (byWorkType == null)
                Build();

            return byWorkType.TryGetValue(workType, out var stats) ? stats : fallback;
        }

        private static void Build()
        {
            byWorkType = new Dictionary<WorkTypeDef, WorkStats>();
            fallback = new WorkStats { stats = new List<WorkStat>(), weightFactor = 1f };
            bool fallbackFound = false;

            foreach (var def in DefDatabase<WorkTypeStatsDef>.AllDefsListForReading)
            {
                var resolved = new WorkStats { stats = Resolve(def.stats), weightFactor = def.weightFactor };

                if (def.workTypeDef.NullOrEmpty())
                {
                    if (!fallbackFound)
                    {
                        fallback = resolved;
                        fallbackFound = true;
                    }
                    continue;
                }

                var workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(def.workTypeDef);
                if (workType != null)
                    byWorkType[workType] = resolved;
            }
        }

        private static List<WorkStat> Resolve(List<WorkStatEntry> entries)
        {
            var result = new List<WorkStat>();
            if (entries == null)
                return result;

            foreach (var entry in entries)
            {
                if (entry.stat.NullOrEmpty())
                    continue;

                var stat = DefDatabase<StatDef>.GetNamedSilentFail(entry.stat);
                if (stat == null)
                    continue;

                if (entry.baseline <= 0f)
                {
                    Log.Warning($"[Adaptive Priorities] WorkTypeStatsDef '{entry.stat}' has baseline {entry.baseline}; must be above 0. Skipping the stat.");
                    continue;
                }

                result.Add(new WorkStat { stat = stat, baseline = entry.baseline });
            }

            return result;
        }
    }
}
