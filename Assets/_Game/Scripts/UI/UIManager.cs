using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DontAnswer
{
    /// <summary>
    /// Builds every piece of UI at runtime (no prefabs) and exposes small methods for the game to call.
    /// Split into partial files: this one (HUD, subtitles, dialogue, fades), .Phone (phone + note), .Menus.
    /// </summary>
    public partial class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Style")]
        [Tooltip("Leave empty to use Unity's built-in font. Drop a horror font here to restyle everything.")]
        public Font font;
        [Tooltip("Typewriter face for the note, memories, story cards and titles. Falls back to Font.")]
        public Font storyFont;
        [Tooltip("Digital face for the clock and the phone's status bar. Falls back to Font.")]
        public Font digitalFont;
        public Color textColor = new Color(0.93f, 0.91f, 0.87f);
        public Color accent = new Color(1f, 0.62f, 0.25f);
        public Color danger = new Color(0.9f, 0.16f, 0.12f);
        public Color muted = new Color(0.55f, 0.56f, 0.6f);
        public Color panelColor = new Color(0.02f, 0.02f, 0.03f, 0.86f);

        RectTransform root;
        Sprite rounded;

        CanvasGroup hud;
        Image crosshair;
        Text prompt, clock, hint, unread, tally;
        CanvasGroup hintGroup;

        GameObject peephole;

        GameObject subtitleBox;
        Text subtitle;
        int subtitleToken;

        GameObject choiceBox;
        Text choiceHeader;
        RectTransform choiceList;
        readonly List<Button> choiceButtons = new List<Button>();
        Action<int> choiceCallback;
        int choiceCount, choiceFrame;

        GameObject confirmBox;
        Text confirmText, confirmYes, confirmNo;
        Action<bool> confirmCallback;

        RectTransform toast;
        Text toastTitle, toastBody;
        Image toastBg;
        CanvasGroup toastGroup;
        Coroutine toastRoutine;

        Image fade;
        Text card;

        public bool ConfirmOpen => confirmBox != null && confirmBox.activeSelf;
        public bool ChoicesOpen => choiceBox != null && choiceBox.activeSelf;

        void Awake()
        {
            Instance = this;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (storyFont == null) storyFont = font;
            if (digitalFont == null) digitalFont = font;
            rounded = MakeRoundedSprite(64, 16);
            Build();
        }

        void Update()
        {
            if (ChoicesOpen && Time.frameCount > choiceFrame)
            {
                int n = GameInput.ChoiceNumber;
                if (n >= 0 && n < choiceCount) Choose(n);
            }
        }

        // ================================================================== construction

        void Build()
        {
            if (GetComponentInChildren<EventSystem>() == null && EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = 5;
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            root = canvasGo.GetComponent<RectTransform>();

            BuildPeephole();
            BuildHud();
            BuildSubtitles();
            BuildChoices();
            BuildPhone();
            BuildNote();
            BuildConfirm();
            BuildToast();
            BuildExtras();
            BuildFade();
            BuildMenus();
        }

        void BuildPeephole()
        {
            var rt = Rect("Peephole", root);
            Stretch(rt);
            peephole = rt.gameObject;

            var maskRt = Rect("Mask", rt);
            Place(maskRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 1080f));
            var raw = maskRt.gameObject.AddComponent<RawImage>();
            raw.texture = MakePeepholeTexture(512);
            raw.raycastTarget = false;

            var left = Img(rt, "Left", Color.black);
            left.rectTransform.anchorMin = new Vector2(0f, 0f);
            left.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            left.rectTransform.offsetMin = Vector2.zero;
            left.rectTransform.offsetMax = new Vector2(-538f, 0f);
            var right = Img(rt, "Right", Color.black);
            right.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            right.rectTransform.anchorMax = new Vector2(1f, 1f);
            right.rectTransform.offsetMin = new Vector2(538f, 0f);
            right.rectTransform.offsetMax = Vector2.zero;

            var help = Txt(rt, "Help", "[E] / [Right Click]   Step back", 24, muted, TextAnchor.MiddleCenter);
            Place(help.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 40f));
            peephole.SetActive(false);
        }

        void BuildHud()
        {
            var rt = Rect("HUD", root);
            Stretch(rt);
            hud = rt.gameObject.AddComponent<CanvasGroup>();
            hud.interactable = false;
            hud.blocksRaycasts = false;

            crosshair = Img(rt, "Crosshair", new Color(1f, 1f, 1f, 0.55f));
            crosshair.sprite = rounded;
            Place(crosshair.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));

            prompt = Txt(rt, "Prompt", "", 28, textColor, TextAnchor.MiddleCenter);
            Place(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(900f, 44f));
            Shadowed(prompt, 2f);

            clock = Digital(Txt(rt, "Clock", "", 40, textColor, TextAnchor.UpperRight));
            Place(clock.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -34f), new Vector2(400f, 60f));
            Shadowed(clock, 2f);

            // How the night is going: the two ways to lose it, always in the corner.
            tally = Txt(rt, "Tally", "", 21, muted, TextAnchor.UpperRight);
            Place(tally.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -86f), new Vector2(700f, 30f));
            Shadowed(tally, 2f);

            unread = Txt(rt, "Unread", "", 22, accent, TextAnchor.UpperRight);
            Place(unread.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -118f), new Vector2(500f, 34f));
            Shadowed(unread, 2f);

            var hintRt = Rect("HintGroup", rt);
            Place(hintRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -38f), new Vector2(1000f, 120f));
            hintGroup = hintRt.gameObject.AddComponent<CanvasGroup>();
            hint = Txt(hintRt, "Hint", "", 27, accent, TextAnchor.UpperLeft);
            Stretch(hint.rectTransform);
            Shadowed(hint, 2f);
        }

        void BuildSubtitles()
        {
            var box = Img(root, "Subtitles", new Color(0f, 0f, 0f, 0.6f));
            box.sprite = rounded;
            box.type = Image.Type.Sliced;
            Place(box.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1320f, 130f));
            subtitleBox = box.gameObject;
            subtitle = Txt(box.rectTransform, "Text", "", 33, textColor, TextAnchor.MiddleCenter);
            Stretch(subtitle.rectTransform, 30f, 14f);
            Shadowed(subtitle, 2f);
            subtitleBox.SetActive(false);
        }

        void BuildChoices()
        {
            var box = Img(root, "Choices", panelColor);
            box.sprite = rounded;
            box.type = Image.Type.Sliced;
            box.raycastTarget = true;
            Place(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(70f, 40f), new Vector2(640f, 560f));
            choiceBox = box.gameObject;

            choiceHeader = Txt(box.rectTransform, "Header", "INTERCOM", 24, accent, TextAnchor.UpperLeft, FontStyle.Bold);
            Place(choiceHeader.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -22f), new Vector2(580f, 36f));

            choiceList = Rect("List", box.rectTransform);
            choiceList.anchorMin = new Vector2(0f, 0f);
            choiceList.anchorMax = new Vector2(1f, 1f);
            choiceList.offsetMin = new Vector2(24f, 24f);
            choiceList.offsetMax = new Vector2(-24f, -70f);
            var vlg = choiceList.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            for (int i = 0; i < 6; i++)
            {
                int index = i;
                var b = Btn(choiceList, "Choice", () => Choose(index), new Vector2(580f, 64f), 27, TextAnchor.MiddleLeft);
                choiceButtons.Add(b);
            }
            choiceBox.SetActive(false);
        }

        void BuildConfirm()
        {
            var dim = Img(root, "ConfirmDim", new Color(0f, 0f, 0f, 0.45f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            confirmBox = dim.gameObject;

            var box = Img(dim.rectTransform, "Box", panelColor);
            box.sprite = rounded;
            box.type = Image.Type.Sliced;
            Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 300f));

            confirmText = Txt(box.rectTransform, "Text", "", 38, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(confirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(660f, 130f));

            var yes = Btn(box.rectTransform, "Open it", () => AnswerConfirm(true), new Vector2(280f, 72f), 30, TextAnchor.MiddleCenter);
            Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-155f, 36f), new Vector2(280f, 72f));
            confirmYes = yes.GetComponentInChildren<Text>();
            confirmYes.color = danger;
            var no = Btn(box.rectTransform, "Don't", () => AnswerConfirm(false), new Vector2(280f, 72f), 30, TextAnchor.MiddleCenter);
            Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(155f, 36f), new Vector2(280f, 72f));
            confirmNo = no.GetComponentInChildren<Text>();
            confirmBox.SetActive(false);
        }

        void BuildToast()
        {
            toastBg = Img(root, "Toast", new Color(0.06f, 0.065f, 0.085f, 0.95f));
            toastBg.sprite = rounded;
            toastBg.type = Image.Type.Sliced;
            toast = toastBg.rectTransform;
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            Place(toast, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -160f), new Vector2(560f, 104f));
            toastTitle = Txt(toast, "Title", "", 24, accent, TextAnchor.UpperLeft, FontStyle.Bold);
            Place(toastTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -14f), new Vector2(516f, 34f));
            toastBody = Txt(toast, "Body", "", 22, textColor, TextAnchor.UpperLeft);
            Place(toastBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -48f), new Vector2(516f, 50f));
            toast.gameObject.SetActive(false);
        }

        void BuildFade()
        {
            fade = Img(root, "Fade", Color.black);
            Stretch(fade.rectTransform);
            fade.color = new Color(0f, 0f, 0f, 0f);
            card = Story(Txt(root, "Card", "", 46, textColor, TextAnchor.MiddleCenter));
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 600f));
            card.lineSpacing = 1.15f;
            card.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
        }

        // ================================================================== HUD API

        public void SetHudVisible(bool visible)
        {
            hud.alpha = visible ? 1f : 0f;
            if (!visible)
            {
                subtitleBox.SetActive(false);
                toast.gameObject.SetActive(false);
            }
        }

        public void SetPrompt(string text)
        {
            bool has = !string.IsNullOrEmpty(text);
            if (prompt.text != (text ?? "")) prompt.text = text ?? "";
            crosshair.color = has ? accent : new Color(1f, 1f, 1f, 0.55f);
            crosshair.rectTransform.sizeDelta = has ? new Vector2(11f, 11f) : new Vector2(6f, 6f);
        }

        public void SetCrosshairVisible(bool visible) => crosshair.enabled = visible;

        int lastUnread = -1;

        public void SetUnread(int count)
        {
            if (count == lastUnread) return;
            lastUnread = count;
            unread.text = count <= 0 ? "" : "[TAB]  " + count + (count == 1 ? " new message" : " new messages");
        }

        public void SetClock(string text)
        {
            if (clock.text != text) clock.text = text;
            SetPhoneTime(text);
        }

        int lastLeft = -1, lastGotIn = -1;

        /// <summary>The two ways to lose the night: the living you left outside, and the times they got in.</summary>
        public void SetTally(int leftOutside, int maxLeftOutside, int gotIn, int maxGotIn)
        {
            if (leftOutside == lastLeft && gotIn == lastGotIn) return;
            lastLeft = leftOutside;
            lastGotIn = gotIn;
            string Part(string label, int n, int max)
            {
                string s = label + "  " + n + " / " + max;
                return n >= max - 1 ? "<color=#" + ColorUtility.ToHtmlStringRGB(danger) + ">" + s + "</color>" : s;
            }
            tally.text = Part("LEFT OUTSIDE", leftOutside, maxLeftOutside) + "        " + Part("THEY GOT IN", gotIn, maxGotIn);
        }

        public void SetHint(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                hint.text = "";
                return;
            }
            hint.text = text;
            StartCoroutine(FadeGroup(hintGroup, 0f, 1f, 0.6f));
        }

        /// <summary>Clears the hint only if it's still the one given (a newer hint stays).</summary>
        public void ClearHintIf(string text)
        {
            if (hint.text == text) hint.text = "";
        }

        public void SetPeephole(bool on)
        {
            peephole.SetActive(on);
            crosshair.enabled = !on;
        }

        // ================================================================== subtitles

        public int ShowSubtitle(string speaker, Color speakerColor, string text)
        {
            subtitleToken++;
            subtitleBox.SetActive(true);
            subtitleSpeaker = string.IsNullOrEmpty(speaker) ? "" : "<color=#" + ColorUtility.ToHtmlStringRGB(speakerColor) + "><b>" + speaker + "</b></color>   ";
            subtitle.text = subtitleSpeaker + text;
            return subtitleToken;
        }

        string subtitleSpeaker = "";

        public void SetSubtitleText(int token, string text)
        {
            if (token != subtitleToken) return;
            subtitle.text = subtitleSpeaker + text;
        }

        public void HideSubtitle(int token)
        {
            if (token != subtitleToken) return;
            subtitleBox.SetActive(false);
        }

        public void HideSubtitleNow()
        {
            subtitleToken++;
            subtitleBox.SetActive(false);
        }

        // ================================================================== choices

        public void ShowChoices(string header, IList<string> options, IList<bool> dimmed, Action<int> callback)
        {
            choiceHeader.text = header;
            choiceCallback = callback;
            choiceCount = Mathf.Min(options.Count, choiceButtons.Count);
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                var b = choiceButtons[i];
                bool on = i < choiceCount;
                b.gameObject.SetActive(on);
                if (!on) continue;
                var label = b.GetComponentInChildren<Text>();
                label.text = "  " + (i + 1) + "    " + options[i];
                label.color = dimmed != null && i < dimmed.Count && dimmed[i] ? muted : textColor;
            }
            var size = ((RectTransform)choiceBox.transform).sizeDelta;
            ((RectTransform)choiceBox.transform).sizeDelta = new Vector2(size.x, 100f + choiceCount * 74f);
            choiceBox.SetActive(true);
            choiceFrame = Time.frameCount;
            EventSystem.current?.SetSelectedGameObject(null);
        }

        public void HideChoices()
        {
            choiceBox.SetActive(false);
            choiceCallback = null;
        }

        void Choose(int index)
        {
            var cb = choiceCallback;
            if (cb == null) return;
            if (AudioManager.Instance != null) AudioManager.Instance.UIClick();
            HideChoices();
            cb(index);
        }

        // ================================================================== confirm

        public void ShowConfirm(string text, string yes, string no, Action<bool> callback)
        {
            confirmText.text = text;
            confirmYes.text = yes;
            confirmNo.text = no;
            confirmCallback = callback;
            confirmBox.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        public void CancelConfirm()
        {
            if (!ConfirmOpen) return;
            AnswerConfirm(false);
        }

        void AnswerConfirm(bool yes)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.UIClick();
            confirmBox.SetActive(false);
            var cb = confirmCallback;
            confirmCallback = null;
            cb?.Invoke(yes);
        }

        // ================================================================== toast

        public void ShowToast(string title, string body, bool creepy, bool isMessage = false)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastTitle.text = (isMessage && !creepy ? "New message  -  " : "") + title;
            toastTitle.color = creepy ? danger : accent;
            toastBody.text = body;
            toastRoutine = StartCoroutine(ToastRoutine());
        }

        IEnumerator ToastRoutine()
        {
            toast.gameObject.SetActive(true);
            var cg = toastGroup;
            Vector2 home = new Vector2(-48f, -160f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 4f)
            {
                toast.anchoredPosition = home + new Vector2((1f - t) * 300f, 0f);
                cg.alpha = t;
                yield return null;
            }
            toast.anchoredPosition = home;
            cg.alpha = 1f;
            yield return new WaitForSecondsRealtime(4.5f);
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime * 2f)
            {
                cg.alpha = t;
                yield return null;
            }
            toast.gameObject.SetActive(false);
        }

        // ================================================================== fades & cards

        public void SetFade(float alpha) => fade.color = new Color(0f, 0f, 0f, alpha);

        public IEnumerator Fade(float target, float duration)
        {
            float start = fade.color.a;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration))
            {
                SetFade(Mathf.Lerp(start, target, t));
                yield return null;
            }
            SetFade(target);
        }

        public IEnumerator ShowCard(string text, float hold)
        {
            card.text = text;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 1.6f)
            {
                card.color = new Color(textColor.r, textColor.g, textColor.b, t);
                yield return null;
            }
            card.color = textColor;
            float waited = 0f;
            while (waited < hold)
            {
                waited += Time.unscaledDeltaTime;
                if (waited > 0.5f && GameInput.Skip) break;
                yield return null;
            }
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime * 1.6f)
            {
                card.color = new Color(textColor.r, textColor.g, textColor.b, t);
                yield return null;
            }
            card.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
        }

        IEnumerator FadeGroup(CanvasGroup g, float from, float to, float duration)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
            {
                g.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            g.alpha = to;
        }

        // ================================================================== factory helpers

        RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        Image Img(Transform parent, string name, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text Txt(Transform parent, string name, string content, int size, Color color, TextAnchor align, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Typewriter face, for the things that are part of the story.</summary>
        Text Story(Text t)
        {
            t.font = storyFont;
            return t;
        }

        /// <summary>Digital face. VT323 sits small in its box, so it gets a size bump.</summary>
        Text Digital(Text t)
        {
            if (digitalFont != font) t.fontSize = Mathf.RoundToInt(t.fontSize * 1.35f);
            t.font = digitalFont;
            t.fontStyle = FontStyle.Normal;
            return t;
        }

        static void Shadowed(Graphic g, float distance)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.9f);
            s.effectDistance = new Vector2(distance, -distance);
        }

        Button Btn(Transform parent, string label, Action onClick, Vector2 size, int fontSize, TextAnchor align)
        {
            var rt = Rect("Button " + label, parent);
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            var b = rt.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = new Color(0.12f, 0.12f, 0.15f, 0.95f);
            colors.highlightedColor = new Color(0.42f, 0.2f, 0.07f, 1f);
            colors.pressedColor = new Color(0.7f, 0.35f, 0.1f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.08f, 0.08f, 0.08f, 0.5f);
            colors.fadeDuration = 0.08f;
            b.colors = colors;
            var nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;
            b.onClick.AddListener(() => onClick());
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = size.y;
            le.minHeight = size.y;

            var t = Txt(rt, "Label", label, fontSize, textColor, align);
            Stretch(t.rectTransform, 20f, 0f);
            return b;
        }

        Slider MakeSlider(Transform parent, string label, float min, float max, float value, Action<float> onChange)
        {
            var row = Rect(label, parent);
            row.sizeDelta = new Vector2(660f, 50f);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 50f;

            var lbl = Txt(row, "Label", label, 26, textColor, TextAnchor.MiddleLeft);
            lbl.rectTransform.anchorMin = new Vector2(0f, 0f);
            lbl.rectTransform.anchorMax = new Vector2(0f, 1f);
            lbl.rectTransform.pivot = new Vector2(0f, 0.5f);
            lbl.rectTransform.sizeDelta = new Vector2(260f, 0f);
            lbl.rectTransform.anchoredPosition = Vector2.zero;

            var sRt = Rect("Slider", row);
            sRt.anchorMin = new Vector2(0f, 0.5f);
            sRt.anchorMax = new Vector2(1f, 0.5f);
            sRt.offsetMin = new Vector2(280f, -11f);
            sRt.offsetMax = new Vector2(0f, 11f);
            var slider = sRt.gameObject.AddComponent<Slider>();

            var bg = Img(sRt, "Background", new Color(1f, 1f, 1f, 0.14f));
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);

            var fillArea = Rect("Fill Area", sRt);
            Stretch(fillArea);
            var fill = Img(fillArea, "Fill", accent);
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = Rect("Handle Area", sRt);
            Stretch(handleArea, 11f, 0f);
            var handle = Img(handleArea, "Handle", Color.white);
            handle.sprite = rounded;
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(24f, 14f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            var nav = slider.navigation;
            nav.mode = Navigation.Mode.None;
            slider.navigation = nav;
            slider.onValueChanged.AddListener(v => onChange(v));
            return slider;
        }

        static Sprite MakeRoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "UIRounded"
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        static Texture2D MakePeepholeTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "PeepholeMask"
            };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 0.97f, d));
                float inner = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.9f, d)) * 0.45f;
                float a = Mathf.Max(edge, inner);
                px[y * size + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(a) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
