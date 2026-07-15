using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using AdaptivePriorities.Core;
using AdaptivePriorities.UI;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.Patches
{
    /// <summary>
    /// In-grid lock UI and the auto/optimize control, injected into the work tab by Harmony. Hooks
    /// target vanilla base types and their runtime subclasses, so Fluffy's Work Tab (whose columns
    /// extend the vanilla ones and whose window extends MainTabWindow_PawnTable) is covered by the same
    /// patches. The only place Work Tab is named is the window-placement fix below, since its top bar
    /// is laid out differently.
    ///
    /// Middle-clicks are caught in PREFIXES because the vanilla name/header widgets sit on a GUI.Button
    /// that grabs the event before a postfix could see it. Postfixes only draw. All bodies use
    /// positional args (__0/__1) so subclass overrides that rename parameters still bind.
    /// </summary>
    public static class WorkGridPatches
    {
        private static readonly Type[] CellParams = { typeof(Rect), typeof(Pawn), typeof(PawnTable) };
        private static readonly Type[] HeaderParams = { typeof(Rect), typeof(PawnTable) };

        private static readonly AccessTools.FieldRef<MainTabWindow_PawnTable, PawnTable> WindowTableRef =
            AccessTools.FieldRefAccess<MainTabWindow_PawnTable, PawnTable>("table");

        // Set only when Fluffy's Work Tab is present; used to hand its window to the dedicated placement
        // patch and skip the top-right base draw that would collide with its top bar.
        private static Type workTabWindowType;

        public static void Apply(Harmony h)
        {
            var labelClick = new HarmonyMethod(typeof(WorkGridPatches), nameof(LabelCellPrefix));
            var labelDraw = new HarmonyMethod(typeof(WorkGridPatches), nameof(LabelCellPostfix));
            var headerClick = new HarmonyMethod(typeof(WorkGridPatches), nameof(WorkHeaderPrefix));
            var headerDraw = new HarmonyMethod(typeof(WorkGridPatches), nameof(WorkHeaderPostfix));
            var cellClick = new HarmonyMethod(typeof(WorkGridPatches), nameof(WorkCellPrefix));
            var cellDraw = new HarmonyMethod(typeof(WorkGridPatches), nameof(WorkCellPostfix));

            // Pawn-name column: shared by the vanilla work tab and Work Tab (both reuse vanilla
            // PawnColumnWorker_Label). Discovered by hierarchy, so no mod is named here.
            PatchDeclaring(h, typeof(PawnColumnWorker_Label), "DoCell", CellParams, prefix: labelClick, postfix: labelDraw);

            // Work-type columns: every subclass of the vanilla work-priority column with its own
            // cell/header body (Work Tab's do).
            PatchDeclaring(h, typeof(PawnColumnWorker_WorkPriority), "DoHeader", HeaderParams, prefix: headerClick, postfix: headerDraw);
            PatchDeclaring(h, typeof(PawnColumnWorker_WorkPriority), "DoCell", CellParams, prefix: cellClick, postfix: cellDraw);

            // Both work windows extend MainTabWindow_PawnTable and call base.DoWindowContents, so one
            // postfix on the base draws our control. Work Tab gets a dedicated placement patch (its
            // top-right is occupied) that draws top-left instead.
            workTabWindowType = AccessTools.TypeByName("WorkTab.MainTabWindow_WorkTab");
            try
            {
                h.Patch(AccessTools.Method(typeof(MainTabWindow_PawnTable), nameof(MainTabWindow_PawnTable.DoWindowContents)),
                    postfix: new HarmonyMethod(typeof(WorkGridPatches), nameof(WindowPostfix)));

                if (workTabWindowType != null)
                {
                    var m = AccessTools.DeclaredMethod(workTabWindowType, "DoWindowContents", new[] { typeof(Rect) });
                    if (m != null)
                        h.Patch(m, postfix: new HarmonyMethod(typeof(WorkGridPatches), nameof(WorkTabWindowPostfix)));
                }
            }
            catch (Exception e)
            {
                Log.Warning("[Adaptive Priorities] Could not patch work window; auto/optimize control may be unavailable. " + e.Message);
            }
        }

        // Patches the base type plus every loaded subclass that declares its own body for the method,
        // so each concrete override is patched exactly once. Guarded per type so one odd subclass can't
        // abort the whole hook.
        private static void PatchDeclaring(Harmony h, Type baseType, string method, Type[] paramTypes,
            HarmonyMethod prefix = null, HarmonyMethod postfix = null)
        {
            foreach (var type in GenTypes.AllTypes.Where(t => baseType.IsAssignableFrom(t)))
            {
                MethodBase target;
                try
                {
                    target = AccessTools.DeclaredMethod(type, method, paramTypes);
                }
                catch
                {
                    continue;
                }

                if (target == null || target.IsAbstract)
                    continue;

                try
                {
                    h.Patch(target, prefix: prefix, postfix: postfix);
                }
                catch (Exception e)
                {
                    Log.Warning($"[Adaptive Priorities] Could not patch {type.FullName}.{method}: {e.Message}");
                }
            }
        }

        // Grid hooks run inside a foreign draw loop; a throw here would corrupt the tab, so each body is
        // guarded and logs at most once, degrading to "no overlay".
        private static void Guard(string key, Action body)
        {
            try
            {
                body();
            }
            catch (Exception e)
            {
                Log.ErrorOnce($"[Adaptive Priorities] grid UI hook '{key}' failed: {e}", ("AP_GridHook_" + key).GetHashCode());
            }
        }

        public static void LabelCellPrefix(Rect __0, Pawn __1, PawnTable __2) => Guard("label-click", () =>
        {
            if (!WorkGridOverlay.IsMiddleMouseDown || __1 == null || !WorkGridOverlay.IsWorkTable(__2))
                return;
            if (WorkGridOverlay.MiddleClicked(__0))
                PriorityLockManager.SetPawnLocked(__1, !PriorityLockManager.IsPawnLocked(__1));
        });

        public static void LabelCellPostfix(Rect __0, Pawn __1, PawnTable __2) => Guard("label-draw", () =>
        {
            if (!PriorityLockManager.AnyPawnLocks || __1 == null || !WorkGridOverlay.IsWorkTable(__2))
                return;
            if (PriorityLockManager.IsPawnLocked(__1))
                WorkGridOverlay.DrawLockGlyph(__0, LockGlyphPosition.RightCenter);
        });

        public static void WorkHeaderPrefix(PawnColumnWorker __instance, Rect __0) => Guard("header-click", () =>
        {
            if (!WorkGridOverlay.IsMiddleMouseDown)
                return;
            var workType = __instance?.def?.workType;
            if (workType == null)
                return;
            if (WorkGridOverlay.MiddleClicked(HeaderLabelRect(__instance, __0)))
                PriorityLockManager.SetWorkTypeLocked(workType, !PriorityLockManager.IsWorkTypeLocked(workType));
        });

        public static void WorkHeaderPostfix(PawnColumnWorker __instance, Rect __0) => Guard("header-draw", () =>
        {
            if (!PriorityLockManager.AnyWorkTypeLocks)
                return;
            var workType = __instance?.def?.workType;
            if (workType == null || !PriorityLockManager.IsWorkTypeLocked(workType))
                return;
            WorkGridOverlay.DrawLockGlyph(__0, LockGlyphPosition.BottomCenter);
        });

        public static void WorkCellPrefix(PawnColumnWorker __instance, Rect __0, Pawn __1) => Guard("cell-click", () =>
        {
            if (!WorkGridOverlay.IsMiddleMouseDown)
                return;
            var workType = __instance?.def?.workType;
            if (workType == null || __1 == null || __1.workSettings == null || !__1.workSettings.EverWork || __1.WorkTypeIsDisabled(workType))
                return;
            if (WorkGridOverlay.MiddleClicked(__0))
                PriorityLockManager.SetCellLocked(__1, workType, !PriorityLockManager.IsCellLocked(__1, workType));
        });

        public static void WorkCellPostfix(PawnColumnWorker __instance, Rect __0, Pawn __1) => Guard("cell-draw", () =>
        {
            if (!PriorityLockManager.AnyCellLocks)
                return;
            var workType = __instance?.def?.workType;
            if (workType != null && __1 != null && PriorityLockManager.IsCellLocked(__1, workType))
                WorkGridOverlay.DrawLockGlyph(__0, LockGlyphPosition.TopRight);
        });

        // Label render size per work type is constant for the session; cache it so the header hooks
        // don't call Text.CalcSize for every column every frame.
        private static readonly Dictionary<WorkTypeDef, Vector2> LabelSizeCache =
            new Dictionary<WorkTypeDef, Vector2>();

        // The clickable target for a work-type lock is exactly the header label, reproducing vanilla's
        // GetLabelRect: text bounds centered on the column, pushed 20px down on staggered columns.
        // Matching that rect (rather than a widened box) keeps one column's hit area off the next.
        private static Rect HeaderLabelRect(PawnColumnWorker instance, Rect headerRect)
        {
            var workType = instance.def.workType;
            if (!LabelSizeCache.TryGetValue(workType, out Vector2 size))
            {
                Text.Font = GameFont.Small;
                size = Text.CalcSize(workType.labelShort.CapitalizeFirst());
                LabelSizeCache[workType] = size;
            }
            var rect = new Rect(headerRect.center.x - size.x / 2f, headerRect.y, size.x, size.y);
            if (instance.def.moveWorkTypeLabelDown)
                rect.y += 20f;
            return rect;
        }

        public static void WindowPostfix(MainTabWindow_PawnTable __instance, Rect __0) => Guard("window", () =>
        {
            if (Event.current.type == EventType.Layout)
                return;
            // Work Tab windows are handled by the dedicated top-left patch to avoid its top bar.
            if (workTabWindowType != null && workTabWindowType.IsInstanceOfType(__instance))
                return;
            if (!WorkGridOverlay.IsWorkTable(WindowTableRef(__instance)))
                return;

            // Vanilla tab: top-right is free (checkbox top-left, priority labels center).
            DrawAutoControl(new Rect(__0.x, __0.y + 6f, __0.width - 8f, 24f), alignRight: true);
        });

        public static void WorkTabWindowPostfix(MainTabWindow_PawnTable __instance, Rect __0) => Guard("worktab-window", () =>
        {
            if (Event.current.type == EventType.Layout)
                return;
            if (!WorkGridOverlay.IsWorkTable(WindowTableRef(__instance)))
                return;

            // Work Tab occupies top-right (toggle buttons) and center (priority labels); its left is
            // free. Draw after its own content so nothing overdraws us.
            DrawAutoControl(new Rect(__0.x + 8f, __0.y + 4f, __0.width, 24f), alignRight: false);
        });

        // Draws "Auto [x]  (Optimize)" tightly, anchored to the left or right of the band.
        private static void DrawAutoControl(Rect band, bool alignRight)
        {
            var map = Find.CurrentMap;
            var comp = AdaptivePrioritiesGameComponent.Current;
            if (map == null || comp == null)
                return;

            string label = "AP_AutoModeLabel".Translate();
            Text.Font = GameFont.Small;
            float labelW = Text.CalcSize(label).x;
            bool auto = comp.autoMode;
            const float box = 24f, gapLabel = 6f, gapBtn = 8f, btnW = 64f;
            float w = labelW + gapLabel + box + (auto ? 0f : gapBtn + btnW);
            float x = alignRight ? band.xMax - w : band.x;
            float y = band.y;

            var prevAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x, y, labelW, box), label);
            Text.Anchor = prevAnchor;

            Widgets.Checkbox(new Vector2(x + labelW + gapLabel, y), ref auto, box);
            TooltipHandler.TipRegion(new Rect(x, y, w, box), "AP_AutoModeTabTip".Translate());
            if (auto != comp.autoMode)
            {
                comp.autoMode = auto;
                if (auto)
                    comp.RunAutoRecalc();
            }

            if (!comp.autoMode)
            {
                var btnRect = new Rect(x + labelW + gapLabel + box + gapBtn, y, btnW, box);
                if (Widgets.ButtonText(btnRect, "AP_Optimize".Translate()))
                {
                    int changed = ColonyPriorityAssigner.ApplyProposal(map);
                    Messages.Message("AP_RecalculateDone".Translate(changed), MessageTypeDefOf.TaskCompletion, historical: false);
                }
            }
        }
    }
}
