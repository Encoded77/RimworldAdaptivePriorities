using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.Core
{
    /// <summary>
    /// Maps a pawn's passion in a skill to a 0..1 score bonus, covering modded passions with no
    /// per-passion code. Vanilla None/Minor/Major use the tunable bonuses; a PassionScoreDef override
    /// pins an exact value for a named passion; otherwise the bonus is derived from the passion's own
    /// learnRateFactor, read from VSE's PassionDef by cached reflection (no assembly reference, gated on
    /// the mod being active). Bad passions (isBad) are floored to a penalty so they steer work away.
    ///
    /// Alpha Skills ships its passions as VSE PassionDefs and depends on VSE, so this one bridge covers
    /// both. If VSE's internals move it falls back to the (VSE-patched) SkillRecord.LearnRateFactor, then
    /// to a minor-passion bonus; it never throws into the scorer.
    /// </summary>
    public static class PassionScoreService
    {
        // Vanilla learn-rate anchors (SkillRecord.LearnRateFactor). The curve maps a passion's
        // learnRateFactor through these and extrapolates past them, so a 2x/3x passion beats a major one.
        private const float LearnRateNone = 0.35f;
        private const float LearnRateMinor = 1f;
        private const float LearnRateMajor = 1.5f;

        // Resolved reflectively so there's no build-time dependency on VSE.
        private const string VsePackageId = "vanillaexpanded.skills";
        private const string PassionManagerTypeName = "VSE.Passions.PassionManager";
        private const string PassionDefTypeName = "VSE.Passions.PassionDef";

        private static bool vseResolved;
        private static bool vseAvailable;
        private static MethodInfo passionToDefMethod; // static PassionDef PassionToDef(Passion)
        private static FieldInfo learnRateField;      // float PassionDef.learnRateFactor
        private static FieldInfo isBadField;          // bool  PassionDef.isBad

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

            EnsureVse();
            if (vseAvailable)
            {
                try
                {
                    if (passionToDefMethod.Invoke(null, new object[] { passion }) is Def def)
                        return def.defName;
                }
                catch
                {
                }
            }

            return $"Passion#{(byte)passion}";
        }

        private static float ModdedBonus(SkillRecord record)
        {
            var info = InfoFor(record.passion);

            // Explicit override wins.
            if (info.overrideDef != null)
                return Mathf.Clamp(info.overrideDef.bonus, -1f, 1f);

            // Otherwise derive from the passion's learn rate and bad flag.
            if (info.hasData)
            {
                float bonus = FromLearnRate(info.learnRate);
                if (info.isBad)
                    bonus = Mathf.Min(bonus, -ScoringConfig.BadPassionPenalty);
                return Mathf.Clamp(bonus, -1f, 1f);
            }

            // VSE data unreadable: read the (VSE-patched) factor directly. direct:true drops the pawn's
            // global-learning / saturation multipliers, leaving the passion signal.
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
            EnsureVse();
            if (!vseAvailable)
                return info;

            try
            {
                if (!(passionToDefMethod.Invoke(null, new object[] { passion }) is Def passionDef))
                    return info;

                info.learnRate = (float)learnRateField.GetValue(passionDef);
                info.isBad = isBadField != null && (bool)isBadField.GetValue(passionDef);
                info.hasData = true;
                info.overrideDef = OverrideFor(passionDef.defName);
            }
            catch (Exception e)
            {
                Log.WarningOnce($"[Adaptive Priorities] Could not read VSE passion data; using learn-rate fallback. {e}", 77122301);
            }

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

        private static void EnsureVse()
        {
            if (vseResolved)
                return;
            vseResolved = true;

            if (!ModsConfig.IsActive(VsePackageId))
                return;

            try
            {
                var managerType = GenTypes.GetTypeInAnyAssembly(PassionManagerTypeName);
                var passionDefType = GenTypes.GetTypeInAnyAssembly(PassionDefTypeName);
                if (managerType == null || passionDefType == null)
                {
                    Log.Warning($"[Adaptive Priorities] {VsePackageId} is active but its passion types were not found; modded passions will use the learn-rate fallback.");
                    return;
                }

                passionToDefMethod = managerType.GetMethod("PassionToDef", BindingFlags.Public | BindingFlags.Static);
                learnRateField = passionDefType.GetField("learnRateFactor", BindingFlags.Public | BindingFlags.Instance);
                isBadField = passionDefType.GetField("isBad", BindingFlags.Public | BindingFlags.Instance);

                vseAvailable = passionToDefMethod != null && learnRateField != null;
                if (!vseAvailable)
                    Log.Warning("[Adaptive Priorities] VSE passion API shape changed; modded passions will use the learn-rate fallback.");
            }
            catch (Exception e)
            {
                Log.Warning($"[Adaptive Priorities] Failed to bind VSE passion API; modded passions will use the learn-rate fallback. {e}");
            }
        }

        private struct PassionInfo
        {
            public bool hasData;
            public float learnRate;
            public bool isBad;
            public PassionScoreDef overrideDef;
        }
    }
}
