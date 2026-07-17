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

        /// <summary>
        /// Weight of the (0..1) average relevant skill level in the final score; at 1 the skill term is
        /// the average level as a fraction of 20. Also sets the scale everything else is read against:
        /// a passion bonus of B is worth B x 20 / skillWeight skill levels.
        /// </summary>
        public float skillWeight = 1f;

        /// <summary>Flat bonus for a minor passion in the work type's best relevant skill.</summary>
        public float minorPassionBonus = 0.15f;

        /// <summary>Flat bonus for a major passion in the work type's best relevant skill.</summary>
        public float majorPassionBonus = 0.3f;

        /// <summary>How far below no-passion an undesirable (isBad) modded passion scores — VSE apathy,
        /// Alpha dunce, etc. 0 = neutral. Vanilla passions are unaffected.</summary>
        public float badPassionPenalty = 0.1f;

        /// <summary>
        /// Ceiling on the bonus the modded-passion learn-rate curve may produce. The curve extrapolates
        /// unclamped, so without this VSE_Critical (3x) reads 0.75 and out-values the whole skill term,
        /// letting an unskilled enthusiast outrank a master. Keep below skillWeight. Explicit
        /// PassionScoreDefs are deliberate data and are not capped.
        /// </summary>
        public float maxPassionBonus = 0.45f;

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

        /// <summary>
        /// Whether the minimum-worker coverage guarantee is enforced. When false, every work type's
        /// minWorkers resolves to 0: nobody is force-assigned to work everyone is bad at, and jobs may
        /// go completely uncovered (including Doctor in a small colony).
        /// </summary>
        public bool coverageGuaranteeEnabled = true;

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
