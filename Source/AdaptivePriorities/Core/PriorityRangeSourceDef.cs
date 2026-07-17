using Verse;

namespace AdaptivePriorities
{
    /// <summary>
    /// Data-driven description of where a range-extending mod stores its configured maximum work
    /// priority. Supporting a new mod is pure XML, from any mod, with no assembly reference.
    ///
    /// Simple case — the value lives in a static int field/property:
    ///
    ///   <AdaptivePriorities.PriorityRangeSourceDef>
    ///     <defName>WorkTabRangeSource</defName>
    ///     <modPackageId>Fluffy.WorkTab</modPackageId>
    ///     <typeName>WorkTab.Settings</typeName>
    ///     <memberName>maxPriority</memberName>
    ///   </AdaptivePriorities.PriorityRangeSourceDef>
    ///
    /// Two-hop case — the value lives on an instance reached through a static singleton. Here
    /// <see cref="memberName"/> names the static field/property holding the instance and
    /// <see cref="instanceMemberName"/> names the int field/property/parameterless-method on it:
    ///
    ///   <AdaptivePriorities.PriorityRangeSourceDef>
    ///     <defName>PriorityMasterRangeSource</defName>
    ///     <modPackageId>Lauriichan.PriorityMaster</modPackageId>
    ///     <typeName>PriorityMod.Core.PriorityMaster</typeName>
    ///     <memberName>settings</memberName>
    ///     <instanceMemberName>maxPriority</instanceMemberName>
    ///   </AdaptivePriorities.PriorityRangeSourceDef>
    /// </summary>
    public class PriorityRangeSourceDef : Def
    {
        /// <summary>Source is only considered when this mod is active.</summary>
        public string modPackageId;

        /// <summary>Full name of the type holding the value (or the static singleton), resolved via GenTypes across all loaded assemblies.</summary>
        public string typeName;

        /// <summary>
        /// Name of a static member on <see cref="typeName"/>. When <see cref="instanceMemberName"/> is
        /// empty this must be a static int field/property. Otherwise it is the static field/property
        /// holding the instance to read <see cref="instanceMemberName"/> from.
        /// </summary>
        public string memberName;

        /// <summary>
        /// Optional. Name of an int field/property/parameterless-method on the instance returned by
        /// the static <see cref="memberName"/>. Leave empty for the simple static-int case.
        /// </summary>
        public string instanceMemberName;

        public override void PostLoad()
        {
            base.PostLoad();
            if (modPackageId.NullOrEmpty() || typeName.NullOrEmpty() || memberName.NullOrEmpty())
                Log.Warning($"[Adaptive Priorities] PriorityRangeSourceDef '{defName}' is missing modPackageId, typeName or memberName and will be ignored.");
        }
    }
}
