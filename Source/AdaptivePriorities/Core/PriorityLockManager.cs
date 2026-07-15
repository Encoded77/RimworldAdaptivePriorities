using Verse;

namespace AdaptivePriorities.Core
{
    /// <summary>
    /// Null-safe facade over the save's lock state (with no active game, queries report "not locked"
    /// and mutations no-op). Three tiers: a whole-pawn lock excludes a pawn from the assigner entirely,
    /// a whole-work-type lock freezes that column across the colony, and a per-cell lock freezes one
    /// (pawn, work type) value. The broader lock is checked first.
    /// </summary>
    public static class PriorityLockManager
    {
        private static AdaptivePrioritiesGameComponent Comp => AdaptivePrioritiesGameComponent.Current;

        // Cheap gates so the per-frame grid draw hooks can skip the whole scan when nothing is locked.
        public static bool AnyPawnLocks => Comp?.AnyPawnLocks ?? false;
        public static bool AnyWorkTypeLocks => Comp?.AnyWorkTypeLocks ?? false;
        public static bool AnyCellLocks => Comp?.AnyCellLocks ?? false;

        public static bool IsPawnLocked(Pawn pawn) => Comp?.IsPawnLocked(pawn) ?? false;

        public static bool IsWorkTypeLocked(WorkTypeDef workType) => Comp?.IsWorkTypeLocked(workType) ?? false;

        public static bool IsCellLocked(Pawn pawn, WorkTypeDef workType) => Comp?.IsCellLocked(pawn, workType) ?? false;

        public static void SetPawnLocked(Pawn pawn, bool locked) => Comp?.SetPawnLocked(pawn, locked);

        public static void SetWorkTypeLocked(WorkTypeDef workType, bool locked) => Comp?.SetWorkTypeLocked(workType, locked);

        public static void SetCellLocked(Pawn pawn, WorkTypeDef workType, bool locked) => Comp?.SetCellLocked(pawn, workType, locked);
    }
}
