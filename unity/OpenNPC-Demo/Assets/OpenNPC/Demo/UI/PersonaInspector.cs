using System.Text;
using OpenNPC.Demo.NPC;
using OpenNPC.Personas;
using UnityEngine;
using UnityEngine.UI;

namespace OpenNPC.Demo.UI
{
    /// <summary>
    /// The persona card (P). Proof that each NPC is backed by its own personality
    /// definition: exactly the data the dialogue provider receives.
    /// </summary>
    public sealed class PersonaInspector : MonoBehaviour
    {
        private UITheme _theme;
        private Text _name;
        private Text _body;
        private RectTransform _root;
        private string _shownId;

        public bool IsOpen => gameObject.activeSelf;

        public void Initialize(UITheme theme)
        {
            _theme = theme;
            _root = (RectTransform)transform;
            UIFactory.Anchor(_root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -130f), new Vector2(520f, 640f));
            (Image border, Image fill) = UIFactory.BorderedBox("Card", _root, theme.ink, theme.white, theme.border);
            UIFactory.Stretch(border.rectTransform);

            Text tag = UIFactory.Label("Tag", fill.transform, theme.monoBold, theme.smallSize, theme.ink);
            UIFactory.Anchor(tag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -20f), new Vector2(460f, 24f));
            tag.text = "PERSONA  ·  P to close";

            _name = UIFactory.Label("Name", fill.transform, theme.display, theme.headingSize, theme.ink);
            UIFactory.Anchor(_name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -44f), new Vector2(460f, 64f));

            _body = UIFactory.Label("Body", fill.transform, theme.mono, theme.smallSize, theme.ink);
            UIFactory.Anchor(_body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -116f), new Vector2(460f, 500f));
            gameObject.SetActive(false);
        }

        public void Toggle(NPCController npc)
        {
            if (IsOpen || npc == null)
                gameObject.SetActive(false);
            else
                Show(npc.Persona.Persona);
        }

        public void Show(Persona p)
        {
            if (p == null)
                return;
            gameObject.SetActive(true);
            _shownId = p.id;
            _name.text = p.name.ToUpperInvariant();
            string dim = UIFactory.Hex(_theme.muted);
            var sb = new StringBuilder();
            void Field(string label, string value)
            {
                if (string.IsNullOrEmpty(value)) return;
                sb.Append("<color=").Append(dim).Append('>').Append(label).Append("</color>\n").Append(value).Append("\n\n");
            }
            Field("ID", p.id + (p.IsGenerated ? "   (generated)" : "   (authored)"));
            Field("OCCUPATION", p.age > 0 ? $"{p.occupation}, {p.age}" : p.occupation);
            Field("TRAITS", string.Join("  ·  ", p.personalityTraits));
            Field("MOOD", p.mood);
            Field("BACKGROUND", p.background);
            Field("SPEAKING STYLE", p.speakingStyle);
            Field("LIKES / DISLIKES", string.Join(", ", p.likes) + "  /  " + string.Join(", ", p.dislikes));
            Field("KNOWS ABOUT", string.Join(", ", p.knowledge.ConvertAll(k => k.topic)));
            _body.text = sb.ToString().TrimEnd();
            Vector2 size = UIFactory.Measure(_body, _body.text, 460f);
            _root.sizeDelta = new Vector2(520f, 150f + size.y);
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>Keep the card in sync if the NPC's mood changes mid-conversation.</summary>
        public void Refresh(NPCController npc)
        {
            if (IsOpen && npc != null && npc.Persona.NpcId == _shownId)
                Show(npc.Persona.Persona);
        }
    }
}
