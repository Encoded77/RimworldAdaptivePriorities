using System.Collections.Generic;
using Verse;

namespace AdaptivePriorities
{
    /// <summary>
    /// How external workers (mechs, drones) reduce the colonist demand for a work type. Off ignores them;
    /// Reduce subtracts their capacity but keeps a backup floor of colonists; Full offloads hard, down to
    /// zero colonists.
    /// </summary>
    public enum ExternalOffloadMode
    {
        Off,
        Reduce,
        Full,
    }

    /// <summary>
    /// Colony-wide assignment policy for one work type: how important the job is, whether every capable
    /// pawn does it or only the best few, and the minimum worker count for coverage. Pure XML data. A
    /// def with an empty workTypeDef is the default for work types nothing else covers.
    /// </summary>
    public class WorkTypePolicyDef : Def
    {
        /// <summary>defName of the target WorkTypeDef; resolved at runtime, silently skipped if absent. Empty = default policy.</summary>
        public string workTypeDef;

        /// <summary>How urgent this job is, 0..1 (1 maps to priority 1). Independent of who does it.</summary>
        public float urgency = 0.5f;

        /// <summary>All capable pawns get the job at the urgency priority (Firefighter, Patient, Hauling...). Ignores the specialization fields below.</summary>
        public bool assignEveryone;

        /// <summary>Coverage guarantee: at least this many workers get assigned even if their scores are poor, as long as anyone capable exists.</summary>
        public int minWorkers = 1;

        /// <summary>Specialization cap as a fraction of capable colonists (ceil), never below minWorkers.</summary>
        public float maxWorkersFraction = 0.5f;

        /// <summary>Capable pawns scoring below this get 0 instead of a low priority — specialists only, unless needed for minWorkers.</summary>
        public float scoreCutoff = 0.35f;

        /// <summary>If true, each lower-ranked assigned worker gets one priority step worse than the one above.</summary>
        public bool priorityFalloff;

        /// <summary>
        /// Pin every assigned worker to exactly the urgency priority, ignoring quality/falloff. For
        /// life-critical work (Doctor) so it never sinks below chore work on a wide priority range.
        /// </summary>
        public bool pinPriority;

        /// <summary>
        /// How external workers (mechs, drones) reduce colonist demand for this work type. Reduce by
        /// default; emergency/self-care work ships Off so a lone drone can't thin its coverage.
        /// </summary>
        public ExternalOffloadMode externalOffload = ExternalOffloadMode.Reduce;

        /// <summary>Colonists always kept on this work type under Reduce, however much external capacity exists.</summary>
        public int externalBackup = 1;

        /// <summary>
        /// Multiplier on external capacity before it is floored into a colonist-equivalent reduction: a
        /// full-time automaton is worth more than one colonist on fragmented menial work. 1 = one-for-one.
        /// </summary>
        public float externalUptimeFactor = 1f;

        private static Dictionary<WorkTypeDef, WorkTypePolicyDef> byWorkType;
        private static float maxNaturalPriority;

        public static WorkTypePolicyDef For(WorkTypeDef workType)
        {
            if (byWorkType == null)
            {
                byWorkType = new Dictionary<WorkTypeDef, WorkTypePolicyDef>();
                foreach (var def in DefDatabase<WorkTypePolicyDef>.AllDefsListForReading)
                {
                    var resolved = DefDatabase<WorkTypeDef>.GetNamedSilentFail(def.workTypeDef);
                    if (resolved != null)
                        byWorkType[resolved] = def;
                }

                maxNaturalPriority = 1f;
                foreach (var wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                    maxNaturalPriority = UnityEngine.Mathf.Max(maxNaturalPriority, wt.naturalPriority);
            }

            if (!byWorkType.TryGetValue(workType, out var policy))
            {
                policy = Derive(workType);
                byWorkType[workType] = policy;
            }

            return policy;
        }

        /// <summary>
        /// Work types without an explicit def (typically modded) get a policy derived from their own
        /// WorkTypeDef: urgency from naturalPriority (normalized against the highest loaded), and
        /// everyone-assignment when the job has no relevant skills. An explicit def always wins.
        ///
        /// External-worker defaults (the larger backup a quality craft keeps, say) come from an optional
        /// <see cref="ExternalWorkerDefaults"/> mod extension on the WorkTypeDef, so a mod adding
        /// quality-craft work types (subjobs derived from Crafting/Smithing...) can mark them the same way
        /// vanilla crafts are marked, without any code here. Absent extension = the plain class defaults.
        /// </summary>
        private static WorkTypePolicyDef Derive(WorkTypeDef workType)
        {
            var ext = workType.GetModExtension<ExternalWorkerDefaults>();
            return new WorkTypePolicyDef
            {
                defName = "AP_Derived_" + workType.defName,
                workTypeDef = workType.defName,
                urgency = UnityEngine.Mathf.Clamp01(workType.naturalPriority / maxNaturalPriority),
                assignEveryone = workType.relevantSkills.NullOrEmpty(),
                externalOffload = ext?.externalOffload ?? ExternalOffloadMode.Reduce,
                externalBackup = ext?.externalBackup ?? 1,
                externalUptimeFactor = ext?.externalUptimeFactor ?? 1f,
            };
        }
    }
}
