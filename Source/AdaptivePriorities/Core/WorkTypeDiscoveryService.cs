using System.Collections.Generic;
using Verse;

namespace AdaptivePriorities.Core
{
    /// <summary>
    /// Source of truth for "which WorkTypeDefs exist". Reads from DefDatabase rather than assuming the
    /// vanilla list, so modded work types are picked up.
    /// </summary>
    public static class WorkTypeDiscoveryService
    {
        private static List<WorkTypeDef> cachedWorkTypes;

        public static List<WorkTypeDef> GetAllWorkTypes()
        {
            return cachedWorkTypes ??= new List<WorkTypeDef>(DefDatabase<WorkTypeDef>.AllDefsListForReading);
        }

        /// <summary>Call after a dev-mode def reload; nothing else should need it.</summary>
        public static void InvalidateCache()
        {
            cachedWorkTypes = null;
        }

        public static List<Pawn> GetColonistsOnMap(Map map)
        {
            return map?.mapPawns?.FreeColonists;
        }
    }
}
