using Verse;

namespace AdaptivePriorities.Core
{
    /// <summary>Resolved per-work-type policy: the XML/derived default with player overrides layered on.</summary>
    public struct EffectiveWorkPolicy
    {
        public float urgency;
        public bool assignEveryone;
        public int minWorkers;
        public float maxWorkersFraction;
        public float scoreCutoff;
        public bool pinPriority;
        public bool priorityFalloff;
    }

    /// <summary>
    /// Resolves the effective policy for a work type: WorkTypePolicyDef.For (explicit XML or derived)
    /// with any settings-tab overrides applied. The field-name constants are the override keys.
    /// </summary>
    public static class WorkPolicyConfig
    {
        public const string UrgencyKey = "urgency";
        public const string AssignEveryoneKey = "assignEveryone";
        public const string MinWorkersKey = "minWorkers";
        public const string MaxWorkersFractionKey = "maxWorkersFraction";
        public const string ScoreCutoffKey = "scoreCutoff";
        public const string PinPriorityKey = "pinPriority";

        public static EffectiveWorkPolicy For(WorkTypeDef workType)
        {
            var def = WorkTypePolicyDef.For(workType);
            var s = AdaptivePrioritiesMod.Settings;

            if (s == null)
            {
                return new EffectiveWorkPolicy
                {
                    urgency = def.urgency,
                    assignEveryone = def.assignEveryone,
                    minWorkers = def.minWorkers,
                    maxWorkersFraction = def.maxWorkersFraction,
                    scoreCutoff = def.scoreCutoff,
                    pinPriority = def.pinPriority,
                    priorityFalloff = def.priorityFalloff,
                };
            }

            return new EffectiveWorkPolicy
            {
                urgency = s.GetFloat(AdaptivePrioritiesSettings.PolicyKey(UrgencyKey, workType), def.urgency),
                assignEveryone = s.GetBool(AdaptivePrioritiesSettings.PolicyKey(AssignEveryoneKey, workType), def.assignEveryone),
                minWorkers = s.GetInt(AdaptivePrioritiesSettings.PolicyKey(MinWorkersKey, workType), def.minWorkers),
                maxWorkersFraction = s.GetFloat(AdaptivePrioritiesSettings.PolicyKey(MaxWorkersFractionKey, workType), def.maxWorkersFraction),
                scoreCutoff = s.GetFloat(AdaptivePrioritiesSettings.PolicyKey(ScoreCutoffKey, workType), def.scoreCutoff),
                pinPriority = s.GetBool(AdaptivePrioritiesSettings.PolicyKey(PinPriorityKey, workType), def.pinPriority),
                priorityFalloff = def.priorityFalloff,
            };
        }
    }
}
