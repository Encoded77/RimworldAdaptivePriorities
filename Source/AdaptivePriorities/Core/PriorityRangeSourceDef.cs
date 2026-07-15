using Verse;

namespace AdaptivePriorities
{
    /// <summary>
    /// Data-driven description of where a range-extending mod stores its configured maximum work
    /// priority. Supporting a new mod is pure XML, from any mod, with no assembly reference:
    ///
    ///   <AdaptivePriorities.PriorityRangeSourceDef>
    ///     <defName>WorkTabRangeSource</defName>
    ///     <modPackageId>Fluffy.WorkTab</modPackageId>
    ///     <typeName>WorkTab.Settings</typeName>
    ///     <memberName>maxPriority</memberName>
    ///   </AdaptivePriorities.PriorityRangeSourceDef>
    /// </summary>
    public class PriorityRangeSourceDef : Def
    {
        /// <summary>Source is only considered when this mod is active.</summary>
        public string modPackageId;

        /// <summary>Full name of the type holding the value, resolved via GenTypes across all loaded assemblies.</summary>
        public string typeName;

        /// <summary>Name of a static int field or property on that type.</summary>
        public string memberName;

        public override void PostLoad()
        {
            base.PostLoad();
            if (modPackageId.NullOrEmpty() || typeName.NullOrEmpty() || memberName.NullOrEmpty())
                Log.Warning($"[Adaptive Priorities] PriorityRangeSourceDef '{defName}' is missing modPackageId, typeName or memberName and will be ignored.");
        }
    }
}
