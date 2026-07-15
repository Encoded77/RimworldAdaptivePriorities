using System.Collections.Generic;
using AdaptivePriorities.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace AdaptivePriorities
{
    /// <summary>A single locked (pawn, work type) cell. Scribed by reference so it survives save/load.</summary>
    public class CellLock : IExposable
    {
        public Pawn pawn;
        public WorkTypeDef workType;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Defs.Look(ref workType, "workType");
        }
    }

    /// <summary>
    /// Per-save state: the player's locks and the auto-mode flag. This is gameplay state, not a
    /// global preference, so it lives in a GameComponent rather than ModSettings.
    /// </summary>
    public class AdaptivePrioritiesGameComponent : GameComponent
    {
        /// <summary>When on, assignment re-runs on a fixed interval (see settings). Toggled from the Work tab.</summary>
        public bool autoMode;

        // Not scribed: reset on load so a freshly loaded colony gets one recalc after the interval
        // rather than immediately mid-load.
        private int lastAutoRecalcTick = -1;

        // Scribed as lists so Scribe_Collections resolves the pawn references / defs directly. Pawn and
        // work-type lock lists stay tiny, so linear Contains is fine even from the per-frame grid draw;
        // cell locks can grow, so they get a parallel index below.
        private List<Pawn> lockedPawns = new List<Pawn>();
        private List<CellLock> lockedCells = new List<CellLock>();
        private List<WorkTypeDef> lockedWorkTypes = new List<WorkTypeDef>();

        // O(1) cell-lock lookup mirroring lockedCells, rebuilt on load and kept in sync on edit, so
        // IsCellLocked doesn't scan the whole list on every cell every frame.
        private readonly Dictionary<Pawn, HashSet<WorkTypeDef>> lockedCellIndex = new Dictionary<Pawn, HashSet<WorkTypeDef>>();

        public AdaptivePrioritiesGameComponent(Game game)
        {
        }

        // GetComponent is a linear scan of the game's components; the grid calls Current for every lock
        // check per cell per frame, so cache it and refresh only when the active game changes.
        private static Game cachedGame;
        private static AdaptivePrioritiesGameComponent cached;

        public static AdaptivePrioritiesGameComponent Current
        {
            get
            {
                var game = Verse.Current.Game;
                if (game != cachedGame)
                {
                    cachedGame = game;
                    cached = game?.GetComponent<AdaptivePrioritiesGameComponent>();
                }
                return cached;
            }
        }

        // Cheap "is anything locked" gates so the grid's per-cell draw hooks can bail immediately when
        // nothing of that kind is locked.
        public bool AnyPawnLocks => lockedPawns.Count > 0;
        public bool AnyWorkTypeLocks => lockedWorkTypes.Count > 0;
        public bool AnyCellLocks => lockedCells.Count > 0;

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            // Auto mode is on for a fresh colony ("install and it just works"); the player can still
            // turn it off per-save. Assign once immediately so starting colonists get priorities from
            // tick one instead of waiting out an interval.
            autoMode = true;
            RunAutoRecalc();
        }

        // Auto mode is just a periodic scan: cheap every tick (one compare), real work once per
        // interval. No per-event hooks — the coarse interval both covers every trigger source and
        // keeps transient states (short mental breaks, inspirations) from causing recalc thrash.
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (!autoMode)
                return;

            int now = Find.TickManager.TicksGame;
            if (lastAutoRecalcTick < 0)
            {
                lastAutoRecalcTick = now;
                return;
            }

            // Floor at one hour so a misconfigured setting can't recalc every tick.
            int interval = Mathf.Max(GenDate.TicksPerHour, AdaptivePrioritiesMod.Settings?.autoRecalcIntervalTicks ?? GenDate.TicksPerHour);
            if (now - lastAutoRecalcTick < interval)
                return;

            RunAutoRecalc();
        }

        /// <summary>Applies assignment to every player-home map now and restarts the interval timer.</summary>
        public void RunAutoRecalc()
        {
            lastAutoRecalcTick = Find.TickManager.TicksGame;
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
                if (maps[i].IsPlayerHome)
                    ColonyPriorityAssigner.ApplyProposal(maps[i]);
        }

        public bool IsPawnLocked(Pawn pawn) => lockedPawns.Contains(pawn);

        public bool IsWorkTypeLocked(WorkTypeDef workType) => lockedWorkTypes.Contains(workType);

        public void SetWorkTypeLocked(WorkTypeDef workType, bool locked)
        {
            if (locked)
            {
                if (!lockedWorkTypes.Contains(workType))
                    lockedWorkTypes.Add(workType);
            }
            else
            {
                lockedWorkTypes.Remove(workType);
            }
        }

        public bool IsCellLocked(Pawn pawn, WorkTypeDef workType) =>
            lockedCellIndex.TryGetValue(pawn, out var set) && set.Contains(workType);

        public void SetPawnLocked(Pawn pawn, bool locked)
        {
            if (locked)
            {
                if (!lockedPawns.Contains(pawn))
                    lockedPawns.Add(pawn);
            }
            else
            {
                lockedPawns.Remove(pawn);
            }
        }

        public void SetCellLocked(Pawn pawn, WorkTypeDef workType, bool locked)
        {
            lockedCells.RemoveAll(c => c.pawn == pawn && c.workType == workType);
            if (locked)
                lockedCells.Add(new CellLock { pawn = pawn, workType = workType });

            // Keep the fast index in sync with the scribed list.
            if (locked)
            {
                if (!lockedCellIndex.TryGetValue(pawn, out var set))
                    lockedCellIndex[pawn] = set = new HashSet<WorkTypeDef>();
                set.Add(workType);
            }
            else if (lockedCellIndex.TryGetValue(pawn, out var set))
            {
                set.Remove(workType);
                if (set.Count == 0)
                    lockedCellIndex.Remove(pawn);
            }
        }

        private void RebuildCellIndex()
        {
            lockedCellIndex.Clear();
            for (int i = 0; i < lockedCells.Count; i++)
            {
                var c = lockedCells[i];
                if (c?.pawn == null || c.workType == null)
                    continue;
                if (!lockedCellIndex.TryGetValue(c.pawn, out var set))
                    lockedCellIndex[c.pawn] = set = new HashSet<WorkTypeDef>();
                set.Add(c.workType);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref autoMode, "autoMode", false);
            Scribe_Collections.Look(ref lockedPawns, "lockedPawns", LookMode.Reference);
            Scribe_Collections.Look(ref lockedCells, "lockedCells", LookMode.Deep);
            Scribe_Collections.Look(ref lockedWorkTypes, "lockedWorkTypes", LookMode.Def);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lockedPawns ??= new List<Pawn>();
                lockedCells ??= new List<CellLock>();
                lockedWorkTypes ??= new List<WorkTypeDef>();
                // Drop locks whose pawn or work type no longer exists (dead/removed, or a work type
                // from a since-removed mod).
                lockedPawns.RemoveAll(p => p == null);
                lockedCells.RemoveAll(c => c == null || c.pawn == null || c.workType == null);
                lockedWorkTypes.RemoveAll(w => w == null);
                RebuildCellIndex();
            }
        }
    }
}
