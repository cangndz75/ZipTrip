using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZipTrip.Unity
{
    // UI-SLICE-01 paper/stationery UI language (STYLE-FRAME-01): tokens plus the two primitives every panel uses, a
    // rounded paper card with one warm drop shadow and a label. Nothing here receives raycasts: input is hit-tested
    // explicitly by the HUD (no EventSystem), so every Graphic keeps raycastTarget off.
    public static class PaperUi
    {
        private static readonly Dictionary<string, Sprite> Skins = new Dictionary<string, Sprite>();

        /// <summary>Authored, reusable stationery texture. Created once; the 32 px rim is retained by Image.Sliced.</summary>
        public static Sprite Skin(string name, int border = 32)
        {
            if (Skins.TryGetValue(name, out var sprite))
                return sprite;
            var texture = Resources.Load<Texture2D>("UiSlice011/" + name);
            if (texture == null)
                return null;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            Skins[name] = sprite;
            return sprite;
        }
        public static readonly Color Ink = PresentationKit.Hex(0x2F3A3C);
        public static readonly Color Muted = PresentationKit.Hex(0x7A7468);
        public static readonly Color Paper = PresentationKit.Hex(0xFBF7EE);
        public static readonly Color Cream = PresentationKit.Hex(0xF7F1E6);
        public static readonly Color ButtonFill = PresentationKit.Hex(0xEFE6D6);
        public static readonly Color Line = PresentationKit.Hex(0xEEE5D3);
        public static readonly Color Teal = PresentationKit.Teal;
        public static readonly Color Terracotta = PresentationKit.Terracotta;
        public static readonly Color Mustard = PresentationKit.Mustard;
        /// <summary>Medium-priority contextual action (Rotate): softer than the hero mustard.</summary>
        public static readonly Color SoftMustard = PresentationKit.Hex(0xEED6A2);
        public static readonly Color MustardEdge = PresentationKit.Hex(0xCDA454);
        public static readonly Color MustardUnder = PresentationKit.Hex(0xB0843A);
        public static readonly Color Pending = PresentationKit.Hex(0xB9B2A6);
        public static readonly Color Shadow = new Color(0.23f, 0.17f, 0.12f, 0.22f);

        /// <summary>
        /// Rounded paper card: an empty root holding a soft warm "Shadow" 8 px below and the "Face"; content added to the
        /// root draws above both. Returns the root.
        /// </summary>
        public static RectTransform Card(Transform parent, string name, Sprite rounded, Vector2 anchor, Vector2 position, Vector2 size,
            Color fill, float shadowAlpha = 0.22f, float drop = 8f, Sprite skin = null)
        {
            var card = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            card.SetParent(parent, false);
            card.anchorMin = card.anchorMax = anchor;
            card.anchoredPosition = position;
            card.sizeDelta = size;
            Image(card, "Shadow", skin != null ? skin : rounded, new Color(Shadow.r, Shadow.g, Shadow.b, shadowAlpha), size + new Vector2(8f, 8f))
                .anchoredPosition = new Vector2(0f, -drop);
            Image(card, "Face", skin != null ? skin : rounded, fill, size);
            return card;
        }

        /// <summary>Resizes a card built by <see cref="Card"/> (root, shadow and face).</summary>
        public static void Resize(RectTransform card, Vector2 size)
        {
            card.sizeDelta = size;
            ((RectTransform)card.Find("Shadow")).sizeDelta = size + new Vector2(8f, 8f);
            ((RectTransform)card.Find("Face")).sizeDelta = size;
        }

        public static UnityEngine.UI.Image Face(RectTransform card) => card.Find("Face").GetComponent<UnityEngine.UI.Image>();

        public static RectTransform Image(Transform parent, string name, Sprite sprite, Color color, Vector2 size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        public static Text Label(Transform parent, string text, Font font, int size, Color color, TextAnchor alignment, Vector2 position,
            Vector2 box)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var rect = label.rectTransform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            label.font = font;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color;
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }
    }
}
