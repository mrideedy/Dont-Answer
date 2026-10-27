using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DontAnswer
{
    public partial class UIManager
    {
        const string ControlsText =
            "WASD  move      MOUSE  look      E / CLICK  interact      F  light      TAB  phone      N  rules      ESC  pause";

        GameObject titleRoot, pauseRoot, deathRoot, endingRoot;
        Text deathTitle, deathHint, deathStats, endTitle, endSubtitle, endBody, endStats, deathRetryLabel, endPlayLabel;
        bool deathFinal;
        readonly List<Slider> sensitivitySliders = new List<Slider>();
        readonly List<Slider> volumeSliders = new List<Slider>();

        public bool AnyMenuOpen =>
            titleRoot.activeSelf || pauseRoot.activeSelf || deathRoot.activeSelf || endingRoot.activeSelf;

        static float Sensitivity => PlayerPrefs.GetFloat(PlayerController.SensitivityKey, 0.11f);

        void BuildMenus()
        {
            BuildTitle();
            BuildPause();
            BuildDeath();
            BuildEnding();
        }

        void BuildTitle()
        {
            var bg = Img(root, "Title", new Color(0f, 0f, 0f, 0.5f));
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);
            titleRoot = bg.gameObject;
            var r = bg.rectTransform;

            var name = Story(Txt(r, "Name", "DON'T ANSWER", 150, textColor, TextAnchor.MiddleCenter, FontStyle.Bold));
            Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1800f, 190f));
            var sh = name.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.55f, 0.04f, 0.03f, 0.95f);
            sh.effectDistance = new Vector2(6f, -6f);

            var tag = Story(Txt(r, "Tagline", "Some of the knocks tonight won't be people.", 34, accent, TextAnchor.MiddleCenter));
            Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(1600f, 50f));
            Shadowed(tag, 2f);

            var col = Column(r, new Vector2(0f, -10f), 660f);
            Btn(col, "PLAY", () => GameDirector.Instance.StartGame(), new Vector2(660f, 86f), 40, TextAnchor.MiddleCenter);
            Spacer(col, 20f);
            sensitivitySliders.Add(MakeSlider(col, "Mouse sensitivity", 0.02f, 0.4f, Sensitivity, SetSensitivity));
            volumeSliders.Add(MakeSlider(col, "Volume", 0f, 1f, AudioListener.volume, SetVolume));

            var controls = Txt(r, "Controls", ControlsText, 23, muted, TextAnchor.MiddleCenter);
            Place(controls.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1800f, 40f));
            titleRoot.SetActive(false);
        }

        void BuildPause()
        {
            var bg = Img(root, "Pause", new Color(0f, 0f, 0f, 0.75f));
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);
            pauseRoot = bg.gameObject;
            var r = bg.rectTransform;

            var t = Txt(r, "Title", "PAUSED", 96, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1200f, 130f));

            var col = Column(r, new Vector2(0f, 130f), 660f);
            Btn(col, "RESUME", () => GameDirector.Instance.ResumeGame(), new Vector2(660f, 76f), 34, TextAnchor.MiddleCenter);
            Btn(col, "RESTART THE NIGHT", () => GameDirector.Instance.RestartNight(), new Vector2(660f, 76f), 34, TextAnchor.MiddleCenter);
            Btn(col, "MAIN MENU", () => GameDirector.Instance.MainMenu(), new Vector2(660f, 76f), 34, TextAnchor.MiddleCenter);
            Spacer(col, 20f);
            sensitivitySliders.Add(MakeSlider(col, "Mouse sensitivity", 0.02f, 0.4f, Sensitivity, SetSensitivity));
            volumeSliders.Add(MakeSlider(col, "Volume", 0f, 1f, AudioListener.volume, SetVolume));

            var controls = Txt(r, "Controls", ControlsText, 23, muted, TextAnchor.MiddleCenter);
            Place(controls.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1800f, 40f));
            pauseRoot.SetActive(false);
        }

        void BuildDeath()
        {
            var bg = Img(root, "Death", new Color(0f, 0f, 0f, 0f));
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);
            deathRoot = bg.gameObject;
            var r = bg.rectTransform;

            deathTitle = Story(Txt(r, "Title", "IT GOT IN.", 130, danger, TextAnchor.MiddleCenter, FontStyle.Bold));
            Place(deathTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1800f, 190f));

            deathHint = Story(Txt(r, "Hint", "", 30, textColor, TextAnchor.MiddleCenter));
            Place(deathHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(1500f, 160f));
            deathStats = Txt(r, "Stats", "", 30, accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(deathStats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -520f), new Vector2(1500f, 46f));

            var col = Column(r, new Vector2(0f, -100f), 560f);
            var retry = Btn(col, "TRY AGAIN", () =>
            {
                // Out of chances: the night starts over. Otherwise, back to just before the last knock.
                if (deathFinal) GameDirector.Instance.RestartNight();
                else GameDirector.Instance.RetryFromCheckpoint();
            }, new Vector2(560f, 80f), 36, TextAnchor.MiddleCenter);
            deathRetryLabel = retry.GetComponentInChildren<Text>();
            Btn(col, "MAIN MENU", () => GameDirector.Instance.MainMenu(), new Vector2(560f, 70f), 30, TextAnchor.MiddleCenter);
            deathRoot.SetActive(false);
        }

        void BuildEnding()
        {
            var bg = Img(root, "Ending", new Color(0f, 0f, 0f, 0.92f));
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);
            endingRoot = bg.gameObject;
            var r = bg.rectTransform;

            endTitle = Story(Txt(r, "Title", "", 104, accent, TextAnchor.MiddleCenter, FontStyle.Bold));
            Place(endTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1800f, 140f));
            endSubtitle = Story(Txt(r, "Subtitle", "", 34, textColor, TextAnchor.MiddleCenter));
            Place(endSubtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -235f), new Vector2(1600f, 100f));
            endBody = Txt(r, "Body", "", 26, textColor, TextAnchor.UpperLeft);
            Place(endBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(1320f, 420f));
            endBody.lineSpacing = 1.1f;
            endStats = Txt(r, "Stats", "", 24, muted, TextAnchor.MiddleCenter);
            Place(endStats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -760f), new Vector2(1700f, 40f));

            var row = Rect("Buttons", r);
            Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(900f, 80f));
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 30f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            var again = Btn(row, "PLAY AGAIN", () => GameDirector.Instance.RestartNight(), new Vector2(430f, 76f), 32, TextAnchor.MiddleCenter);
            endPlayLabel = again.GetComponentInChildren<Text>();
            Btn(row, "MAIN MENU", () => GameDirector.Instance.MainMenu(), new Vector2(430f, 76f), 32, TextAnchor.MiddleCenter);
            endingRoot.SetActive(false);
        }

        RectTransform Column(RectTransform parent, Vector2 position, float width)
        {
            var col = Rect("Column", parent);
            col.anchorMin = col.anchorMax = new Vector2(0.5f, 0.5f);
            col.pivot = new Vector2(0.5f, 1f);
            col.anchoredPosition = position;
            col.sizeDelta = new Vector2(width, 0f);
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 14f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var fit = col.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return col;
        }

        void Spacer(RectTransform parent, float height)
        {
            var s = Rect("Spacer", parent);
            var le = s.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
        }

        void SetSensitivity(float value)
        {
            var gm = GameDirector.Instance;
            if (gm != null && gm.player != null) gm.player.SetSensitivity(value);
            else PlayerPrefs.SetFloat(PlayerController.SensitivityKey, value);
            foreach (var s in sensitivitySliders) s.SetValueWithoutNotify(value);
        }

        void SetVolume(float value)
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(AudioManager.VolumeKey, value);
            foreach (var s in volumeSliders) s.SetValueWithoutNotify(value);
        }

        void SyncSliders()
        {
            foreach (var s in sensitivitySliders) s.SetValueWithoutNotify(Sensitivity);
            foreach (var s in volumeSliders) s.SetValueWithoutNotify(AudioListener.volume);
        }

        public void ShowTitle(bool show)
        {
            titleRoot.SetActive(show);
            if (show) SyncSliders();
        }

        public void ShowPause(bool show)
        {
            pauseRoot.SetActive(show);
            if (show) SyncSliders();
        }

        /// <summary>
        /// They got in. Shows what happened and how many chances are left; the last one ends the night, and the only
        /// way on is to start it over.
        /// </summary>
        public void ShowDeath(string title, string hintText, int deaths, int limit)
        {
            deathFinal = deaths >= limit;
            int left = limit - deaths;
            deathTitle.text = deathFinal ? "THE NIGHT IS LOST." : title;
            deathHint.text = deathFinal ? "They got in " + deaths + " times. That was the last.\n" + hintText : hintText;
            deathStats.text = deathFinal ? "THEY GOT IN  " + deaths + " / " + limit
                : "THEY GOT IN  " + deaths + " / " + limit + "        " + (left == 1 ? "Once more and the night is lost." : left + " more times and the night is lost.");
            deathStats.color = deathFinal || left <= 1 ? danger : accent;
            deathRetryLabel.text = deathFinal ? "START THE NIGHT OVER" : "TRY AGAIN";
            deathRoot.SetActive(true);
        }

        public void HideDeath() => deathRoot.SetActive(false);

        /// <summary>The end of the night: whether you made it, how, and what happened to everyone.</summary>
        public void ShowEnding(string title, string subtitle, string body, string stats, bool lost)
        {
            endTitle.text = title;
            endTitle.color = lost ? danger : accent;
            endSubtitle.text = subtitle;
            endBody.text = body;
            endStats.text = stats;
            endPlayLabel.text = lost ? "START THE NIGHT OVER" : "PLAY AGAIN";
            endingRoot.SetActive(true);
        }

        public void HideAllMenus()
        {
            titleRoot.SetActive(false);
            pauseRoot.SetActive(false);
            deathRoot.SetActive(false);
            endingRoot.SetActive(false);
        }
    }
}
