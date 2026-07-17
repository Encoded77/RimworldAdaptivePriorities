using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.Core
{
    /// <summary>
    /// The one reflection binding into Vanilla Skills Expanded's passion API — no assembly reference,
    /// gated on the mod being active. Alpha Skills ships its passions as VSE PassionDefs and depends
    /// on VSE, so this single bridge covers both. Shared by the scorer (PassionScoreService) and the
    /// settings UI (PassionDiscoveryService) so VSE is only ever bound once.
    ///
    /// Contract: every accessor is fail-silent. If VSE's internals move, binding logs one warning and
    /// calls degrade to null/false/empty — never a throw into the scorer or OnGUI.
    /// </summary>
    public static class VsePassionBridge
    {
        private const string VsePackageId = "vanillaexpanded.skills";
        private const string PassionManagerTypeName = "VSE.Passions.PassionManager";
        private const string PassionDefTypeName = "VSE.Passions.PassionDef";

        private static bool resolved;
        private static bool available;
        private static MethodInfo passionToDefMethod; // static PassionDef PassionToDef(Passion)
        private static FieldInfo passionsField;       // static PassionDef[] PassionManager.Passions
        private static FieldInfo learnRateField;      // float PassionDef.learnRateFactor
        private static FieldInfo isBadField;          // bool  PassionDef.isBad
        private static PropertyInfo iconProperty;     // Texture2D PassionDef.Icon (lazy ContentFinder)

        /// <summary>True when VSE is active and its passion API bound successfully.</summary>
        public static bool Available
        {
            get
            {
                EnsureResolved();
                return available;
            }
        }

        /// <summary>The VSE PassionDef behind a Passion value, or null (VSE absent, unbound, out of range).</summary>
        public static Def PassionToDef(Passion passion)
        {
            EnsureResolved();
            if (!available)
                return null;

            try
            {
                return passionToDefMethod.Invoke(null, new object[] { passion }) as Def;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Every loaded passion def in VSE's registration order (the vanilla three first). Empty when
        /// VSE is absent or the list can't be read — callers then fall back to the vanilla passions.
        /// </summary>
        public static List<Def> AllPassionDefs()
        {
            var result = new List<Def>();
            EnsureResolved();
            if (!available || passionsField == null)
                return result;

            try
            {
                if (passionsField.GetValue(null) is Array all)
                {
                    foreach (var item in all)
                        if (item is Def def)
                            result.Add(def);
                }
            }
            catch (Exception e)
            {
                Log.WarningOnce($"[Adaptive Priorities] Could not enumerate VSE passions; settings will show only the vanilla ones. {e}", 77122302);
                result.Clear();
            }

            return result;
        }

        /// <summary>Reads learnRateFactor/isBad off a VSE PassionDef. False when unreadable.</summary>
        public static bool TryGetData(Def passionDef, out float learnRate, out bool isBad)
        {
            learnRate = 1f;
            isBad = false;
            EnsureResolved();
            if (!available || passionDef == null)
                return false;

            try
            {
                learnRate = (float)learnRateField.GetValue(passionDef);
                isBad = isBadField != null && (bool)isBadField.GetValue(passionDef);
                return true;
            }
            catch (Exception e)
            {
                Log.WarningOnce($"[Adaptive Priorities] Could not read VSE passion data; using learn-rate fallback. {e}", 77122301);
                return false;
            }
        }

        /// <summary>
        /// The passion's icon; VSE resolves it lazily via ContentFinder, so main (OnGUI) thread only.
        /// Null when the def has no icon or the property moved.
        /// </summary>
        public static Texture2D IconFor(Def passionDef)
        {
            EnsureResolved();
            if (!available || iconProperty == null || passionDef == null)
                return null;

            try
            {
                return iconProperty.GetValue(passionDef) as Texture2D;
            }
            catch
            {
                return null;
            }
        }

        private static void EnsureResolved()
        {
            if (resolved)
                return;
            resolved = true;

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
                passionsField = managerType.GetField("Passions", BindingFlags.Public | BindingFlags.Static);
                learnRateField = passionDefType.GetField("learnRateFactor", BindingFlags.Public | BindingFlags.Instance);
                isBadField = passionDefType.GetField("isBad", BindingFlags.Public | BindingFlags.Instance);
                iconProperty = passionDefType.GetProperty("Icon", BindingFlags.Public | BindingFlags.Instance);

                available = passionToDefMethod != null && learnRateField != null;
                if (!available)
                    Log.Warning("[Adaptive Priorities] VSE passion API shape changed; modded passions will use the learn-rate fallback.");
            }
            catch (Exception e)
            {
                Log.Warning($"[Adaptive Priorities] Failed to bind VSE passion API; modded passions will use the learn-rate fallback. {e}");
            }
        }
    }
}
