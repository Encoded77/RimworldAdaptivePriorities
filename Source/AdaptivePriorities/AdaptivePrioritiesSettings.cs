using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AdaptivePriorities
{
    /// <summary>
    /// Global preferences stored as implicit per-value overrides: a value counts as overridden only
    /// once the player changes it away from its default, so untouched values keep following the Defs
    /// (and stay patchable by other mods). Keys are the bare field name for global scoring values and
    /// "field@WorkTypeDefName" for per-work-type policy, so any discovered work type can be tuned even
    /// without an XML policy def.
    /// </summary>
    public class AdaptivePrioritiesSettings : ModSettings
    {
        public bool advancedMode;

        /// <summary>Auto-mode interval in ticks (2500 = one in-game hour). Floored at one hour so priorities don't recompute every tick.</summary>
        public int autoRecalcIntervalTicks = GenDate.TicksPerHour;

        private Dictionary<string, float> floatOverrides = new Dictionary<string, float>();
        private Dictionary<string, bool> boolOverrides = new Dictionary<string, bool>();
        private Dictionary<string, int> intOverrides = new Dictionary<string, int>();

        public static string PolicyKey(string field, WorkTypeDef workType) => field + "@" + workType.defName;

        public bool IsOverridden(string key) =>
            floatOverrides.ContainsKey(key) || boolOverrides.ContainsKey(key) || intOverrides.ContainsKey(key);

        public float GetFloat(string key, float defaultValue) =>
            floatOverrides.TryGetValue(key, out var v) ? v : defaultValue;

        public bool GetBool(string key, bool defaultValue) =>
            boolOverrides.TryGetValue(key, out var v) ? v : defaultValue;

        public int GetInt(string key, int defaultValue) =>
            intOverrides.TryGetValue(key, out var v) ? v : defaultValue;

        // Setting a value back to its default clears the override, so "reset" and "drag to default"
        // are the same thing.
        public void SetFloat(string key, float value, float defaultValue)
        {
            if (Mathf.Approximately(value, defaultValue)) floatOverrides.Remove(key);
            else floatOverrides[key] = value;
        }

        public void SetBool(string key, bool value, bool defaultValue)
        {
            if (value == defaultValue) boolOverrides.Remove(key);
            else boolOverrides[key] = value;
        }

        public void SetInt(string key, int value, int defaultValue)
        {
            if (value == defaultValue) intOverrides.Remove(key);
            else intOverrides[key] = value;
        }

        public void ClearKey(string key)
        {
            floatOverrides.Remove(key);
            boolOverrides.Remove(key);
            intOverrides.Remove(key);
        }

        public void ClearWorkType(WorkTypeDef workType)
        {
            string suffix = "@" + workType.defName;
            RemoveBySuffix(floatOverrides, suffix);
            RemoveBySuffix(boolOverrides, suffix);
            RemoveBySuffix(intOverrides, suffix);
        }

        public void ClearAll()
        {
            floatOverrides.Clear();
            boolOverrides.Clear();
            intOverrides.Clear();
        }

        public bool HasAnyOverride => floatOverrides.Count > 0 || boolOverrides.Count > 0 || intOverrides.Count > 0;

        private static void RemoveBySuffix<T>(Dictionary<string, T> dict, string suffix)
        {
            var toRemove = new List<string>();
            foreach (var key in dict.Keys)
                if (key.EndsWith(suffix))
                    toRemove.Add(key);
            foreach (var key in toRemove)
                dict.Remove(key);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref advancedMode, "advancedMode", false);
            Scribe_Values.Look(ref autoRecalcIntervalTicks, "autoRecalcIntervalTicks", GenDate.TicksPerHour);
            Scribe_Collections.Look(ref floatOverrides, "floatOverrides", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref boolOverrides, "boolOverrides", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref intOverrides, "intOverrides", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                floatOverrides ??= new Dictionary<string, float>();
                boolOverrides ??= new Dictionary<string, bool>();
                intOverrides ??= new Dictionary<string, int>();
            }
        }
    }
}
