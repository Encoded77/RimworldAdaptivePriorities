using Verse;

namespace AdaptivePriorities
{
    /// <summary>
    /// Weights for the pawn/work-type scoring formula, exposed as a Def so it can be rebalanced with
    /// XML patches. One def is expected; the first in the DefDatabase wins.
    /// </summary>
    public class ScoringTuningDef : Def
    {
        private static ScoringTuningDef cached;

        public static ScoringTuningDef Active => cached ??= DefDatabase<ScoringTuningDef>.AllDefsListForReading.FirstOrFallback();

        /// <summary>Weight of the (0..1) average relevant skill level in the final score.</summary>
        public float skillWeight = 0.7f;

        /// <summary>Flat bonus for a minor passion in the work type's best relevant skill.</summary>
        public float minorPassionBonus = 0.15f;

        /// <summary>Flat bonus for a major passion in the work type's best relevant skill.</summary>
        public float majorPassionBonus = 0.3f;

        /// <summary>Score for work types with no relevant skills (Hauling, Cleaning, Firefighter...).</summary>
        public float noSkillWorkScore = 0.5f;

        /// <summary>
        /// How much a pawn's standing relative to the colony's best counts versus their absolute score
        /// (0 = absolute only, 1 = pure ranking). Relative standing is what spreads results across the
        /// whole priority range.
        /// </summary>
        public float relativeWeight = 0.5f;

        /// <summary>
        /// How strongly fit quality shifts a specialist's priority around the urgency baseline
        /// (0 = pure urgency, higher = more spread). Ignored for assignEveryone/pinPriority work.
        /// </summary>
        public float qualityWeight = 0.5f;

        /// <summary>
        /// Depth discount for reluctant (ideoligion-opposed) coverage assignments: scales their fit
        /// quality when computing the priority (0 = always the deepest tier).
        /// </summary>
        public float opposedWorkFactor = 0f;

        /// <summary>
        /// Whether opposed work may be assigned as a last resort when no willing pawn can cover it.
        /// False = never auto-assign opposed work, even if the job goes uncovered.
        /// </summary>
        public bool assignOpposedWhenNeeded = true;

        /// <summary>
        /// How strongly a work type's aptitude stats (WorkTypeStatsDef) shift a score (0 = ignore,
        /// 1 = multiply directly). Stats are neutral at 1.0, so factor = 1 + weight × (avgStat - 1).
        /// </summary>
        public float workStatWeight = 0.5f;

        /// <summary>Whether a current inspiration matching a work type's skills grants a score bonus.</summary>
        public bool inspirationBonusEnabled = true;

        /// <summary>
        /// Flat score bonus while a pawn is inspired for a skill the work type uses. Large by design —
        /// inspirations are temporary. Controls selection/ranking; inspirationUrgency controls priority.
        /// </summary>
        public float inspirationBonus = 0.5f;

        /// <summary>
        /// Priority urgency floor (0..1, 1 = top) for a pawn's inspired work type. The score bonus
        /// alone can't raise priority above the work type's own urgency ceiling, so the assigner lifts
        /// an inspired assignment to at least this urgency. 0.9 ≈ near-top.
        /// </summary>
        public float inspirationUrgency = 0.9f;
    }
}
