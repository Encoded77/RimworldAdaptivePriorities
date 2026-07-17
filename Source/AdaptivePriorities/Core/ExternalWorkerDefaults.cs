using Verse;

namespace AdaptivePriorities
{
    /// <summary>
    /// Optional per-WorkTypeDef override for the external-worker defaults a work type gets when its policy
    /// is derived (no explicit WorkTypePolicyDef). This is how a work type is marked a "quality craft" that
    /// keeps a larger colonist backup - so an external crafter (a Fabricor) supplements the colony's
    /// crafters rather than replacing them - without needing a full policy def. A mod adding quality-craft
    /// work types (subjobs it derives from Crafting/Smithing, say) attaches this the same way vanilla crafts
    /// are marked, and the player can patch one on too. Ignored when an explicit WorkTypePolicyDef exists
    /// for the type, since that already carries these fields.
    /// </summary>
    public class ExternalWorkerDefaults : DefModExtension
    {
        public ExternalOffloadMode externalOffload = ExternalOffloadMode.Reduce;
        public int externalBackup = 1;
        public float externalUptimeFactor = 1f;
    }
}
