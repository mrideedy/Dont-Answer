using UnityEngine;
using UnityEngine.UI;

namespace DontAnswer
{
    public partial class UIManager
    {
        GameObject phoneRoot;
        Text phoneTime, phoneEmpty;
        RectTransform phoneContent;
        ScrollRect phoneScroll;

        GameObject noteRoot;
        Text noteText;
        int phoneTab;
        Text tabMessages, tabMemories, phoneTitle;

        public bool PhoneVisible => phoneRoot != null && phoneRoot.activeSelf;

        void BuildPhone()
        {
            var frame = Img(root, "Phone", new Color(0.015f, 0.015f, 0.02f, 1f));
            frame.sprite = rounded;
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = true;
            Place(frame.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-70f, 40f), new Vector2(480f, 840f));
            phoneRoot = frame.gameObject;

            var screen = Img(frame.rectTransform, "Screen", new Color(0.065f, 0.07f, 0.095f, 1f));
            screen.sprite = rounded;
            screen.type = Image.Type.Sliced;
            Stretch(screen.rectTransform, 14f, 14f);
            var s = screen.rectTransform;

            phoneTime = Digital(Txt(s, "Time", "", 22, textColor, TextAnchor.MiddleLeft));
            Place(phoneTime.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(220f, 30f));
            var status = Digital(Txt(s, "Status", "LTE    23%", 20, muted, TextAnchor.MiddleRight));
            Place(status.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -12f), new Vector2(220f, 30f));
            phoneTitle = Txt(s, "Title", "Messages", 34, textColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(phoneTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -48f), new Vector2(240f, 50f));
            var bm = Btn(s, "Texts", () => SetPhoneTab(0), new Vector2(92f, 40f), 20, TextAnchor.MiddleCenter);
            Place((RectTransform)bm.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-122f, -54f), new Vector2(92f, 40f));
            tabMessages = bm.GetComponentInChildren<Text>();
            var bmem = Btn(s, "Memories", () => SetPhoneTab(1), new Vector2(110f, 40f), 20, TextAnchor.MiddleCenter);
            Place((RectTransform)bmem.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -54f), new Vector2(110f, 40f));
            tabMemories = bmem.GetComponentInChildren<Text>();

            var viewport = Img(s, "Viewport", new Color(0f, 0f, 0f, 0f));
            viewport.raycastTarget = true;
            viewport.rectTransform.anchorMin = Vector2.zero;
            viewport.rectTransform.anchorMax = Vector2.one;
            viewport.rectTransform.offsetMin = new Vector2(12f, 56f);
            viewport.rectTransform.offsetMax = new Vector2(-12f, -110f);
            viewport.gameObject.AddComponent<RectMask2D>();

            phoneContent = Rect("Content", viewport.rectTransform);
            phoneContent.anchorMin = new Vector2(0f, 1f);
            phoneContent.anchorMax = new Vector2(1f, 1f);
            phoneContent.pivot = new Vector2(0.5f, 1f);
            phoneContent.sizeDelta = Vector2.zero;
            phoneContent.anchoredPosition = Vector2.zero;
            var vlg = phoneContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fit = phoneContent.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            phoneScroll = viewport.gameObject.AddComponent<ScrollRect>();
            phoneScroll.viewport = viewport.rectTransform;
            phoneScroll.content = phoneContent;
            phoneScroll.horizontal = false;
            phoneScroll.vertical = true;
            phoneScroll.movementType = ScrollRect.MovementType.Clamped;
            phoneScroll.scrollSensitivity = 35f;

            phoneEmpty = Txt(viewport.rectTransform, "Empty", "No messages.", 24, muted, TextAnchor.MiddleCenter);
            Stretch(phoneEmpty.rectTransform);

            var foot = Txt(s, "Footer", "[TAB]  Put away       scroll for older", 20, muted, TextAnchor.MiddleCenter);
            Place(foot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(430f, 30f));

            phoneRoot.SetActive(false);
        }

        public void ShowPhone(bool show)
        {
            phoneRoot.SetActive(show);
            if (show) ScrollPhoneToBottom();
        }

        void SetPhoneTab(int tab)
        {
            phoneTab = tab;
            if (GameDirector.Instance != null) RefreshPhone(GameDirector.Instance.phone);
        }

        public void RefreshPhone(PhoneSystem phone)
        {
            if (phoneContent == null) return;
            for (int i = phoneContent.childCount - 1; i >= 0; i--)
            {
                var child = phoneContent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            if (phoneTitle != null) phoneTitle.text = phoneTab == 0 ? "Messages" : "Memories";
            if (tabMessages != null) tabMessages.color = phoneTab == 0 ? accent : muted;
            if (tabMemories != null) tabMemories.color = phoneTab == 1 ? accent : muted;
            if (phoneTab == 1)
            {
                BuildMemoryList();
                if (PhoneVisible) ScrollPhoneToBottom();
                return;
            }
            phoneEmpty.text = "No messages.";
            phoneEmpty.gameObject.SetActive(phone.Inbox.Count == 0);

            foreach (var e in phone.Inbox)
            {
                var m = e;
                var bubble = Img(phoneContent, "Message", m.creepy ? new Color(0.24f, 0.035f, 0.035f, 1f) : new Color(0.13f, 0.14f, 0.19f, 1f));
                bubble.sprite = rounded;
                bubble.type = Image.Type.Sliced;
                var v = bubble.gameObject.AddComponent<VerticalLayoutGroup>();
                v.padding = new RectOffset(18, 18, 12, 14);
                v.spacing = 4f;
                v.childControlWidth = true;
                v.childControlHeight = true;
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;

                string col = ColorUtility.ToHtmlStringRGB(m.creepy ? danger : accent);
                string head = "<color=#" + col + "><b>" + m.sender + "</b></color>    <size=17><color=#8a8f9c>" + e.time +
                              (e.unread ? "   <b>NEW</b>" : "") + "</color></size>";
                Txt(bubble.rectTransform, "Head", head, 22, textColor, TextAnchor.UpperLeft);
                Txt(bubble.rectTransform, "Body", m.text, 23, textColor, TextAnchor.UpperLeft);
            }
            if (PhoneVisible) ScrollPhoneToBottom();
        }

        void BuildMemoryList()
        {
            phoneEmpty.gameObject.SetActive(false);
            AddMemoryEntry("Voicemail  -  Leo", "Saved 1 year ago, 11:52 PM.  You never called back.", () => GameDirector.Instance.PlayVoicemail());
            var gd = GameDirector.Instance;
            if (gd == null) return;
            foreach (var m in gd.FoundMemories())
            {
                var item = m;
                AddMemoryEntry(item.title, item.description, () => gd.ReplayMemory(item));
            }
        }

        void AddMemoryEntry(string title, string body, System.Action play)
        {
            var bubble = Img(phoneContent, "Memory", new Color(0.16f, 0.13f, 0.22f, 1f));
            bubble.sprite = rounded;
            bubble.type = Image.Type.Sliced;
            var v = bubble.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(18, 18, 12, 14);
            v.spacing = 6f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            Txt(bubble.rectTransform, "Head", "<b>" + title + "</b>", 22, new Color(0.8f, 0.75f, 1f), TextAnchor.UpperLeft);
            Txt(bubble.rectTransform, "Body", body, 20, textColor, TextAnchor.UpperLeft);
            Btn(bubble.rectTransform, "Play", () => play(), new Vector2(120f, 40f), 20, TextAnchor.MiddleCenter);
        }

        void ScrollPhoneToBottom()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(phoneContent);
            phoneScroll.verticalNormalizedPosition = 0f;
        }

        void SetPhoneTime(string time)
        {
            if (phoneTime != null && phoneTime.text != time) phoneTime.text = time;
        }

        void BuildNote()
        {
            var dim = Img(root, "Note", new Color(0f, 0f, 0f, 0.6f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            noteRoot = dim.gameObject;

            var paper = Img(dim.rectTransform, "Paper", new Color(0.89f, 0.85f, 0.74f));
            paper.sprite = rounded;
            paper.type = Image.Type.Sliced;
            Place(paper.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(920f, 990f));
            paper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -1.2f);

            noteText = Story(Txt(paper.rectTransform, "Text", "", 27, new Color(0.13f, 0.1f, 0.08f), TextAnchor.UpperLeft));
            Stretch(noteText.rectTransform, 58f, 48f);
            noteText.lineSpacing = 1.05f;
            // The rules grow during the night (Voss writes on them): shrink a little rather than run off the paper.
            noteText.resizeTextForBestFit = true;
            noteText.resizeTextMinSize = 20;
            noteText.resizeTextMaxSize = 27;
            noteText.verticalOverflow = VerticalWrapMode.Truncate;

            var foot = Txt(dim.rectTransform, "Footer", "[E]  Close", 24, muted, TextAnchor.MiddleCenter);
            Place(foot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(600f, 30f));
            noteRoot.SetActive(false);
        }

        public void ShowNote(string text)
        {
            noteText.text = text;
            noteRoot.SetActive(true);
        }

        public void HideNote() => noteRoot.SetActive(false);
    }
}
