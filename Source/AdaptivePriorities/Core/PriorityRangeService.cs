using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.Core
{
    /// <summary>
    /// Resolves the effective work priority range (1 = most urgent, up to LowestPriority, 0 = not
    /// doing), never assuming vanilla's 1-4 and requiring no configuration.
    ///
    /// The ceiling comes from PriorityRangeSourceDefs — data describing where each known range-extending
    /// mod stores its configured max. Inactive or unresolvable sources are skipped silently, degrading
    /// to the vanilla 1-4 fallback. Values are read live (cached MemberInfo) so in-session changes are
    /// picked up; if several sources are active, the highest wins.
    ///
    /// Why data-driven rather than generic detection: Pawn_WorkSettings.LowestPriority is a compile-time
    /// const (inlined, nothing to reflect), and write/read probing measures a mod's storage capacity,
    /// not the player's configured range. The mod's own settings value is the only truthful signal.
    /// </summary>
    public static class PriorityRangeService
    {
        private const int FallbackLowestPriority = 4;

        private static List<Func<int>> sourceReaders;

        /// <summary>1 is always the most urgent priority in Verse, regardless of how far the range extends.</summary>
        public static int HighestPriority => 1;

        public static int LowestPriority
        {
            get
            {
                sourceReaders ??= ResolveSources();

                int lowest = FallbackLowestPriority;
                foreach (var read in sourceReaders)
                {
                    try
                    {
                        lowest = Mathf.Max(lowest, read());
                    }
                    catch (Exception e)
                    {
                        Log.WarningOnce($"[Adaptive Priorities] A priority range source failed to read; ignoring it. {e}", 194270);
                    }
                }

                return lowest;
            }
        }

        public static int Clamp(int priority) => Mathf.Clamp(priority, 0, LowestPriority);

        /// <summary>Maps a 0..1 desirability score (1 = best) onto the effective priority range.</summary>
        public static int FromNormalized(float normalized01)
        {
            normalized01 = Mathf.Clamp01(normalized01);
            int lowest = LowestPriority;
            int range = lowest - HighestPriority;
            return Mathf.RoundToInt(lowest - normalized01 * range);
        }

        /// <summary>Inverse of <see cref="FromNormalized"/>; priority 0 (not doing) is not meaningful here.</summary>
        public static float ToNormalized(int priority)
        {
            int lowest = LowestPriority;
            int range = lowest - HighestPriority;
            if (range <= 0)
                return 1f;

            priority = Mathf.Clamp(priority, HighestPriority, lowest);
            return 1f - (priority - HighestPriority) / (float)range;
        }

        private static List<Func<int>> ResolveSources()
        {
            var readers = new List<Func<int>>();

            foreach (var def in DefDatabase<PriorityRangeSourceDef>.AllDefsListForReading)
            {
                if (def.modPackageId.NullOrEmpty() || def.typeName.NullOrEmpty() || def.memberName.NullOrEmpty())
                    continue;

                if (!ModsConfig.IsActive(def.modPackageId))
                    continue;

                try
                {
                    var type = GenTypes.GetTypeInAnyAssembly(def.typeName);
                    if (type == null)
                    {
                        Log.Warning($"[Adaptive Priorities] Range source '{def.defName}': type '{def.typeName}' not found although mod '{def.modPackageId}' is active; skipping.");
                        continue;
                    }

                    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                    var field = type.GetField(def.memberName, flags);
                    if (field != null && field.FieldType == typeof(int))
                    {
                        readers.Add(() => (int)field.GetValue(null));
                        Log.Message($"[Adaptive Priorities] Priority range source active: {def.defName} ({def.typeName}.{def.memberName}, currently {(int)field.GetValue(null)}).");
                        continue;
                    }

                    var property = type.GetProperty(def.memberName, flags);
                    if (property != null && property.PropertyType == typeof(int) && property.GetMethod != null)
                    {
                        readers.Add(() => (int)property.GetValue(null));
                        Log.Message($"[Adaptive Priorities] Priority range source active: {def.defName} ({def.typeName}.{def.memberName}, currently {(int)property.GetValue(null)}).");
                        continue;
                    }

                    Log.Warning($"[Adaptive Priorities] Range source '{def.defName}': no static int field/property '{def.memberName}' on '{def.typeName}'; skipping.");
                }
                catch (Exception e)
                {
                    Log.Warning($"[Adaptive Priorities] Range source '{def.defName}' failed to resolve; skipping. {e}");
                }
            }

            return readers;
        }
    }
}
