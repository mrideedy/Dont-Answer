using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DontAnswer
{
    public enum GameState { Title, Intro, Playing, Dead, Ending }

    /// <summary>
    /// Runs the night of Halloween at 13 Elm Street. Split across partial files:
    /// this one (lifecycle, clock, input, dialogue helpers), .Visits (the door), .Events (the house),
    /// .Cinematics (prologue, deaths, endings, checkpoints).
    /// </summary>
    public partial class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        [Header("Content")]
        public TextAsset linesCsv;

        [Header("Systems")]
        public UIManager ui;
        public AudioManager sfx;
        public PhoneSystem phone;

        [Header("Player")]
        public PlayerController player;
        public Flashlight flashlight;
        public Transform playerStart;
        public Transform doorStand;
        public Transform prologueView;

        [Header("Front door & porch")]
        public FrontDoor door;
        public PorchLight porchLight;
        public Transform peepholeAnchor;
        public VisitorAvatar visitor;
        public Transform visitorSpot;
        public Transform visitorApproach;
        public Transform visitorExit;
        public MailSlot mailSlot;

        [Header("Other ways in")]
        public BackDoor backDoor;
        public WindowEntry[] windows;
        public VisitorAvatar prowler;

        [Header("Inside")]
        public Transform[] insideSeats;
        public InteriorDoor bedroomDoor;
        public InteriorDoor basementDoor;
        public MemoryItem[] memories;
        public Landline landline;
        public TVSet tv;
        public Transform tvFeedFigure;
        public PowerGrid power;
        public FlickerLight[] lamps;
        public WindowFigure windowFigure;
        public Transform upstairsPoint;
        public Transform streetPoint;

        [Header("Atmosphere")]
        public Volume peepholeVolume;
        public Light moonLight;
        public Light sunLight;
        public Light lightningLight;
        public Camera mainCamera;

        [Header("Pacing")]
        public float idleMinutesPerSecond = 1.4f;
        public float visitMinutesPerSecond = 0.1f;
        public float minIdleSeconds = 14f;
        public float firstVisitorDelay = 22f;

        [Header("Debug")]
        public bool skipIntro;
        public bool skipPrologue;
        [Tooltip("Jump straight to this minute after midnight (e.g. 180). -999 = normal start.")]
        public int debugStartMinute = -999;
        [Tooltip("0 = random night.")]
        public int debugSeed;

        // The debug switches only work in the editor, so a stray tick can never ship in a build.
#if UNITY_EDITOR
        bool SkipIntroNow => skipIntro;
        bool SkipPrologueNow => skipPrologue;
        int StartMinuteNow => debugStartMinute;
#else
        bool SkipIntroNow => false;
        bool SkipPrologueNow => false;
        int StartMinuteNow => -999;
#endif

        public GameState State { get; private set; } = GameState.Title;
        public float Minute { get; private set; }
        public NightPlan Plan { get; private set; }
        public VisitorDef CurrentVisitor { get; private set; }
        public bool IsPeeping { get; private set; }
        public bool IsTalking { get; private set; }
        public bool NoteOpen { get; private set; }
        public bool PhoneOpen { get; private set; }
        public bool MemoryOpen { get; private set; }
        public bool Paused { get; private set; }
        public bool InCutscene { get; private set; }
        public bool PowerOut { get; private set; }
        public int Deaths { get; private set; }
        public bool GuardingBackDoor => guests.ContainsKey("officer_h");
        public bool InBasement => player != null && player.transform.position.y < -0.6f;
        public bool InKitchen => player != null && player.transform.position.x < -3.1f && player.transform.position.y > -0.6f;

        public bool CanFreelyInteract =>
            State == GameState.Playing && !Paused && !InCutscene && !IsPeeping && !IsTalking &&
            !NoteOpen && !PhoneOpen && !MemoryOpen && !ui.ConfirmOpen;

        Dictionary<string, VisitorDef> defs;
        readonly List<(int minute, string id)> schedule = new List<(int, string)>();
        List<NightEvent> events = new List<NightEvent>();
        readonly HashSet<int> firedEvents = new HashSet<int>();
        readonly HashSet<string> saved = new HashSet<string>();
        readonly HashSet<string> lost = new HashSet<string>();
        readonly Dictionary<string, VisitorAvatar> guests = new Dictionary<string, VisitorAvatar>();
        readonly HashSet<int> usedReturnSlots = new HashSet<int>();
        readonly HashSet<string> memoriesFound = new HashSet<string>();
        int scheduleIndex, lastHour, viewFrame, rulesStage;
        float eventCooldown;
        bool noteRead, clockFrozen, retrying, lockedLastFrame, firstVisitDone;
        static bool autoStart;

        Color ambientBase, fogBase, backgroundBase;
        float moonBase;

        public static readonly Color OutsideColor = new Color(1f, 0.78f, 0.45f);
        public static readonly Color YouColor = new Color(0.6f, 0.8f, 1f);
        public static readonly Color InsideColor = new Color(0.6f, 0.95f, 0.65f);
        public static readonly Color MemoryColor = new Color(0.75f, 0.7f, 1f);
        public static readonly Color StaticColor = new Color(0.62f, 0.62f, 0.68f);
        public static readonly Color AngryColor = new Color(1f, 0.3f, 0.25f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            autoStart = false;
            Instance = null;
        }

        // ================================================================== lifecycle

        void Awake()
        {
            Instance = this;
            LineBank.Load(linesCsv);
            defs = StoryScript.BuildVisitors();
            CreateRoamer();
            ambientBase = RenderSettings.ambientLight;
            fogBase = RenderSettings.fogColor;
            if (mainCamera != null) backgroundBase = mainCamera.backgroundColor;
            if (moonLight != null) moonBase = moonLight.intensity;
        }

        void Start()
        {
            if (sunLight != null) sunLight.enabled = false;
            if (SkipIntroNow || SkipPrologueNow || StartMinuteNow > -999)
                Debug.LogWarning($"[Don't Answer] Debug start is ON (skip intro: {SkipIntroNow}, skip prologue: {SkipPrologueNow}, start minute: {StartMinuteNow}). " +
                                 "Clear it on the Game object's GameDirector to play the full story. Builds always ignore it.");
            if (autoStart)
            {
                autoStart = false;
                ResetWorld();
                ui.SetFade(1f);
                StartCoroutine(IntroRoutine(true));
            }
            else GoToTitle();
        }

        void GoToTitle()
        {
            ResetWorld();
            State = GameState.Title;
            ui.SetHudVisible(false);
            ui.ShowTitle(true);
            ui.SetFade(0f);
            sfx.TitleAmbience();
        }

        public void StartGame()
        {
            if (State != GameState.Title) return;
            sfx.UIClick();
            ui.ShowTitle(false);
            ResetWorld();
            StartCoroutine(IntroRoutine(SkipIntroNow));
        }

        void ResetWorld()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Paused = false;
            StopNight();
            IsPeeping = IsTalking = NoteOpen = PhoneOpen = MemoryOpen = InCutscene = false;
            player.ResetState();
            player.ExitView();
            player.Teleport(playerStart.position, playerStart.eulerAngles.y);

            Plan = NightPlan.Create(debugSeed != 0 ? debugSeed : Random.Range(1, int.MaxValue));
            phone.Begin(StoryScript.Messages(), Plan);
            // The same people come every night, but never quite on the same beat.
            var jitter = new System.Random(Plan.seed ^ 0x51ED);
            events = StoryScript.Events();
            foreach (var e in events)
                if (e.kind != EventKind.RulesAmended && e.kind != EventKind.Siege) e.minute += jitter.Next(-6, 7);
            firedEvents.Clear();
            schedule.Clear();
            foreach (var s in StoryScript.Schedule)
            {
                bool fixedBeat = s.id == "marco" || s.id == "voss_f" || s.id == "leo_final";
                schedule.Add(fixedBeat ? s : (s.minute + jitter.Next(-5, 6), s.id));
            }
            schedule.Add((StoryScript.OutageMinute, "OUTAGE"));
            schedule.Sort((a, b) => a.minute.CompareTo(b.minute));
            scheduleIndex = 0;
            usedReturnSlots.Clear();
            saved.Clear();
            lost.Clear();
            memoriesFound.Clear();
            if (memories != null) foreach (var m in memories) if (m != null) m.Found = false;
            ClearGuests();
            finalOutcome = FinalOutcome.None;

            visitor.Hide();
            prowler.Hide();
            door.SetAngle(0f);
            door.ResetLock();
            porchLight.SetMode(PorchLightMode.On);
            backDoor.ResetState(false);
            foreach (var w in windows) w.ResetState(true);
            mailSlot.ResetState();
            landline.StopRinging();
            if (tv != null) tv.SetMode(TVSet.TVMode.Off);
            PowerOut = false;
            power.SetPower(true);
            if (basementLeo != null) basementLeo.Hide();
            flashlight.HasFlashlight = false;
            flashlight.Set(false);
            if (bedroomDoor != null) bedroomDoor.SetOpen(false, false);
            if (basementDoor != null) basementDoor.SetOpen(false, false);
            if (tvFeedFigure != null) tvFeedFigure.gameObject.SetActive(false);
            ResetHaunting();
            ForgetSiege();

            CurrentVisitor = null;
            result = VisitResult.None;
            Minute = StoryScript.NightStart;
            lastHour = HourOf(Minute);
            noteRead = false;
            clockFrozen = true;
            rulesStage = 0;
            firstVisitDone = false;
            Deaths = 0;
            checkpoint = null;
            retrying = false;
            eventCooldown = 0f;

            ui.HideAllMenus();
            ui.ShowPhone(false);
            ui.HideNote();
            ui.HideMemory();
            ui.HideChoices();
            ui.CancelConfirm();
            ui.SetPeephole(false);
            ui.SetBlinds(false);
            ui.SetHint(null);
            ui.HideSubtitleNow();
            ui.SetLetterbox(false, true);
            SetPeepholeVolume(0f);
            RestoreLighting();
        }

        void StopNight()
        {
            StopAllCoroutines();
            knockRoutine = talkRoutine = null;
            probeRoutine = null;
            knocking = talkBusy = angry = staring = false;
            if (sfx != null)
            {
                sfx.SetStatic(false);
                sfx.SetTension(0f);
                sfx.SetBreath(false);
            }
        }

        // ================================================================== per-frame

        void Update()
        {
            if (State == GameState.Playing)
            {
                HandleInput();
                if (!Paused)
                {
                    phone.Tick(Minute);
                    TickEvents();
                    CheckHour();
                    CheckTier();
                    WatchThePeephole();
                    UpdateTension();
                    sfx.SetBasement(InBasement);
                }
            }
            ui.SetClock(FormatTime(Minute));
            ui.SetUnread(State == GameState.Playing ? phone.Unread : 0);
            ui.SetTally(lost.Count, StoryScript.MaxLeftOutside, Deaths, StoryScript.MaxGotIn);
        }

        void LateUpdate()
        {
            bool playing = State == GameState.Playing && !Paused;
            bool overlay = IsTalking || NoteOpen || PhoneOpen || MemoryOpen || ui.ConfirmOpen;
            player.MoveEnabled = playing && !InCutscene && !IsPeeping && !IsTalking && !NoteOpen && !MemoryOpen && !ui.ConfirmOpen;
            player.LookEnabled = playing && !InCutscene && !overlay;

            bool wantLock = playing && !overlay;
            var mode = wantLock ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != mode) Cursor.lockState = mode;
            Cursor.visible = !wantLock;

            bool locked = Cursor.lockState == CursorLockMode.Locked;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (wantLock && lockedLastFrame && !locked && !Paused) TogglePause();
