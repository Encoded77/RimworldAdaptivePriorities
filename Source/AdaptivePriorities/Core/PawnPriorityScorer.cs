using RimWorld;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.Core
{
    /// <summary>
    /// Computes a normalized 0..1 "how good is this pawn at this work type" score from skills,
    /// passions and work-speed stats. Weights come from ScoringTuningDef.
    ///
    /// Most trait/backstory disposition is already captured upstream: skillGains are baked into skill
    /// levels at generation, trait aptitudes are included by SkillRecord.Level, and hard work disables
    /// flow through WorkTypeIsDisabled. Remaining trait/gene/bionic modifiers come in via the work
    /// type's aptitude stats (WorkTypeStatsDef).
    /// </summary>
    public static class PawnPriorityScorer
    {
        // Vanilla learn-rate anchors (no passion / major passion) used to map modded passions onto the
        // bonus scale, since they extend the Passion enum with values a switch would misread.
        private const float NoPassionLearnRate = 0.35f;
        private const float MajorPassionLearnRate = 1.5f;

        /// <summary>Returns 0 for work the pawn cannot do at all.</summary>
        public static float Score(Pawn pawn, WorkTypeDef workType)
        {
            if (pawn.WorkTypeIsDisabled(workType))
                return 0f;

            var relevantSkills = workType.relevantSkills;
            if (relevantSkills.NullOrEmpty() || pawn.skills == null)
                return Mathf.Clamp01(ScoringConfig.NoSkillWorkScore * WorkStatFactor(pawn, workType));

            float totalLevel = 0f;
            float bestPassionBonus = 0f;

            for (int i = 0; i < relevantSkills.Count; i++)
            {
                var record = pawn.skills.GetSkill(relevantSkills[i]);
                if (record == null || record.TotallyDisabled)
                    continue;

                totalLevel += record.Level;
                bestPassionBonus = Mathf.Max(bestPassionBonus, PassionBonus(record));
            }

            float averageLevel01 = totalLevel / (relevantSkills.Count * 20f);
            float baseScore = ScoringConfig.SkillWeight * averageLevel01 + bestPassionBonus;
            float scored = baseScore * WorkStatFactor(pawn, workType) + InspirationBonus(pawn, workType);
            return Mathf.Clamp01(scored);
        }

        /// <summary>Flat score bonus while the pawn is inspired for a skill this work type uses, so the recalc routes the work to them.</summary>
        private static float InspirationBonus(Pawn pawn, WorkTypeDef workType)
        {
            if (ScoringConfig.InspirationBonus <= 0f || !IsInspiredFor(pawn, workType))
                return 0f;

            return ScoringConfig.InspirationBonus;
        }

        /// <summary>
        /// Whether the pawn has an inspiration boosting a skill this work type uses, detected by
        /// intersecting the inspiration's associatedSkills with the work type's relevantSkills (so
        /// modded inspirations work with no config). The assigner uses this to lift the priority, not
        /// just the score, so the pawn actually drops other work.
        /// </summary>
        public static bool IsInspiredFor(Pawn pawn, WorkTypeDef workType)
        {
            if (!ScoringConfig.InspirationBonusEnabled || !pawn.Inspired)
                return false;

            var associatedSkills = pawn.InspirationDef?.associatedSkills;
            var relevantSkills = workType.relevantSkills;
            if (associatedSkills.NullOrEmpty() || relevantSkills.NullOrEmpty())
                return false;

            for (int i = 0; i < associatedSkills.Count; i++)
            {
                if (relevantSkills.Contains(associatedSkills[i]))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a pawn can currently be given work. Downed, mentally-broken and suspended pawns are
        /// excluded so their work redistributes to able colonists; their existing priorities are left
        /// untouched until they recover.
        /// </summary>
        public static bool CanBeAssigned(Pawn pawn)
        {
            return pawn != null
                   && !pawn.Dead
                   && !pawn.Downed
                   && !pawn.InMentalState
                   && !pawn.Suspended
                   && pawn.workSettings != null
                   && pawn.workSettings.EverWork;
        }

        /// <summary>
        /// Score multiplier (1.0 = neutral) from the average of the work type's aptitude stats
        /// (WorkTypeStatsDef), capturing trait/gene/bionic modifiers the raw skill level misses.
        /// Returns 1 when the work type has no mapped stats.
        /// </summary>
        private static float WorkStatFactor(Pawn pawn, WorkTypeDef workType)
        {
            float weight = ScoringConfig.WorkStatWeight;
            if (weight <= 0f)
                return 1f;

            var stats = WorkTypeStatsDef.StatsFor(workType);
            if (stats == null || stats.Count == 0)
                return 1f;

            float total = 0f;
            for (int i = 0; i < stats.Count; i++)
                total += pawn.GetStatValue(stats[i]);

            float avgStat = total / stats.Count;
            return 1f + weight * (avgStat - 1f);
        }

        /// <summary>
        /// Ideoligion-opposed work (PreceptDef.opposedWorkTypes) is legal but mood-punished, and not
        /// covered by WorkTypeIsDisabled. Deliberately not folded into the score: zeroing it would
        /// destroy the skill ordering among opposed pawns, making an all-opposed colony's coverage pick
        /// arbitrary. The assigner uses this flag to rank opposed pawns last and assign them only when
        /// coverage requires it.
        /// </summary>
        public static bool IdeoOpposes(Pawn pawn, WorkTypeDef workType)
        {
            var ideo = pawn.Ideo;
            if (ideo == null)
                return false;

            var precepts = ideo.PreceptsListForReading;
            for (int i = 0; i < precepts.Count; i++)
            {
                var opposed = precepts[i].def.opposedWorkTypes;
                if (opposed != null && opposed.Contains(workType))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Blends a pawn's absolute score with their standing relative to the colony's best at the same
        /// work type. Absolute scores cluster low, so relative standing is what lets "our best cook"
        /// reach top cooking priority even in a colony of mediocre cooks.
        /// </summary>
        public static float BlendWithColonyBest(float rawScore, float colonyBestScore)
        {
            if (rawScore <= 0f)
                return 0f;

            float relative = colonyBestScore > 0f ? rawScore / colonyBestScore : 0f;
            float w = ScoringConfig.RelativeWeight;
            return Mathf.Clamp01((1f - w) * rawScore + w * relative);
        }

        private static float PassionBonus(SkillRecord record)
        {
            switch (record.passion)
            {
                case Passion.None:
                    return 0f;
                case Passion.Minor:
                    return ScoringConfig.MinorPassionBonus;
                case Passion.Major:
                    return ScoringConfig.MajorPassionBonus;
                default:
                    // Modded passion: scale its learn rate between the vanilla none/major anchors.
                    float t = Mathf.InverseLerp(NoPassionLearnRate, MajorPassionLearnRate, record.LearnRateFactor());
                    return Mathf.Clamp01(t) * ScoringConfig.MajorPassionBonus;
            }
        }
    }
}
