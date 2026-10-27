using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DontAnswer
{
    public partial class UIManager
    {
        GameObject blinds;
        RectTransform letterTop, letterBottom;
        Coroutine letterboxRoutine;
        Image holdRing;
        GameObject memoryRoot;
        Text memoryTitle, memoryBody, memoryKnock;

        void BuildExtras()
        {
            // Blinds: dark slats over the view when you peek through them.
            var b = Rect("Blinds", root);
            Stretch(b);
            blinds = b.gameObject;
            for (int i = 0; i < 9; i++)
            {
                var slat = Img(b, "Slat", new Color(0.82f, 0.78f, 0.7f, 1f));
                slat.rectTransform.anchorMin = new Vector2(0f, 0f);
                slat.rectTransform.anchorMax = new Vector2(1f, 0f);
                slat.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                float y = 1080f * (i / 8f);
                slat.rectTransform.anchoredPosition = new Vector2(0f, y + (Mathf.Abs(i - 4) < 1 ? 0f : 0f));
                slat.rectTransform.sizeDelta = new Vector2(0f, Mathf.Abs(i - 4) <= 1 ? 26f : 92f);
                slat.color = Mathf.Abs(i - 4) <= 1 ? new Color(0.25f, 0.23f, 0.2f, 0.95f) : new Color(0.18f, 0.17f, 0.15f, 1f);
            }
            var bh = Txt(b, "Help", "[E] / [Right Click]   Let the blinds go", 24, muted, TextAnchor.MiddleCenter);
            Place(bh.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(900f, 40f));
            blinds.SetActive(false);

            // Cinematic bars.
            letterTop = Img(root, "LetterTop", Color.black).rectTransform;
            letterTop.anchorMin = new Vector2(0f, 1f);
            letterTop.anchorMax = new Vector2(1f, 1f);
            letterTop.pivot = new Vector2(0.5f, 1f);
            letterTop.sizeDelta = new Vector2(0f, 0f);
            letterBottom = Img(root, "LetterBottom", Color.black).rectTransform;
            letterBottom.anchorMin = new Vector2(0f, 0f);
            letterBottom.anchorMax = new Vector2(1f, 0f);
            letterBottom.pivot = new Vector2(0.5f, 0f);
            letterBottom.sizeDelta = new Vector2(0f, 0f);

            // Hold-to-use ring around the crosshair.
            holdRing = Img(root, "HoldRing", accent);
            holdRing.sprite = RingSprite(64);
            holdRing.type = Image.Type.Filled;
            holdRing.fillMethod = Image.FillMethod.Radial360;
            holdRing.fillOrigin = (int)Image.Origin360.Top;
            holdRing.fillAmount = 0f;
            Place(holdRing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 54f));

            // Memory viewer.
            var dim = Img(root, "Memory", new Color(0f, 0f, 0f, 0.72f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            memoryRoot = dim.gameObject;
            var card = Img(dim.rectTransform, "Card", new Color(0.9f, 0.87f, 0.8f));
            card.sprite = rounded;
            card.type = Image.Type.Sliced;
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(860f, 560f));
            card.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
            memoryTitle = Story(Txt(card.rectTransform, "Title", "", 40, new Color(0.15f, 0.12f, 0.1f), TextAnchor.UpperCenter, FontStyle.Bold));
            Place(memoryTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(760f, 60f));
            memoryBody = Story(Txt(card.rectTransform, "Body", "", 28, new Color(0.18f, 0.15f, 0.13f), TextAnchor.UpperLeft));
            Place(memoryBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(740f, 300f));
            memoryKnock = Story(Txt(card.rectTransform, "Knock", "", 64, new Color(0.45f, 0.1f, 0.08f), TextAnchor.MiddleCenter, FontStyle.Bold));
            Place(memoryKnock.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(760f, 90f));
            var foot = Txt(dim.rectTransform, "Footer", "[E]  Put it down", 24, muted, TextAnchor.MiddleCenter);
            Place(foot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(600f, 30f));
            memoryRoot.SetActive(false);
        }

        public void SetBlinds(bool on)
        {
            blinds.SetActive(on);
            crosshair.enabled = !on && !peephole.activeSelf;
        }

        public void SetLetterbox(bool on, bool instant)
        {
            if (letterboxRoutine != null) StopCoroutine(letterboxRoutine);
            float target = on ? 120f : 0f;
            if (instant)
            {
                letterTop.sizeDelta = new Vector2(0f, target);
                letterBottom.sizeDelta = new Vector2(0f, target);
                hud.alpha = on ? 0f : hud.alpha;
                return;
            }
            letterboxRoutine = StartCoroutine(Letterbox(target));
        }

        IEnumerator Letterbox(float target)
        {
            float start = letterTop.sizeDelta.y;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 2.5f)
            {
                float h = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                letterTop.sizeDelta = new Vector2(0f, h);
                letterBottom.sizeDelta = new Vector2(0f, h);
                yield return null;
            }
            letterTop.sizeDelta = new Vector2(0f, target);
            letterBottom.sizeDelta = new Vector2(0f, target);
        }

        public void SetHoldProgress(float t)
        {
            holdRing.fillAmount = Mathf.Clamp01(t);
        }

        public void ShowMemory(string title, string body, bool knock)
        {
            memoryTitle.text = title;
            memoryBody.text = body;
            memoryKnock.text = knock ? "• •          •          • •" : "";
            memoryRoot.SetActive(true);
        }

        public void HideMemory() => memoryRoot.SetActive(false);

        static Sprite RingSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Ring" };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half));
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - half * 0.78f) / (half * 0.12f));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