#endif
            lockedLastFrame = locked;
        }

        void HandleInput()
        {
            if (GameInput.Pause)
            {
                if (Paused) { ResumeGame(); return; }
                if (IsPeeping) { ExitPeek(); return; }
                if (NoteOpen) { CloseNote(); return; }
                if (MemoryOpen) { CloseMemory(); return; }
                if (PhoneOpen) { TogglePhone(); return; }
                if (ui.ConfirmOpen) { ui.CancelConfirm(); return; }
                if (IsTalking && !talkBusy) { EndTalk(); return; }
                if (!InCutscene) TogglePause();
                return;
            }
            if (Paused || InCutscene) return;

            if (IsPeeping && Time.frameCount > viewFrame + 1 && GameInput.Back) { ExitPeek(); return; }
            if (NoteOpen && Time.frameCount > viewFrame + 1 && (GameInput.Back || GameInput.Interact)) { CloseNote(); return; }
            if (MemoryOpen && Time.frameCount > viewFrame + 1 && (GameInput.Back || GameInput.Interact)) { CloseMemory(); return; }
            if (GameInput.Flashlight && !IsTalking) flashlight.Toggle();
            if (GameInput.Phone && !IsTalking && !NoteOpen && !MemoryOpen && !IsPeeping && !ui.ConfirmOpen) TogglePhone();
            else if (GameInput.Rules && noteRead && !IsTalking && !NoteOpen && !MemoryOpen && !IsPeeping && !ui.ConfirmOpen) OpenNote();
        }

        void CheckHour()
        {
            int h = HourOf(Minute);
            if (h == lastHour) return;
            lastHour = h;
            if (h < 0) return;
            sfx.PlaySfx2D("church_bell", h == 0 ? 0.7f : 0.3f);
            sfx.SetDread(Mathf.Clamp01(h / 5f) * 0.9f + 0.1f);
            if (h == 0 && CurrentVisitor == null) ui.SetHint("Midnight.\nSurvive until 6:00 AM.");
        }

        void UpdateTension()
        {
            float t = 0f;
            if (CurrentVisitor != null) t = IsPeeping ? 0.35f : 0.15f;
            if (probing) t = Mathf.Max(t, 0.5f);
            if (angry) t = 1f;
            if (PowerOut) t = Mathf.Max(t, InBasement ? 0.5f : 0.3f);
            if (InCutscene && CurrentVisitor != null) t = 0.7f;
            sfx.SetTension(t);
        }

        // ================================================================== clock

        IEnumerator IdleUntil(float target, float minSeconds)
        {
            float waited = 0f;
            while (Minute < target || waited < minSeconds * IdleStretch)
            {
                if (!clockFrozen)
                {
                    Minute = Mathf.Min(target, Minute + Time.deltaTime * idleMinutesPerSecond / IdleStretch);
                    waited += Time.deltaTime;
                }
                yield return null;
            }
        }

        /// <summary>The quiet between visits gets longer as the night goes on: more time for them to try the windows.</summary>
        float IdleStretch => Mathf.Lerp(1f, 1.6f, Mathf.InverseLerp(60f, 300f, Minute));

        float NextItemMinute()
        {
            int next = scheduleIndex + 1;
            return next < schedule.Count ? schedule[next].minute : StoryScript.Sunrise;
        }

        static int HourOf(float minute) => Mathf.FloorToInt(minute / 60f);

        public static string FormatTime(float minute)
        {
            int m = Mathf.FloorToInt(minute);
            int total = ((m % 1440) + 1440) % 1440;
            int h24 = total / 60, mm = total % 60;
            int h12 = h24 % 12 == 0 ? 12 : h24 % 12;
            return h12 + ":" + mm.ToString("00") + (h24 < 12 ? " AM" : " PM");
        }

        // ================================================================== dialogue

        /// <summary>Speak a line with subtitles. Uses the recording if it exists, otherwise mumbles.</summary>
        public IEnumerator Say(string lineId, string speaker, Color color, Vector3? pos, bool muffled, bool mimic, float pitch, bool skippable)
        {
            var line = LineBank.Get(lineId);
            string text = line.text;
            if (string.IsNullOrEmpty(text)) yield break;
            int token = ui.ShowSubtitle(speaker, color, "");
            float clipLen = sfx.PlayLine(lineId, pos, muffled, mimic, pitch, out var source);
            bool recorded = clipLen > 0f;
            float cps = recorded ? Mathf.Max(10f, text.Length / Mathf.Max(0.4f, clipLen * 0.9f)) : 38f;
            int startFrame = Time.frameCount;
            float shown = 0f, elapsed = 0f;
            int len = 0, letters = 0;
            bool skipped = false;
            while (len < text.Length)
            {
                elapsed += Time.deltaTime;
                shown += Time.deltaTime * cps;
                if (skippable && Time.frameCount > startFrame + 2 && GameInput.Skip)
                {
                    shown = text.Length;
                    skipped = true;
                    if (source != null) source.Stop();
                }
                int newLen = Mathf.Min(text.Length, Mathf.FloorToInt(shown));
                if (!recorded && !skipped)
                {
                    for (int i = len; i < newLen; i++)
                        if (char.IsLetter(text[i]) && ++letters % 3 == 0)
                            sfx.Voice(pos, pitch, mimic, muffled ? 0.35f : 0.8f);
                }
                if (newLen != len)
                {
                    len = newLen;
                    ui.SetSubtitleText(token, text.Substring(0, len));
                }
                yield return null;
            }
            float hold = recorded && !skipped ? Mathf.Max(0.5f, clipLen - elapsed) + 0.5f : 0.9f + text.Length * 0.028f;
            int doneFrame = Time.frameCount;
            for (float held = 0f; held < hold; held += Time.deltaTime)
            {
                if (skippable && Time.frameCount > doneFrame + 2 && GameInput.Skip)
                {
                    if (source != null) source.Stop();
                    break;
                }
                yield return null;
            }
            ui.HideSubtitle(token);
        }

        /// <summary>Speak a line as whoever the line table says (used by world objects like the mail slot).</summary>
        public IEnumerator SayLine(string lineId, Vector3? pos, bool muffled)
        {
            var line = LineBank.Get(lineId);
            yield return Say(lineId, line.speaker.Replace(" (?)", "").ToUpperInvariant(), OutsideColor, pos, muffled, line.IsMimic, 1f, false);
        }

        IEnumerator SayYou(string text)
        {
            int token = ui.ShowSubtitle("YOU", YouColor, text);
            yield return new WaitForSeconds(0.8f + text.Length * 0.02f);
            ui.HideSubtitle(token);
        }

        IEnumerator Thought(string text, float seconds = 2.4f)
        {
            int token = ui.ShowSubtitle("", Color.white, "<i>" + text + "</i>");
            yield return new WaitForSeconds(seconds);
            ui.HideSubtitle(token);
        }

        // ================================================================== small player actions

        public void ReadNote()
        {
            if (!CanFreelyInteract) return;
            OpenNote();
        }

        void OpenNote()
        {
            NoteOpen = true;
            viewFrame = Time.frameCount;
            if (PhoneOpen) TogglePhone();
            string text = StoryScript.RulesText;
            if (rulesStage >= 1) text += StoryScript.RulesAmendment;
            if (rulesStage >= 2) text += StoryScript.RulesAmendment2;
            ui.ShowNote(text);
            sfx.PlaySfx2D("paper", 0.7f);
        }

        void CloseNote()
        {
            NoteOpen = false;
            ui.HideNote();
            sfx.PlaySfx2D("paper", 0.45f, 1.1f);
            if (!noteRead)
            {
                noteRead = true;
                clockFrozen = false;
                ui.SetHint(backDoor.Locked ? "Wait for midnight." : "Lock the back door in the kitchen.\nThe bolt sticks - lean on it.");
            }
        }

        void TogglePhone()
        {
            PhoneOpen = !PhoneOpen;
            ui.ShowPhone(PhoneOpen);
            if (!PhoneOpen) phone.MarkAllRead();
            sfx.UIClick();
        }

        public void LockBackDoor(BackDoor bd)
        {
            bd.SetLocked(true);
            sfx.PlaySfx("deadbolt", bd.SoundPosition, 1f);
            player.Shake(0.01f, 0.2f);
            if (noteRead && Minute < 0f) ui.SetHint("Wait for midnight.");
        }

        public void SecureWindow(WindowEntry w)
        {
            if (!CanFreelyInteract) return;
            w.Secure();
        }

        public void PeekThrough(Transform anchor, string kind)
        {
            if (!CanFreelyInteract || anchor == null) return;
            IsPeeping = true;
            peepholeActive = false;
            viewFrame = Time.frameCount;
            player.EnterView(anchor, 62f, new Vector2(22f, 10f));
            ui.SetBlinds(true);
            ui.SetPrompt(null);
            sfx.PlaySfx("sheet_rustle", anchor.position, 0.25f, 1.6f);
        }

        void ExitPeek()
        {
            if (!IsPeeping) return;
            IsPeeping = false;
            peepholeActive = false;
            player.ExitView();
            ui.SetPeephole(false);
            ui.SetBlinds(false);
            SetPeepholeVolume(0f);
        }

        void SetPeepholeVolume(float weight)
        {
            if (peepholeVolume != null) peepholeVolume.weight = weight;
        }

        public void ShowMemory(MemoryItem item)
        {
            if (!CanFreelyInteract) return;
            MemoryOpen = true;
            viewFrame = Time.frameCount;
            bool first = !item.Found;
            item.Found = true;
            memoriesFound.Add(item.memoryId);
            ui.ShowMemory(item.title, item.description, item.playsSecretKnock);
            StartCoroutine(MemoryAudio(item, first));
        }

        IEnumerator MemoryAudio(MemoryItem item, bool first)
        {
            if (first) sfx.PlaySfx2D("music_box", 0.18f, 0.9f);
            if (item.playsSecretKnock)
            {
                yield return new WaitForSeconds(0.4f);
                sfx.PlaySfx2D("knock_secret", 0.8f);
                yield return new WaitForSeconds(2f);
            }
            if (!string.IsNullOrEmpty(item.memoryId) && LineBank.Has(item.memoryId))
                yield return Say(item.memoryId, "LEO  (memory)", MemoryColor, null, false, false, 1.25f, true);
            if (first) ui.ShowToast("Memory", item.title + " - saved to your phone", false);
        }

        void CloseMemory()
        {
            MemoryOpen = false;
            ui.HideMemory();
        }

        public IEnumerable<MemoryItem> FoundMemories()
        {
            if (memories == null) yield break;
            foreach (var m in memories) if (m != null && m.Found) yield return m;
        }

        public void ReplayMemory(MemoryItem item)
        {
            if (item == null) return;
            StartCoroutine(Say(item.memoryId, "LEO  (memory)", MemoryColor, null, false, false, 1.25f, true));
        }

        /// <summary>The voicemail Leo left a year ago, still saved on Sam's phone.</summary>
        public void PlayVoicemail()
        {
            memoriesFound.Add("MEM_VOICEMAIL");
            StartCoroutine(VoicemailRoutine());
        }

        IEnumerator VoicemailRoutine()
        {
            sfx.PlaySfx2D("walkie_static", 0.4f);
            yield return new WaitForSeconds(0.5f);
            yield return Say("MEM_VOICEMAIL", "LEO  (voicemail, a year ago)", MemoryColor, null, false, false, 1.25f, true);
        }
    }
}
