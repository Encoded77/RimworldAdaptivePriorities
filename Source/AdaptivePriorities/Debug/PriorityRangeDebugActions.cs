using LudeonTK;
using AdaptivePriorities.Core;
using Verse;

namespace AdaptivePriorities.Debug
{
    /// <summary>Dev-mode action to inspect the detected priority range.</summary>
    public static class PriorityRangeDebugActions
    {
        [DebugAction("Adaptive Priorities", "Log priority range", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogPriorityRange()
        {
            Log.Message($"[Adaptive Priorities] Effective priority range = {PriorityRangeService.HighestPriority}..{PriorityRangeService.LowestPriority}.");
        }
    }
}
