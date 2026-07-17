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

        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

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
                    var reader = BuildReader(def);
                    if (reader != null)
                    {
                        readers.Add(reader);
                        Log.Message($"[Adaptive Priorities] Priority range source active: {def.defName} (currently {SafeRead(reader)}).");
                    }
                }
                catch (Exception e)
                {
                    Log.Warning($"[Adaptive Priorities] Range source '{def.defName}' failed to resolve; skipping. {e}");
                }
            }

            return readers;
        }

        private static Func<int> BuildReader(PriorityRangeSourceDef def)
        {
            var type = GenTypes.GetTypeInAnyAssembly(def.typeName);
            if (type == null)
            {
                Log.Warning($"[Adaptive Priorities] Range source '{def.defName}': type '{def.typeName}' not found although mod '{def.modPackageId}' is active; skipping.");
                return null;
            }

            var root = ResolveMember(type, def.memberName, StaticFlags);
            if (root == null)
            {
                Log.Warning($"[Adaptive Priorities] Range source '{def.defName}': no static field/property/method '{def.memberName}' on '{def.typeName}'; skipping.");
                return null;
            }

            if (def.instanceMemberName.NullOrEmpty())
            {
                if (root.MemberType != typeof(int))
                {
                    Log.Warning($"[Adaptive Priorities] Range source '{def.defName}': static member '{def.memberName}' on '{def.typeName}' is not an int; skipping.");
                    return null;
                }

                var getStatic = root.Get;
                return () => (int)getStatic(null);
            }

            // memberName holds an instance; read the int off it.
            var instance = ResolveMember(root.MemberType, def.instanceMemberName, InstanceFlags);
            if (instance == null || instance.MemberType != typeof(int))
            {
                Log.Warning($"[Adaptive Priorities] Range source '{def.defName}': no instance int field/property/method '{def.instanceMemberName}' on '{root.MemberType}'; skipping.");
                return null;
            }

            var getRoot = root.Get;
            var getInstance = instance.Get;
            return () =>
            {
                var target = getRoot(null);
                if (target == null)
                    return FallbackLowestPriority;
                return (int)getInstance(target);
            };
        }

        private static MemberAccessor ResolveMember(Type type, string name, BindingFlags flags)
        {
            var field = type.GetField(name, flags);
            if (field != null)
                return new MemberAccessor(target => field.GetValue(target), field.FieldType);

            var property = type.GetProperty(name, flags);
            if (property != null && property.GetMethod != null)
                return new MemberAccessor(target => property.GetValue(target), property.PropertyType);

            var method = type.GetMethod(name, flags, null, Type.EmptyTypes, null);
            if (method != null)
                return new MemberAccessor(target => method.Invoke(target, null), method.ReturnType);

            return null;
        }

        private static int SafeRead(Func<int> reader)
        {
            try { return reader(); }
            catch { return FallbackLowestPriority; }
        }

        private sealed class MemberAccessor
        {
            public readonly Func<object, object> Get;
            public readonly Type MemberType;

            public MemberAccessor(Func<object, object> get, Type memberType)
            {
                Get = get;
                MemberType = memberType;
            }
        }
    }
}
