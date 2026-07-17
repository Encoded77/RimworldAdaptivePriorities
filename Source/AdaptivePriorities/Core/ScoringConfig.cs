namespace AdaptivePriorities.Core
{
    /// <summary>
    /// Read point for the global scoring weights: the player's override if set, otherwise the XML Def
    /// value (so the mod stays zero-config and ScoringTuningDef stays patchable).
    /// </summary>
    public static class ScoringConfig
    {
        private static AdaptivePrioritiesSettings Settings => AdaptivePrioritiesMod.Settings;
        private static ScoringTuningDef Def => ScoringTuningDef.Active;

        private static float F(string key, float def) => Settings != null ? Settings.GetFloat(key, def) : def;
        private static bool B(string key, bool def) => Settings != null ? Settings.GetBool(key, def) : def;

        public static float SkillWeight => F("skillWeight", Def.skillWeight);
        public static float MinorPassionBonus => F("minorPassionBonus", Def.minorPassionBonus);
        public static float MajorPassionBonus => F("majorPassionBonus", Def.majorPassionBonus);
        public static float BadPassionPenalty => F("badPassionPenalty", Def.badPassionPenalty);
        public static float MaxPassionBonus => F("maxPassionBonus", Def.maxPassionBonus);
        public static float NoSkillWorkScore => F("noSkillWorkScore", Def.noSkillWorkScore);
        public static float RelativeWeight => F("relativeWeight", Def.relativeWeight);
        public static float QualityWeight => F("qualityWeight", Def.qualityWeight);
        public static float OpposedWorkFactor => F("opposedWorkFactor", Def.opposedWorkFactor);
        public static float WorkStatWeight => F("workStatWeight", Def.workStatWeight);
        public static float InspirationBonus => F("inspirationBonus", Def.inspirationBonus);
        public static float InspirationUrgency => F("inspirationUrgency", Def.inspirationUrgency);
        public static bool CoverageGuaranteeEnabled => B("coverageGuaranteeEnabled", Def.coverageGuaranteeEnabled);
        public static bool InspirationBonusEnabled => B("inspirationBonusEnabled", Def.inspirationBonusEnabled);
        public static bool AssignOpposedWhenNeeded => B("assignOpposedWhenNeeded", Def.assignOpposedWhenNeeded);
    }
}
