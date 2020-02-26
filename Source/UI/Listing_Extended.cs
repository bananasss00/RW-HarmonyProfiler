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
    }
}