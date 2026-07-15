using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

public enum LockGlyphPosition
{
    Center,
    TopRight,
    RightCenter,
    BottomCenter
}

namespace AdaptivePriorities.UI
{
    /// <summary>
    /// Shared drawing and hit-testing helpers for the in-grid lock UI, so the same padlock glyph and
    /// middle-click gesture behave identically in the vanilla Work tab and Fluffy's Work Tab.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class WorkGridOverlay
    {
        private static readonly Texture2D LockGlyph = ContentFinder<Texture2D>.Get("UI/AdaptivePriorities/LockButton");

        // PawnTable.def is private; read it to identify work tables. Result is cached per def.
        private static readonly AccessTools.FieldRef<PawnTable, PawnTableDef> TableDefRef =
            AccessTools.FieldRefAccess<PawnTable, PawnTableDef>("def");

        private static readonly HashSet<PawnTableDef> WorkTables = new HashSet<PawnTableDef>();
        private static readonly HashSet<PawnTableDef> NonWorkTables = new HashSet<PawnTableDef>();

        /// <summary>
        /// True only for work-priority pawn tables. The pawn-name column is shared with the
        /// Assign/Restrict/Animals tables, so this keeps our hooks off those. A table is "work" iff any
        /// of its columns is bound to a work type.
        /// </summary>
        public static bool IsWorkTable(PawnTable table)
        {
            if (table == null)
                return false;

            var def = TableDefRef(table);
            if (def == null)
                return false;
            if (WorkTables.Contains(def))
                return true;
            if (NonWorkTables.Contains(def))
                return false;

            bool isWork = def.columns != null && def.columns.Any(c => c.workType != null);
            (isWork ? WorkTables : NonWorkTables).Add(def);
            return isWork;
        }

        /// <summary>Draws the padlock glyph at the given position within a grid rect.</summary>
        public static void DrawLockGlyph(Rect rect, LockGlyphPosition position)
        {
            float size = Mathf.Min(14f, rect.height, rect.width);

            const float padding = 1f;

            float x = rect.x;
            float y = rect.y;

            switch (position)
            {
                case LockGlyphPosition.Center:
                    x = rect.x + (rect.width - size) * 0.5f;
                    y = rect.y + (rect.height - size) * 0.5f;
                    break;
                case LockGlyphPosition.TopRight:
                    x = rect.xMax - size - padding;
                    y = rect.y + padding;
                    break;

                case LockGlyphPosition.RightCenter:
                    x = rect.xMax - size;
                    y = rect.y + (rect.height - size) * 0.5f;
                    break;

                case LockGlyphPosition.BottomCenter:
                    // Vanilla's work-type header connector is two 1px verticals at center.x and
                    // center.x+1, so its visual midpoint is center.x + 0.5. Aim the glyph there; the
                    // round-half-up snap below lands it on the right-hand connector pixel, straddling
                    // both lines.
                    x = rect.center.x + 0.5f - size * 0.5f;
                    y = rect.yMax - size - padding;
                    break;
            }

            // Snap to whole pixels — a fractional X samples the icon across two columns and reads as
            // blurry. Round-half-up (not Mathf.Round's banker's rounding) so a .5 always resolves the
            // same way.
            var glyphRect = new Rect(Mathf.Floor(x + 0.5f), Mathf.Floor(y + 0.5f), size, size);

            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.depth = 1;
            GUI.DrawTexture(glyphRect, LockGlyph);
            GUI.color = prev;
        }

        /// <summary>
        /// Is this the middle-mouse-down event at all? Click hooks test this first so they skip their
        /// per-cell work on the repaint/layout passes that make up nearly every frame.
        /// </summary>
        public static bool IsMiddleMouseDown
        {
            get
            {
                var e = Event.current;
                return e.type == EventType.MouseDown && e.button == 2;
            }
        }

        /// <summary>
        /// True on the frame the middle mouse button is pressed over rect; consumes the event so it
        /// doesn't fall through to the underlying tab.
        /// </summary>
        public static bool MiddleClicked(Rect rect)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 2 && rect.Contains(e.mousePosition))
            {
                e.Use();
                return true;
            }
            return false;
        }
    }
}
