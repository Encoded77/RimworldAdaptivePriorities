using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.Core
{
    /// <summary>
    /// Maps a pawn's passion in a skill to a 0..1 score bonus, covering modded passions with no
    /// per-passion code. Vanilla None/Minor/Major use the tunable bonuses; modded (VSE/Alpha Skills)
    /// passions resolve with the same precedence as every other value in the mod:
    /// settings override > XML Def (PassionScoreDef) > derived from the passion's own learnRateFactor,
    /// read through <see cref="VsePassionBridge"/>. Bad passions (isBad) are floored to a penalty so
    /// they steer work away.
    ///
    /// If VSE's internals move it falls back to the (VSE-patched) SkillRecord.LearnRateFactor, then to
    /// a minor-passion bonus; it never throws into the scorer.
    /// </summary>
    public static class PassionScoreService
    {
        // Vanilla learn-rate anchors (SkillRecord.LearnRateFactor). The curve maps a passion's
        // learnRateFactor through these and extrapolates past them, so a 2x/3x passion beats a major one.
        private const float LearnRateNone = 0.35f;
        private const float LearnRateMinor = 1f;
        private const float LearnRateMajor = 1.5f;

        private static Dictionary<string, PassionScoreDef> overridesByPassion;
        private static readonly Dictionary<Passion, PassionInfo> infoCache = new Dictionary<Passion, PassionInfo>();

        /// <summary>Score bonus (may be negative) for the pawn's passion in this skill.</summary>
        public static float BonusFor(SkillRecord record)
        {
            switch (record.passion)
            {
                case Passion.None:
                    return 0f;
                case Passion.Minor:
                    return ScoringConfig.MinorPassionBonus;
                case Passion.Major:
                    return ScoringConfig.MajorPassionBonus;
                default:
                    return ModdedBonus(record);
            }
        }

        /// <summary>
        /// The bonus a passion gets before any per-passion settings override: the tunable Minor/Major
        /// values for vanilla passions, and for modded ones the PassionScoreDef pin or the learn-rate
        /// derivation. The settings UI reads this for slider defaults, so a slider left untouched keeps
        /// tracking the curve (and the Minor/Major sliders that feed it).
        /// </summary>
        public static float DefaultBonusFor(Passion passion)
        {
            switch (passion)
            {
                case Passion.None:
                    return 0f;
                case Passion.Minor:
                    return ScoringConfig.MinorPassionBonus;
                case Passion.Major:
                    return ScoringConfig.MajorPassionBonus;
            }

            var info = InfoFor(passion);
            return info.defName != null ? DefaultModdedBonus(info) : ScoringConfig.MinorPassionBonus;
        }

        /// <summary>Whether a PassionScoreDef pins this passion's default (used by the settings tooltip).</summary>
        public static bool HasXmlOverride(string passionDefName) => OverrideFor(passionDefName) != null;

        /// <summary>
        /// Passion name for debug output: the VSE PassionDef's defName for a modded passion, the enum
        /// name for a vanilla one, the raw byte if it can't be resolved. Never throws. Debug-only - it
        /// allocates and resolves through reflection, so keep it off the scoring path.
        /// </summary>
        public static string DebugNameFor(Passion passion)
        {
            switch (passion)
            {
                case Passion.None:
                    return "None";
                case Passion.Minor:
                    return "Minor";
                case Passion.Major:
                    return "Major";
            }

            var info = InfoFor(passion);
            if (info.overrideDef != null)
                return info.overrideDef.passionDef + "*";
            if (info.defName != null)
                return info.defName;

            // Data reads can fail while the def itself still resolves; prefer its name to a raw byte.
            var def = VsePassionBridge.PassionToDef(passion);
            return def != null ? def.defName : $"Passion#{(byte)passion}";
        }

        private static float ModdedBonus(SkillRecord record)
        {
            var info = InfoFor(record.passion);

            if (info.defName == null)
            {
                // VSE data unreadable: read the (VSE-patched) factor directly. direct:true drops the
                // pawn's global-learning / saturation multipliers, leaving the passion signal.
                try
                {
                    return Mathf.Clamp(FromLearnRate(record.LearnRateFactor(direct: true)), -1f, 1f);
                }
                catch
                {
                    // No readable data and no patch: treat as minor so it isn't ignored.
                    return ScoringConfig.MinorPassionBonus;
                }
            }

            float fallback = DefaultModdedBonus(info);
            var settings = AdaptivePrioritiesMod.Settings;
            float bonus = settings != null
                ? settings.GetFloat(AdaptivePrioritiesSettings.PassionKey(info.defName), fallback)
                : fallback;
            return Mathf.Clamp(bonus, -1f, 1f);
        }

        /// <summary>Def-driven default: explicit PassionScoreDef pin, else the learn-rate derivation.</summary>
        private static float DefaultModdedBonus(in PassionInfo info)
        {
            if (info.overrideDef != null)
                return Mathf.Clamp(info.overrideDef.bonus, -1f, 1f);

            float bonus = FromLearnRate(info.learnRate);
            if (info.isBad)
                bonus = Mathf.Min(bonus, -ScoringConfig.BadPassionPenalty);
            return Mathf.Clamp(bonus, -1f, 1f);
        }

        /// <summary>
        /// Piecewise-linear map from learnRateFactor to a bonus, through the none/minor/major anchors.
        /// Unclamped below, so sub-none passions read negative; capped at maxPassionBonus above, since
        /// the extrapolation would otherwise let a high learn rate out-value the whole skill term.
        /// </summary>
        private static float FromLearnRate(float learnRate)
        {
            float minor = ScoringConfig.MinorPassionBonus;
            float major = ScoringConfig.MajorPassionBonus;
            float bonus;

            if (learnRate <= LearnRateMinor)
            {
                float t = (learnRate - LearnRateNone) / (LearnRateMinor - LearnRateNone);
                bonus = Mathf.LerpUnclamped(0f, minor, t);
            }
            else
            {
                float t = (learnRate - LearnRateMinor) / (LearnRateMajor - LearnRateMinor);
                bonus = Mathf.LerpUnclamped(minor, major, t);
            }

            return Mathf.Min(bonus, ScoringConfig.MaxPassionBonus);
        }

        private static PassionInfo InfoFor(Passion passion)
        {
            // byte -> PassionDef is fixed for the session, so cache the reflection result; the bonus
            // arithmetic still re-runs each call to pick up settings changes.
            if (infoCache.TryGetValue(passion, out var cached))
                return cached;

            var info = Resolve(passion);
            infoCache[passion] = info;
            return info;
        }

        private static PassionInfo Resolve(Passion passion)
        {
            var info = default(PassionInfo);
            var passionDef = VsePassionBridge.PassionToDef(passion);
            if (passionDef == null || !VsePassionBridge.TryGetData(passionDef, out info.learnRate, out info.isBad))
                return default;

            info.defName = passionDef.defName;
            info.overrideDef = OverrideFor(passionDef.defName);
            return info;
        }

        private static PassionScoreDef OverrideFor(string passionDefName)
        {
            if (overridesByPassion == null)
            {
                overridesByPassion = new Dictionary<string, PassionScoreDef>();
                foreach (var def in DefDatabase<PassionScoreDef>.AllDefsListForReading)
                    if (!def.passionDef.NullOrEmpty())
                        overridesByPassion[def.passionDef] = def;
            }

            return passionDefName != null && overridesByPassion.TryGetValue(passionDefName, out var d) ? d : null;
        }

        /// <summary>Cached per-Passion identity/data; null defName means "no readable VSE data".</summary>
        private struct PassionInfo
        {
            public string defName;
            public float learnRate;
            public bool isBad;
            public PassionScoreDef overrideDef;
        }
    }
}
