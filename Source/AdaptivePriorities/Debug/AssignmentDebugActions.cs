using System.Text;
using LudeonTK;
using AdaptivePriorities.Core;
using Verse;

namespace AdaptivePriorities.Debug
{
    /// <summary>
    /// Dev-mode assignment actions: apply now, and a dry-run that runs the full pipeline and logs the
    /// proposal (with a coverage check) without writing anything.
    /// </summary>
    public static class AssignmentDebugActions
    {
        [DebugAction("Adaptive Priorities", "Apply assignment now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ApplyAssignmentNow()
        {
            int changed = ColonyPriorityAssigner.ApplyProposal(Find.CurrentMap);
            Log.Message($"[Adaptive Priorities] Applied assignment: {changed} cell(s) changed.");
        }

        [DebugAction("Adaptive Priorities", "Log assignment dry-run", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogAssignmentDryRun()
        {
            var proposal = ColonyPriorityAssigner.ComputeProposal(Find.CurrentMap);
            var workTypes = WorkTypeDiscoveryService.GetAllWorkTypes();

            var sb = new StringBuilder();
            sb.AppendLine($"[Adaptive Priorities] Assignment dry-run ({proposal.Count} colonists, priority range {PriorityRangeService.HighestPriority}..{PriorityRangeService.LowestPriority}). No priorities were changed.");

            // Coverage summary: every work type should have at least one assigned worker unless nobody
            // in the colony is capable of it.
            foreach (var workType in workTypes)
            {
                bool anyAssigned = false;
                bool anyCapable = false;
                foreach (var kvp in proposal)
                {
                    if (!kvp.Value.TryGetValue(workType, out int p))
                        continue;
                    anyCapable = true;
                    anyAssigned |= p > 0;
                }

                if (!anyCapable)
                    sb.AppendLine($"  NOT COVERABLE: {workType.defName} - no colonist can do this work.");
                else if (!anyAssigned)
                    sb.AppendLine($"  NOT COVERED: {workType.defName} - capable colonists exist but none was assigned (this is a bug).");
            }

            foreach (var kvp in proposal)
            {
                var pawn = kvp.Key;
                sb.AppendLine($"{pawn.LabelShortCap}:");
                foreach (var workType in workTypes)
                {
                    if (!kvp.Value.TryGetValue(workType, out int proposed))
                    {
                        sb.AppendLine($"  {workType.defName,-24} DISABLED");
                        continue;
                    }

                    int current = pawn.workSettings?.GetPriority(workType) ?? -1;
                    string marker = proposed == current ? "" : "   <- differs";
                    sb.AppendLine($"  {workType.defName,-24} proposed={proposed} current={current}{marker}");
                }
            }

            Log.Message(sb.ToString());
        }
    }
}
