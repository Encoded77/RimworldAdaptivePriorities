using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.Core
{
    /// <summary>The external-worker reduction resolved for one work type on one recalc.</summary>
    public struct ExternalAdjustment
    {
        /// <summary>Weighted colonist-equivalent capacity from automatons, before the uptime factor.</summary>
        public float capacity;

        public float uptime;
        public ExternalOffloadMode mode;

        /// <summary>Colonist-equivalents removed from demand: Floor(capacity * uptime), or 0 when inactive.</summary>
        public int reduction;

        /// <summary>Lowest colonist count the reduction may leave: backup under Reduce, 0 under Full.</summary>
        public int floor;
    }

    /// <summary>
    /// Counts the colony's external workers - Biotech/Alpha mechs (generic, via
    /// RaceProps.mechEnabledWorkTypes) and data-driven non-mech sources (VQE drones, via
    /// ExternalWorkerSourceDef) - and turns that into a per-work-type reduction the assigner applies to
    /// colonist demand. Availability is loose: any spawned, player-faction automaton that is not dead or
    /// downed. Capacity is weighted by each automaton's work-speed stats against the same
    /// WorkTypeStatsDef baselines the scorer uses, then scaled by the work type's uptime factor and
    /// floored, so a full-time automaton can be worth more than one colonist on fragmented menial work.
    /// </summary>
    public static class ExternalWorkerService
    {
        /// <summary>Settings category for the generic mech path (drones use their source def's category).</summary>
        public const string MechCategory = "Mechs";

        private static HashSet<WorkTypeDef> mechCapableWorkTypes;

        /// <summary>
        /// Weighted external capacity per work type on this map. Enumerates automatons once; returns an
        /// empty dict when nothing contributes, so the common no-automaton colony pays almost nothing.
        /// </summary>
        public static Dictionary<WorkTypeDef, float> BuildCapacity(Map map)
        {
            var result = new Dictionary<WorkTypeDef, float>();
            if (map?.mapPawns == null)
                return result;

            var player = Faction.OfPlayer;

            // Mechs: generic, covers Biotech + any mech mod. Combat-only mechs have an
            // empty mechEnabledWorkTypes and drop out for free.
            if (ScoringConfig.AccountForCategory(MechCategory))
            {
                var mechs = map.mapPawns.SpawnedColonyMechs;
                for (int i = 0; i < mechs.Count; i++)
                {
                    var mech = mechs[i];
                    if (!Available(mech, player))
                        continue;
                    var workTypes = mech.RaceProps?.mechEnabledWorkTypes;
                    if (workTypes.NullOrEmpty())
                        continue;
                    for (int j = 0; j < workTypes.Count; j++)
                    {
                        var wt = workTypes[j];
                        if (MechCapable(wt))
                            Accumulate(result, wt, WeightFor(mech, wt));
                    }
                }
            }

            // Non-mech sources (VQE drones etc.) from the data-driven mapping. Drones are ordinary pawns,
            // not mechanoids, so they live in AllPawnsSpawned rather than SpawnedColonyMechs.
            var sources = ExternalWorkerSourceDef.Resolved;
            if (sources.Count > 0)
            {
                var pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    var pawn = pawns[i];
                    if (!Available(pawn, player))
                        continue;
                    if (!sources.TryGetValue(pawn.def, out var source))
                        continue;
                    if (!ScoringConfig.AccountForCategory(source.category))
                        continue;
                    var workTypes = source.workTypes;
                    for (int j = 0; j < workTypes.Count; j++)
                        Accumulate(result, workTypes[j], WeightFor(pawn, workTypes[j]));
                }
            }

            return result;
        }

        /// <summary>The reduction and floor to apply to one work type, from its capacity and policy.</summary>
        public static ExternalAdjustment AdjustmentFor(WorkTypeDef workType, Dictionary<WorkTypeDef, float> capacity)
        {
            var adj = new ExternalAdjustment { uptime = 1f, mode = ExternalOffloadMode.Off };
            if (capacity == null || !capacity.TryGetValue(workType, out var cap) || cap <= 0f)
                return adj;

            var policy = WorkPolicyConfig.For(workType);
            adj.capacity = cap;
            adj.mode = policy.externalOffload;
            adj.uptime = policy.externalUptimeFactor;
            if (adj.mode == ExternalOffloadMode.Off)
                return adj;

            adj.reduction = Mathf.FloorToInt(cap * Mathf.Max(0f, adj.uptime));
            adj.floor = adj.mode == ExternalOffloadMode.Full ? 0 : Mathf.Max(0, policy.externalBackup);
            return adj;
        }

        private static bool Available(Pawn pawn, Faction player) =>
            pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed && pawn.Faction == player;

        private static void Accumulate(Dictionary<WorkTypeDef, float> map, WorkTypeDef workType, float weight)
        {
            if (weight <= 0f)
                return;
            map.TryGetValue(workType, out var current);
            map[workType] = current + weight;
        }

        /// <summary>
        /// One automaton's colonist-equivalent throughput on a work type: its work-speed stats measured
        /// against the WorkTypeStatsDef baselines (the same yardstick the scorer uses for colonists),
        /// averaged. Uses the raw stat value - a mech's fixed skill is part of its real output and there
        /// is no colonist skill to divide out here. Falls back to 1 when the work type has no mapped stats.
        /// </summary>
        private static float WeightFor(Pawn automaton, WorkTypeDef workType)
        {
            var config = WorkTypeStatsDef.For(workType);
            if (config.stats == null || config.stats.Count == 0)
                return 1f;

            float total = 0f;
            for (int i = 0; i < config.stats.Count; i++)
                total += automaton.GetStatValue(config.stats[i].stat) / config.stats[i].baseline;

            return Mathf.Max(0f, total / config.stats.Count);
        }

        private static bool MechCapable(WorkTypeDef workType)
        {
            if (mechCapableWorkTypes == null)
                BuildMechCapable();
            return mechCapableWorkTypes.Contains(workType);
        }

        // A work type mechs can actually perform: at least one of its workgivers is flagged
        // canBeDoneByMechs. A race's mechEnabledWorkTypes can list a type whose givers are all
        // colonist-only, which this filters out.
        private static void BuildMechCapable()
        {
            mechCapableWorkTypes = new HashSet<WorkTypeDef>();
            foreach (var wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                var givers = wt.workGiversByPriority;
                if (givers == null)
                    continue;
                for (int i = 0; i < givers.Count; i++)
                {
                    if (givers[i].canBeDoneByMechs)
                    {
                        mechCapableWorkTypes.Add(wt);
                        break;
                    }
                }
            }
        }
    }
}
