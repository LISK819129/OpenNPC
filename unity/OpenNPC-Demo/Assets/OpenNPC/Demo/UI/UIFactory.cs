using UnityEngine;
using UnityEngine.UI;

namespace OpenNPC.Demo.UI
{
    /// <summary>Tiny helpers for building uGUI hierarchies in code.</summary>
    public static class UIFactory
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static Image Box(string name, Transform parent, Color colour)
        {
            RectTransform rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = colour;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A filled box with an inset border: returns (outer ink, inner fill).</summary>
        public static (Image border, Image fill) BorderedBox(string name, Transform parent, Color borderColour,
                                                             Color fillColour, float thickness)
        {
            Image outer = Box(name, parent, borderColour);
            Image inner = Box("Fill", outer.transform, fillColour);
            Stretch(inner.rectTransform, thickness);
            return (outer, inner);
        }

        public static Text Label(string name, Transform parent, Font font, int size, Color colour,
                                 TextAnchor anchor = TextAnchor.UpperLeft)
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = colour;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.lineSpacing = 1.05f;
            return t;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        /// <summary>Preferred (width, height) of text wrapped at maxWidth, in canvas units.</summary>
        public static Vector2 Measure(Text text, string content, float maxWidth)
        {
            TextGenerationSettings unbounded = text.GetGenerationSettings(new Vector2(float.PositiveInfinity, 0f));
            TextGenerator gen = text.cachedTextGeneratorForLayout;
            float scale = text.pixelsPerUnit;
            float width = Mathf.Min(gen.GetPreferredWidth(content, unbounded) / scale, maxWidth);
            TextGenerationSettings wrapped = text.GetGenerationSettings(new Vector2(width + 1f, 0f));
            float height = gen.GetPreferredHeight(content, wrapped) / scale;
            return new Vector2(Mathf.Ceil(width) + 1f, Mathf.Ceil(height));
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }
}
