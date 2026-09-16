using UnityEngine;

namespace OpenNPC.Demo.UI
{
    /// <summary>
    /// Visual language of the demo UI, shared with the logo and the web page:
    /// black ink, off-white paper, Bebas Neue for names and headings, a monospace
    /// face for everything technical. Widgets read only from here.
    /// </summary>
    [CreateAssetMenu(menuName = "OpenNPC/UI Theme", fileName = "UITheme")]
    public sealed class UITheme : ScriptableObject
    {
        [Header("Fonts")]
        public Font display;
        public Font mono;
        public Font monoBold;

        [Header("Colours")]
        public Color ink = new Color32(0x11, 0x11, 0x11, 0xFF);
        public Color paper = new Color32(0xF3, 0xF1, 0xEC, 0xFF);
        public Color white = Color.white;
        public Color muted = new Color32(0x8A, 0x87, 0x80, 0xFF);
        public Color debugBackground = new Color32(0x11, 0x11, 0x11, 0xFF);
        public Color debugText = new Color32(0xE8, 0xE6, 0xE0, 0xFF);
        public Color debugDim = new Color32(0x6E, 0x6C, 0x66, 0xFF);

        [Header("Sizes (reference 1920x1080)")]
        public int bodySize = 22;
        public int smallSize = 16;
        public int nameSize = 30;
        public int headingSize = 56;
        public float border = 3f;
        public float bubbleMaxWidth = 440f;
        public Vector2 bubblePadding = new Vector2(20f, 16f);
    }
}
