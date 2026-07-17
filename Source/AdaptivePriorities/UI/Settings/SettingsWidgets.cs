using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AdaptivePriorities.UI.Settings
{
    /// <summary>
    /// The shared row kit for the settings tabs: label | slider | fixed value column | revert arrow.
    /// Values line up in one right-hand column so they can be scanned vertically, and the revert
    /// arrow doubles as the "you changed this" marker — it only exists while the key is overridden,
    /// and clicking it clears just that key. All rows write through AdaptivePrioritiesSettings'
    /// implicit-override model, so dragging a slider back onto its default also clears the override.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SettingsWidgets
    {
        public const float RowHeight = 30f;
        public const float LabelWidth = 260f;
        public const float ValueWidth = 54f;
        public const float RevertSize = 24f;
        public const float Pad = 8f;

        private static readonly Color DisabledTint = new Color(1f, 1f, 1f, 0.4f);
        private static readonly Color HeaderColor = new Color(1f, 1f, 1f, 0.6f);

        // Same art vanilla's slider uses; drawn by our own slider below.
        private static readonly Texture2D SliderRailAtlas = ContentFinder<Texture2D>.Get("UI/Buttons/SliderRail");
        private static readonly Texture2D SliderHandleTex = ContentFinder<Texture2D>.Get("UI/Buttons/SliderHandle");

        private static string draggingKey;
        private static float lastDragSoundTime;

        private static AdaptivePrioritiesSettings Settings => AdaptivePrioritiesMod.Settings;

        /// <summary>
        /// Vanilla-look slider with our own drag tracking. Widgets.HorizontalSlider identifies the
        /// dragged slider by a hash of its screen rect, which proved unreliable inside this window's
        /// tabbed scroll views (sliders froze); keying the drag by the settings key instead is
        /// deterministic, and makes the whole cell height grabbable. Returns the unrounded value.
        /// </summary>
        public static float Slider(Rect cell, string dragKey, float value, float min, float max)
        {
            float centerY = cell.y + cell.height / 2f;
            var rail = new Rect(cell.x + 6f, centerY - 4f, cell.width - 12f, 8f);
            Widgets.DrawAtlas(rail, SliderRailAtlas);
            float t = max > min ? Mathf.InverseLerp(min, max, value) : 0f;
            float handleCenter = Mathf.Lerp(rail.x, rail.xMax, t);
            GUI.DrawTexture(new Rect(handleCenter - 6f, centerY - 6f, 12f, 12f), SliderHandleTex);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Mouse.IsOver(cell))
            {
                draggingKey = dragKey;
                SoundDefOf.DragSlider.PlayOneShotOnCamera();
                Event.current.Use();
            }

            if (draggingKey == dragKey)
            {
                // rawType so the release is seen even if another widget consumed the MouseUp.
                if (Event.current.rawType == EventType.MouseUp)
                {
                    draggingKey = null;
                }
                else if (UnityGUIBugsFixer.MouseDrag())
                {
                    float dragged = Mathf.Clamp(
                        (Event.current.mousePosition.x - rail.x) / rail.width * (max - min) + min, min, max);
                    if (Event.current.type == EventType.MouseDrag)
                        Event.current.Use();
                    if (dragged != value && Time.realtimeSinceStartup > lastDragSoundTime + 0.075f)
                    {
                        SoundDefOf.DragSlider.PlayOneShotOnCamera();
                        lastDragSoundTime = Time.realtimeSinceStartup;
                    }
                    value = dragged;
                }
            }

            return value;
        }

        /// <summary>Standard row geometry. Rows that need custom columns carve their own rects.</summary>
        public static void Split(Rect rect, float labelWidth, out Rect label, out Rect slider, out Rect value, out Rect revert)
        {
            label = new Rect(rect.x, rect.y, labelWidth, rect.height);
            revert = new Rect(rect.xMax - RevertSize, rect.y + (rect.height - RevertSize) / 2f, RevertSize, RevertSize);
            value = new Rect(revert.x - Pad - ValueWidth, rect.y, ValueWidth, rect.height);
            slider = new Rect(label.xMax + Pad, rect.y, value.x - label.xMax - 2f * Pad, rect.height);
        }

        public static void FloatRow(Rect rect, string label, string key, float defaultValue, float min, float max,
            Func<string> tip, bool disabled = false, float labelWidth = LabelWidth, float roundTo = 0.01f)
        {
            Split(rect, labelWidth, out var labelRect, out var sliderRect, out var valueRect, out var revertRect);
            RowChrome(rect, labelRect, label, key, tip, disabled);

            float cur = Settings.GetFloat(key, defaultValue);
            DrawValue(valueRect, cur.ToString("0.00"), disabled);

            if (disabled)
            {
                DrawInertSlider(sliderRect, cur, min, max);
            }
            else
            {
                // Rounding applies only to an actual change: re-rounding the current value every frame
                // would turn an off-grid default (e.g. a derived 0.643 urgency) into a phantom override.
                float raw = Slider(sliderRect, key, cur, min, max);
                if (!Mathf.Approximately(raw, cur))
                {
                    float v = roundTo > 0f ? Mathf.Round(raw / roundTo) * roundTo : raw;
                    Settings.SetFloat(key, v, defaultValue);
                }
            }

            DrawKeyRevert(revertRect, key);
        }

        public static void IntRow(Rect rect, string label, string key, int defaultValue, int min, int max,
            Func<string> tip, bool disabled = false, float labelWidth = LabelWidth)
        {
            Split(rect, labelWidth, out var labelRect, out var sliderRect, out var valueRect, out var revertRect);
            RowChrome(rect, labelRect, label, key, tip, disabled);

            int cur = Settings.GetInt(key, defaultValue);
            DrawValue(valueRect, cur.ToString(), disabled);

            if (disabled)
            {
                DrawInertSlider(sliderRect, cur, min, max);
            }
            else
            {
                float raw = Slider(sliderRect, key, cur, min, max);
                if (!Mathf.Approximately(raw, cur))
                {
                    int v = Mathf.RoundToInt(raw);
                    if (v != cur)
                        Settings.SetInt(key, v, defaultValue);
                }
            }

            DrawKeyRevert(revertRect, key);
        }

        public static void BoolRow(Rect rect, string label, string key, bool defaultValue,
            Func<string> tip, bool disabled = false, float labelWidth = LabelWidth)
        {
            Split(rect, labelWidth, out var labelRect, out _, out var valueRect, out var revertRect);
            RowChrome(rect, labelRect, label, key, tip, disabled);

            bool cur = Settings.GetBool(key, defaultValue);
            bool v = cur;
            // Checkbox sits in the value column so on/off states line up with the numeric readouts.
            Widgets.Checkbox(new Vector2(valueRect.xMax - 24f, rect.y + (rect.height - 24f) / 2f), ref v, 24f, disabled);
            if (!disabled && v != cur)
                Settings.SetBool(key, v, defaultValue);

            DrawKeyRevert(revertRect, key);
        }

        /// <summary>Non-interactive row (e.g. the no-passion baseline): label + fixed value, dimmed.</summary>
        public static void InfoRow(Rect rect, string label, string value, Func<string> tip, float labelWidth = LabelWidth)
        {
            Split(rect, labelWidth, out var labelRect, out _, out var valueRect, out _);
            RowChrome(rect, labelRect, label, label, tip, disabled: true);
            DrawValue(valueRect, value, disabled: true);
        }

        /// <summary>Mouseover highlight, lazy tooltip and the row label. Shared by every row.</summary>
        public static void RowChrome(Rect rowRect, Rect labelRect, string label, string tipId, Func<string> tip, bool disabled)
        {
            Widgets.DrawHighlightIfMouseover(rowRect);
            if (tip != null)
                TooltipHandler.TipRegion(rowRect, new TipSignal(tip, tipId.GetHashCode()));

            var prevColor = GUI.color;
            if (disabled)
                GUI.color = prevColor * DisabledTint;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = prevColor;
        }

        /// <summary>Right-aligned numeric readout so values form a scannable column.</summary>
        public static void DrawValue(Rect rect, string text, bool disabled = false)
        {
            var prevColor = GUI.color;
            if (disabled)
                GUI.color = prevColor * DisabledTint;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(rect, text);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = prevColor;
        }

        /// <summary>Revert arrow for one settings key; drawn only while the key is overridden.</summary>
        public static void DrawKeyRevert(Rect rect, string key)
        {
            if (!Settings.IsOverridden(key))
                return;
            DrawRevert(rect, "AP_RevertTip".Translate(), () => Settings.ClearKey(key));
        }

        /// <summary>Revert arrow with a caller-supplied action (work-type-level reset, interval reset...).</summary>
        public static void DrawRevert(Rect rect, string tooltip, Action revert)
        {
            TooltipHandler.TipRegion(rect, tooltip);
            if (Widgets.ButtonImage(rect, TexButton.Reload))
                revert();
        }

        /// <summary>
        /// Widgets.HorizontalSlider handles its own mouse events (it ignores GUI.enabled), so a
        /// disabled row draws this inert stand-in instead: same geometry, no interaction.
        /// </summary>
        public static void DrawInertSlider(Rect rect, float value, float min, float max)
        {
            var prevColor = GUI.color;
            GUI.color = prevColor * DisabledTint;
            float centerY = rect.y + rect.height / 2f;
            Widgets.DrawLineHorizontal(rect.x + 6f, centerY, rect.width - 12f);
            float t = max > min ? Mathf.InverseLerp(min, max, value) : 0f;
            float handleX = Mathf.Lerp(rect.x + 6f, rect.xMax - 6f, t);
            Widgets.DrawBoxSolid(new Rect(handleX - 3f, centerY - 5f, 6f, 10f), new Color(0.7f, 0.7f, 0.7f, 0.6f));
            GUI.color = prevColor;
        }

        /// <summary>Dimmed column-header label (anchor configurable so headers align with their column).</summary>
        public static void DrawHeaderLabel(Rect rect, string text, TextAnchor anchor)
        {
            var prevColor = GUI.color;
            GUI.color = prevColor * HeaderColor;
            Text.Anchor = anchor;
            Widgets.Label(rect, text);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = prevColor;
        }

        /// <summary>
        /// Lazy tooltip builder: description, then "Default: X", then an optional live-math line and an
        /// optional "why this row is disabled" line. Only ever evaluated while actually hovered.
        /// </summary>
        public static Func<string> Tip(Func<string> description, Func<string> defaultValue,
            Func<string> liveLine = null, Func<string> disabledReason = null)
        {
            return () =>
            {
                string text = description();
                if (defaultValue != null)
                    text += "\n\n" + "AP_TipDefault".Translate(defaultValue());
                if (liveLine != null)
                {
                    string live = liveLine();
                    if (!live.NullOrEmpty())
                        text += "\n" + live;
                }
                if (disabledReason != null)
                {
                    string reason = disabledReason();
                    if (!reason.NullOrEmpty())
                        text += "\n\n" + reason;
                }
                return text;
            };
        }
    }
}
