using UnityEngine;
using Verse;

namespace HarmonyProfiler.UI
{
    public class Listing_Extended : Listing_Standard
    {
        public bool ButtonText(string label, float height = 30f, string highlightTag = null)
        {
            Rect rect = base.GetRect(height);
            bool result = Widgets.ButtonText(rect, label, true, false, true);
            if (highlightTag != null)
            {
                UIHighlighter.HighlightOpportunity(rect, highlightTag);
            }
            base.Gap(this.verticalSpacing);
            return result;
        }

        public void LabelColored(string text, Color color)
        {
            Color backupColor = GUI.color;
            GUI.color = color;
            base.Label(text);
            GUI.color = backupColor;
        }

        public string TextArea(string text, int linesCount, ref Vector2 scrollPos)
        {
            var rect = base.GetRect(Text.LineHeight * linesCount);
            return Widgets.TextAreaScrollable(rect, text, ref scrollPos);
        }

        public string TextAreaFocusControl(string fieldName, string text, int linesCount, ref Vector2 scrollPos)
        {
            var rect = base.GetRect(Text.LineHeight * linesCount);

            Rect rect1 = new Rect(0.0f, 0.0f, rect.width - 16f, Mathf.Max(Verse.Text.CalcHeight(text, rect.width) + 10f, rect.height));
            Widgets.BeginScrollView(rect, ref scrollPos, rect1);
            GUI.SetNextControlName(fieldName);
            string str = Widgets.TextArea(rect1, text);
            Widgets.EndScrollView();

            var inFocus = GUI.GetNameOfFocusedControl().Equals(fieldName);
            if (Input.GetMouseButtonDown(0) && !Mouse.IsOver(rect) && inFocus)
            {
                GUI.FocusControl(null);
            }

            return str;
        }
    }
}