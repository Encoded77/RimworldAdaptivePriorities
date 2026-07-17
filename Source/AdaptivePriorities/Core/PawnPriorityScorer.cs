using RimWorld;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.Core
{
    /// <summary>
    /// The component values behind a <see cref="PawnPriorityScorer.Score(Pawn, WorkTypeDef)"/> result,
    /// for the debug report. Raw data rather than formatted text: the scorer runs the whole pawn x
    /// work-type matrix on every recalc, so filling this must not allocate.
    /// </summary>
    public struct ScoreBreakdown
    {
        /// <summary>Work type is disabled for this pawn; every other field is unset and the score is 0.</summary>
        public bool disabled;

        /// <summary>Scored off noSkillWorkScore (Hauling, Cleaning...), so the skill/passion fields are unset.</summary>
        public bool noRelevantSkills;

        public int relevantSkillCount;

        /// <summary>Summed level of the relevant skills the pawn can actually use.</summary>
        public float totalLevel;

        /// <summary>totalLevel / (relevantSkillCount * 20), the 0..1 skill term.</summary>
        public float averageLevel01;

        /// <summary>Best passion bonus across the relevant skills; negative for a bad passion.</summary>
        public float passionBonus;

        /// <summary>Skill that supplied passionBonus, or null when no skill counted.</summary>
        public SkillDef passionSkill;

        /// <summary>Passion on <see cref="passionSkill"/>; a modded passion is an out-of-range enum value.</summary>
        public Passion passion;

        /// <summary>WorkTypeStatsDef multiplier, 1 = neutral.</summary>
        public float statFactor;

        public float inspirationBonus;

        /// <summary>The score actually returned: 0 at worst, but not capped at 1.</summary>
        public float raw;
    }

    /// <summary>
    /// Computes a "how good is this pawn at this work type" score from skills, passions and work-speed
    /// stats. Weights come from ScoringTuningDef. Roughly 0..1 but deliberately uncapped:
    /// <see cref="PawnPriorityScorer.BlendWithColonyBest"/> divides by the colony's best, so a ceiling
    /// here would flatten the top pawns together and make that ratio a no-op. The blend clamps instead.
    ///
    /// Most trait/backstory disposition is already captured upstream: skillGains are baked into skill
    /// levels at generation, trait aptitudes are included by SkillRecord.Level, and hard work disables
    /// flow through WorkTypeIsDisabled. Remaining trait/gene/bionic modifiers come in via the work
    /// type's aptitude stats (WorkTypeStatsDef).
    /// </summary>
    public static class PawnPriorityScorer
    {
        /// <summary>Returns 0 for work the pawn cannot do at all. Not capped at 1; see the class remarks.</summary>
        public static float Score(Pawn pawn, WorkTypeDef workType) => Score(pawn, workType, out _);

        /// <summary>
        /// As <see cref="Score(Pawn, WorkTypeDef)"/>, also reporting the component values behind the
        /// result so the debug report can explain a score without reimplementing the formula.
        /// </summary>
        public static float Score(Pawn pawn, WorkTypeDef workType, out ScoreBreakdown breakdown)
        {
            breakdown = default;
            breakdown.statFactor = 1f;

            if (pawn.WorkTypeIsDisabled(workType))
            {
                breakdown.disabled = true;
                return 0f;
            }

            var relevantSkills = workType.relevantSkills;
            if (relevantSkills.NullOrEmpty() || pawn.skills == null)
            {
                breakdown.noRelevantSkills = true;
                breakdown.statFactor = WorkStatFactor(pawn, workType);
                breakdown.raw = ScoringConfig.NoSkillWorkScore * breakdown.statFactor;
                return breakdown.raw;
            }

            float totalLevel = 0f;
            // Below any real bonus so a lone negative (bad) passion still wins; reset to 0 if no skill counted.
            float bestPassionBonus = float.NegativeInfinity;
            SkillDef passionSkill = null;
            Passion passion = Passion.None;

            for (int i = 0; i < relevantSkills.Count; i++)
            {
                var record = pawn.skills.GetSkill(relevantSkills[i]);
                if (record == null || record.TotallyDisabled)
                    continue;

                totalLevel += record.Level;

                float bonus = PassionScoreService.BonusFor(record);
                if (bonus > bestPassionBonus)
                {
                    bestPassionBonus = bonus;
                    passionSkill = record.def;
                    passion = record.passion;
                }
            }

            if (float.IsNegativeInfinity(bestPassionBonus))
            {
                bestPassionBonus = 0f;
                passionSkill = null;
            }

            float averageLevel01 = totalLevel / (relevantSkills.Count * 20f);
            float statFactor = WorkStatFactor(pawn, workType);
            float inspiration = InspirationBonus(pawn, workType);
            float baseScore = ScoringConfig.SkillWeight * averageLevel01 + bestPassionBonus;
            float scored = baseScore * statFactor + inspiration;

            breakdown.relevantSkillCount = relevantSkills.Count;
            breakdown.totalLevel = totalLevel;
            breakdown.averageLevel01 = averageLevel01;
            breakdown.passionBonus = bestPassionBonus;
            breakdown.passionSkill = passionSkill;
            breakdown.passion = passion;
            breakdown.statFactor = statFactor;
            breakdown.inspirationBonus = inspiration;
            breakdown.raw = scored;
            return breakdown.raw;
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
        /// Score multiplier (1.0 = neutral) from the work type's aptitude stats (WorkTypeStatsDef),
        /// each measured against its own baseline so absolute stats (CarryingCapacity, MoveSpeed) and
        /// multiplier stats share one scale. Captures trait/gene/bionic modifiers the raw skill level
        /// misses. Returns 1 when the work type has no mapped stats.
        /// </summary>
        private static float WorkStatFactor(Pawn pawn, WorkTypeDef workType)
        {
            float weight = ScoringConfig.WorkStatWeight;
            if (weight <= 0f)
                return 1f;

            var config = WorkTypeStatsDef.For(workType);
            if (config.stats == null || config.stats.Count == 0)
                return 1f;

            float total = 0f;
            for (int i = 0; i < config.stats.Count; i++)
                total += SkillFreeStatValue(pawn, config.stats[i].stat) / config.stats[i].baseline;

            float avgStat = total / config.stats.Count;

            // A weightFactor above 1 can drive this negative for a badly-suited pawn; 0 means "no
            // aptitude", which BlendWithColonyBest already treats as unassignable.
            return Mathf.Max(0f, 1f + weight * config.weightFactor * (avgStat - 1f));
        }

        /// <summary>
        /// A stat's value with the pawn's own skill divided back out, leaving trait/gene/bionic/drug/
        /// health modifiers. Most work-speed stats scale with the very skill the score already weighs
        /// (PlantWorkSpeed is 0.08 + 0.115 x Plants), so using them raw charges a pawn twice for it.
        ///
        /// StatWorker applies skillNeedFactors as a plain multiplier over everything else, so dividing by
        /// the same factors is exact. skillNeedOffsets cannot be undone - they are added before the
        /// multiplications - so a stat using them still double-counts; <see cref="SkillDrivenByOffset"/>
        /// marks those for the debug report.
        /// </summary>
        private static float SkillFreeStatValue(Pawn pawn, StatDef stat)
        {
            float value = pawn.GetStatValue(stat);

            var factors = stat.skillNeedFactors;
            if (pawn.skills == null || factors.NullOrEmpty())
                return value;

            // GetStatValue is finalized, so a stat floored at its own minValue no longer carries the
            // skill factor we would divide by, and dividing anyway inflates it (MiningSpeed floors at 0.1
            // against a level-0 factor of 0.04, reading as 2.5x aptitude). A floored stat says nothing.
            if (value <= stat.minValue)
                return 1f;

            float skillFactor = 1f;
            for (int i = 0; i < factors.Count; i++)
                skillFactor *= factors[i].ValueFor(pawn);

            // A zero factor zeroes the stat too, leaving nothing to recover; call it neutral.
            return skillFactor > 0.0001f ? value / skillFactor : 1f;
        }

        /// <summary>
        /// Whether a stat folds skill in as an offset, which <see cref="SkillFreeStatValue"/> cannot
        /// divide out - so the stat still double-counts its skill.
        /// </summary>
        public static bool SkillDrivenByOffset(StatDef stat) => !stat.skillNeedOffsets.NullOrEmpty();

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
    }
}
