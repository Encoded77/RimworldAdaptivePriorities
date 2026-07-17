using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.Core
{
    /// <summary>
    /// One (pawn, work type) cell of an assignment run, with every input its decision used. Produced
    /// only when <see cref="ColonyPriorityAssigner.ComputeProposal"/> is given a trace list.
    /// </summary>
    public struct AssignmentTrace
    {
        public Pawn pawn;
        public WorkTypeDef workType;
        public EffectiveWorkPolicy policy;
        public ScoreBreakdown breakdown;

        /// <summary>Best raw score across capable pawns for this work type; the blend's denominator.</summary>
        public float colonyBest;

        /// <summary>Raw score blended with colony-relative standing; what ranking and the cutoff use.</summary>
        public float blended;

        /// <summary>0-based rank among capable pawns, opposed pawns sorted last.</summary>
        public int rank;

        public int capableCount;
        public int maxWorkers;
        public bool opposed;
        public bool inspired;
        public bool assigned;
        public bool cellLocked;

        /// <summary>Priority position before mapping onto the range; unset when not assigned.</summary>
        public float normalized;

        public int falloff;
        public int proposed;
        public int current;

        /// <summary>The single rule that decided this cell.</summary>
        public string reason;
    }

    /// <summary>
    /// Colony-wide assignment: scores the whole pawn x work-type matrix at once. Per work type,
    /// capable pawns are ranked by blended score, then the WorkTypePolicyDef decides who is assigned
    /// and at what priority, with minWorkers guaranteeing coverage even when everyone is a poor fit.
    /// </summary>
    public static class ColonyPriorityAssigner
    {
        // Quality at which a specialist's priority equals the work type's urgency: above it quality
        // lifts priority above urgency, below it deepens it. 0.5 is the middle of the 0..1 range.
        private const float QualityPivot = 0.5f;

        /// <summary>
        /// Computes and writes the proposal, diff-writing only changed cells. Returns the number of
        /// changed cells.
        /// </summary>
        public static int ApplyProposal(Map map)
        {
            // Numeric priorities only mean anything in manual-priorities mode; the checkbox mode
            // collapses everything to on/off at priority 3.
            if (!Find.PlaySettings.useWorkPriorities)
                Find.PlaySettings.useWorkPriorities = true;

            int changed = 0;
            foreach (var pawnEntry in ComputeProposal(map))
            {
                var pawn = pawnEntry.Key;
                foreach (var cell in pawnEntry.Value)
                {
                    if (pawn.workSettings.GetPriority(cell.Key) == cell.Value)
                        continue;

                    pawn.workSettings.SetPriority(cell.Key, cell.Value);
                    changed++;
                }
            }

            return changed;
        }

        /// <summary>
        /// Proposed priority (0 = unassigned) for every colonist and work type on the map. Pass
        /// <paramref name="trace"/> to collect a per-cell explanation; it is filled only when non-null,
        /// so the normal recalc path builds no strings.
        /// </summary>
        public static Dictionary<Pawn, Dictionary<WorkTypeDef, int>> ComputeProposal(Map map, List<AssignmentTrace> trace = null)
        {
            var workTypes = WorkTypeDiscoveryService.GetAllWorkTypes();
            var colonists = WorkTypeDiscoveryService.GetColonistsOnMap(map);

            // Only able-bodied, unlocked pawns take part. Excluded pawns get no proposal entry, so
            // their existing priorities are left untouched.
            var proposal = new Dictionary<Pawn, Dictionary<WorkTypeDef, int>>();
            foreach (var pawn in colonists)
            {
                if (PawnPriorityScorer.CanBeAssigned(pawn) && !PriorityLockManager.IsPawnLocked(pawn))
                    proposal[pawn] = new Dictionary<WorkTypeDef, int>();
            }

            var capable = new List<(Pawn pawn, float score, bool opposed, bool inspired, ScoreBreakdown breakdown)>();

            foreach (var workType in workTypes)
            {
                // A locked work type is frozen colony-wide: skip the whole column.
                if (PriorityLockManager.IsWorkTypeLocked(workType))
                    continue;

                capable.Clear();
                float colonyBest = 0f;
                foreach (var pawn in proposal.Keys)
                {
                    if (pawn.WorkTypeIsDisabled(workType))
                        continue;

                    float raw = PawnPriorityScorer.Score(pawn, workType, out var breakdown);
                    colonyBest = Mathf.Max(colonyBest, raw);
                    capable.Add((pawn, raw, PawnPriorityScorer.IdeoOpposes(pawn, workType), PawnPriorityScorer.IsInspiredFor(pawn, workType), breakdown));
                }

                if (capable.Count == 0)
                    continue;

                var policy = WorkPolicyConfig.For(workType);

                // Blend raw scores against the colony's best, then rank. Opposed pawns sort behind all
                // willing ones regardless of skill, but keep their skill ordering among themselves so a
                // coverage pick in an all-opposed colony still selects the best fit.
                for (int i = 0; i < capable.Count; i++)
                    capable[i] = (capable[i].pawn, PawnPriorityScorer.BlendWithColonyBest(capable[i].score, colonyBest), capable[i].opposed, capable[i].inspired, capable[i].breakdown);
                capable.SortByDescending(c => (c.opposed ? 0f : 10f) + c.score);

                int maxWorkers = policy.assignEveryone
                    ? capable.Count
                    : Mathf.Max(policy.minWorkers, Mathf.CeilToInt(policy.maxWorkersFraction * capable.Count));

                for (int rank = 0; rank < capable.Count; rank++)
                {
                    var (pawn, score, opposed, inspired, breakdown) = capable[rank];

                    // Opposed pawns are assigned only when the coverage guarantee has no willing pawn
                    // left, and only if the player allows opposed-work assignment at all.
                    bool opposedCoverage = ScoringConfig.AssignOpposedWhenNeeded && rank < policy.minWorkers;
                    bool withinMaxWorkers = rank < maxWorkers;
                    bool meetsBar = policy.assignEveryone || score >= policy.scoreCutoff || rank < policy.minWorkers;
                    bool willing = !opposed || opposedCoverage;
                    bool assigned = withinMaxWorkers && meetsBar && willing;

                    float normalizedTrace = 0f;
                    int falloffTrace = 0;
                    int priority = 0;
                    if (assigned)
                    {
                        // Pinned (life-critical) and everyone-jobs go to exactly the urgency priority.
                        // Otherwise a specialist's priority pivots around urgency by fit quality: an
                        // above-average pawn is lifted above urgency, a below-average one deepened
                        // below it. A reluctant (opposed) assignment is discounted first so it lands deep.
                        float normalized;
                        int falloff = 0;
                        if ((policy.pinPriority || policy.assignEveryone) && !opposed)
                        {
                            normalized = policy.urgency;
                        }
                        else
                        {
                            float effectiveScore = opposed ? score * ScoringConfig.OpposedWorkFactor : score;
                            normalized = policy.urgency + ScoringConfig.QualityWeight * (effectiveScore - QualityPivot);
                            falloff = policy.priorityFalloff ? rank : 0;
                        }

                        // An inspired pawn's work becomes urgent regardless of the work type's own low
                        // urgency ceiling, so they use the inspiration before it expires.
                        if (inspired)
                            normalized = Mathf.Max(normalized, ScoringConfig.InspirationUrgency);

                        priority = Mathf.Clamp(PriorityRangeService.FromNormalized(normalized) + falloff, PriorityRangeService.HighestPriority, PriorityRangeService.LowestPriority);
                        normalizedTrace = normalized;
                        falloffTrace = falloff;
                    }

                    // A locked cell keeps its current value: propose that so the diff-write skips it.
                    bool cellLocked = PriorityLockManager.IsCellLocked(pawn, workType);
                    if (cellLocked)
                        priority = pawn.workSettings.GetPriority(workType);

                    proposal[pawn][workType] = priority;

                    trace?.Add(new AssignmentTrace
                    {
                        pawn = pawn,
                        workType = workType,
                        policy = policy,
                        breakdown = breakdown,
                        colonyBest = colonyBest,
                        blended = score,
                        rank = rank,
                        capableCount = capable.Count,
                        maxWorkers = maxWorkers,
                        opposed = opposed,
                        inspired = inspired,
                        assigned = assigned,
                        cellLocked = cellLocked,
                        normalized = normalizedTrace,
                        falloff = falloffTrace,
                        proposed = priority,
                        current = pawn.workSettings?.GetPriority(workType) ?? -1,
                        reason = trace == null
                            ? null
                            : DescribeDecision(policy, score, rank, maxWorkers, opposed, opposedCoverage,
                                               withinMaxWorkers, meetsBar, willing, inspired, cellLocked),
                    });
                }
            }

            return proposal;
        }

        /// <summary>
        /// Names the single rule that decided a cell, mirroring the branch order of the assignment
        /// condition above. Debug-only: allocates, so it is called only while tracing.
        /// </summary>
        private static string DescribeDecision(EffectiveWorkPolicy policy, float score, int rank, int maxWorkers,
                                               bool opposed, bool opposedCoverage, bool withinMaxWorkers,
                                               bool meetsBar, bool willing, bool inspired, bool cellLocked)
        {
            if (cellLocked)
                return "LOCKED cell: kept current priority";

            string suffix = inspired ? " +inspired(urgency floor)" : "";

            if (!withinMaxWorkers)
                return $"cut: rank {rank + 1} over maxWorkers {maxWorkers}";
            if (!meetsBar)
                return $"cut: blend {score:0.000} under cutoff {policy.scoreCutoff:0.00}";
            if (!willing)
                return opposedCoverage
                    ? "cut: ideo-opposed"
                    : "cut: ideo-opposed, not needed for coverage";

            if (policy.assignEveryone)
                return "kept: everyone-job" + suffix;
            if (opposed)
                return $"kept: opposed, taken for coverage (rank {rank + 1} within minWorkers {policy.minWorkers})" + suffix;
            if (score >= policy.scoreCutoff)
                return $"kept: blend {score:0.000} over cutoff {policy.scoreCutoff:0.00}" + suffix;
            return $"kept: coverage (rank {rank + 1} within minWorkers {policy.minWorkers})" + suffix;
        }
    }
}
