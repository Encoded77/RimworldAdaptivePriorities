using Verse;

namespace AdaptivePriorities
{
    /// <summary>
    /// Optional per-passion score override. Modded passions already get a bonus derived from their own
    /// learn rate (<see cref="AdaptivePriorities.Core.PassionScoreService"/>); this pins an exact value
    /// for one passion instead. Pure XML, from any mod, no assembly reference:
    ///
    ///   <AdaptivePriorities.PassionScoreDef>
    ///     <defName>AP_Score_VSE_Critical</defName>
    ///     <passionDef>VSE_Critical</passionDef>
    ///     <bonus>0.6</bonus>
    ///   </AdaptivePriorities.PassionScoreDef>
    /// </summary>
    public class PassionScoreDef : Def
    {
        /// <summary>defName of the passion (a VSE/Alpha PassionDef) this override applies to.</summary>
        public string passionDef;

        /// <summary>Flat bonus, same 0..1 scale as minorPassionBonus/majorPassionBonus. May be negative.</summary>
        public float bonus;

        public override void PostLoad()
        {
            base.PostLoad();
            if (passionDef.NullOrEmpty())
                Log.Warning($"[Adaptive Priorities] PassionScoreDef '{defName}' has no passionDef target and will be ignored.");
        }
    }
}
