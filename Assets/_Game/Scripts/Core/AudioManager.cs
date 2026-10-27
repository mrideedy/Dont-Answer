using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>
    /// All sound playback, by name. A sound "x" is loaded from Resources/SFX/x plus its variants x_1, x_2 ...
    /// (one is picked at random, never the same twice in a row) or, if none exist, Resources/SFXFallback/x
    /// (synthesized placeholders). Uses only volume/pitch/3D tricks (no filters) so it behaves the same in WebGL.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public float Tension => tension;
        public float Dread => dread;

        [Header("Emitters")]
        public Transform doorPoint;
        public Transform clockPoint;
        public Transform basementPoint;

        [Header("Mix")]
        [Range(0f, 1f)] public float rainVolume = 0.35f;
        [Range(0f, 1f)] public float houseVolume = 0.5f;
        [Range(0f, 1f)] public float droneVolume = 0.0f;
        [Range(0f, 1f)] public float tickVolume = 0.45f;
        [Range(0f, 1f)] public float musicVolume = 0.5f;

        public const string VolumeKey = "da_volume";

        readonly Dictionary<string, AudioClip[]> cache = new Dictionary<string, AudioClip[]>();
        readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>();
        readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();
        readonly Dictionary<AudioSource, Coroutine> fades = new Dictionary<AudioSource, Coroutine>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource ui;
        int poolCursor;
        float tension, tensionTarget, rainMultiplier = 1f, dread;
        bool outdoorLife;
        float outdoorTimer = 20f;

        // Somewhere out there: (sound, weight, near the house?)
        static readonly (string id, float weight, bool near)[] OutdoorSounds =
        {
            ("dog_distant", 3f, false), ("howl_distant", 1f, false), ("cough_outside", 0.7f, true),
            ("creature_click", 0.5f, true), ("creature_growl", 0.35f, false),
        };
        static readonly Vector3[] NearHouse = { new Vector3(4.6f, 1.2f, -1f), new Vector3(-4.9f, 1.2f, 3.4f), new Vector3(-8.2f, 1.2f, -2.5f), new Vector3(-1f, 1.2f, -9.6f) };

        void Awake()
        {
            Instance = this;
            AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            ui = MakeSource("UI");
            ui.ignoreListenerPause = true;
            for (int i = 0; i < 28; i++) pool.Add(MakeSource("OneShot" + i));

            Loop("rain_window", null);
            Loop("house_tone", null);
            Loop("drone_dread", null);
            Loop("clock_tick", clockPoint, 0.8f, 7f);
            Loop("heartbeat", null);
            Loop("static_loop", null);
            Loop("breath_door", doorPoint, 0.6f, 5f);
            Loop("music_box", null);
            Loop("basement_tone", basementPoint, 2f, 14f);
            Loop("faucet_drip", null);
        }

        // ------------------------------------------------------------------ clips

        public AudioClip Sfx(string id)
        {
            var set = Variants(id);
            if (set.Length == 0) return null;
            if (set.Length == 1) return set[0];
            lastVariant.TryGetValue(id, out int last);
            int pick = Random.Range(0, set.Length - 1);
            if (pick >= last) pick++;
            lastVariant[id] = pick;
            return set[pick];
        }

        AudioClip[] Variants(string id)
        {
            if (string.IsNullOrEmpty(id)) return System.Array.Empty<AudioClip>();
            if (cache.TryGetValue(id, out var set)) return set;
            var list = new List<AudioClip>();
            var c = Resources.Load<AudioClip>("SFX/" + id);
            if (c != null)
            {
                list.Add(c);
                for (int i = 1; i < 12; i++)
                {
                    var v = Resources.Load<AudioClip>("SFX/" + id + "_" + i);
                    if (v == null) break;
                    list.Add(v);
                }
            }
            else
            {
                c = Resources.Load<AudioClip>("SFXFallback/" + id);
                if (c != null) list.Add(c);
            }
            set = list.ToArray();
            cache[id] = set;
            return set;
        }

        AudioSource MakeSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            return s;
        }

        AudioSource Loop(string id, Transform emitter, float minDist = 1f, float maxDist = 10f)
        {
            var s = MakeSource("Loop " + id);
            if (emitter != null)
            {
                s.transform.SetParent(emitter, false);
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = minDist;
                s.maxDistance = maxDist;
            }
            s.clip = Sfx(id);
            s.loop = true;
            s.volume = 0f;
            if (s.clip != null) s.Play();
            loops[id] = s;
            return s;
        }

        public void SetLoop(string id, float volume, float fadeTime)
        {
            if (loops.TryGetValue(id, out var s)) Fade(s, volume, fadeTime);
        }

        public void Fade(AudioSource s, float volume, float time)
        {
            if (s == null) return;
            if (fades.TryGetValue(s, out var c) && c != null) StopCoroutine(c);
            fades[s] = StartCoroutine(FadeRoutine(s, volume, time));
        }

        IEnumerator FadeRoutine(AudioSource s, float target, float time)
        {
            float start = s.volume;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, time))
            {
                s.volume = Mathf.Lerp(start, target, t);
                yield return null;
            }
            s.volume = target;
        }

        void Update()
        {
            tension = Mathf.MoveTowards(tension, tensionTarget, Time.unscaledDeltaTime * 0.6f);
            if (loops.TryGetValue("heartbeat", out var heart))
            {
                heart.volume = tension * 0.75f;
                heart.pitch = 0.85f + tension * 0.55f;
            }
            if (outdoorLife) OutdoorLife(Time.deltaTime);
        }

        /// <summary>The night outside is never quite empty: a dog, something howling, someone coughing under the window.</summary>
        void OutdoorLife(float dt)
        {
            outdoorTimer -= dt;
            if (outdoorTimer > 0f) return;
            outdoorTimer = Random.Range(24f, 55f);
            float total = 0f;
            foreach (var s in OutdoorSounds) total += s.weight;
            float r = Random.value * total;
            foreach (var s in OutdoorSounds)
            {
                r -= s.weight;
                if (r > 0f) continue;
                Vector3 p;
                if (s.near) p = NearHouse[Random.Range(0, NearHouse.Length)] + Random.insideUnitSphere * 0.6f;
                else
                {
                    float a = Random.value * Mathf.PI * 2f;
                    p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(14f, 24f) + new Vector3(0f, 1.5f, -2f);
                }
                PlayAt(Sfx(s.id), p, s.near ? 0.55f : 0.8f, Random.Range(0.85f, 1.05f), s.near ? 1.5f : 4f, s.near ? 14f : 40f);
                break;
            }
        }

        // ------------------------------------------------------------------ moods

        public void TitleAmbience()
        {
            outdoorLife = false;
            SetLoop("music_box", musicVolume, 1.5f);
            SetLoop("rain_window", rainVolume * 0.7f, 1.5f);
            foreach (var id in new[] { "house_tone", "drone_dread", "clock_tick", "static_loop", "breath_door", "basement_tone", "faucet_drip" })
                SetLoop(id, 0f, 0.5f);
            tensionTarget = 0f;
        }

        public void GameAmbience()
        {
            outdoorLife = true;
            rainMultiplier = 1f;
            SetLoop("music_box", 0f, 2f);
            SetLoop("rain_window", rainVolume, 2f);
            SetLoop("house_tone", houseVolume, 3f);
            SetLoop("clock_tick", tickVolume, 2f);
            SetLoop("faucet_drip", 0.12f, 3f);
            SetDread(dread);
        }

        /// <summary>A low drone that rises as the night gets worse (0..1).</summary>
        public void SetDread(float value)
        {
            dread = Mathf.Clamp01(value);
            SetLoop("drone_dread", dread * 0.35f, 4f);
        }

        public void Silence(float time)
        {
            foreach (var s in loops.Values) Fade(s, 0f, time);
            tensionTarget = 0f;
        }

        /// <summary>
        /// Everything stops at once: every loop, every sound still ringing out, even the heartbeat and the dogs down the
        /// street. The silence before a scare. <see cref="GameAmbience"/> brings the night back.
        /// </summary>
        public void HardSilence(float fade = 0.06f)
        {
            foreach (var s in loops.Values) Fade(s, 0f, fade);
            foreach (var s in pool) if (s.isPlaying) s.Stop();
            tension = tensionTarget = 0f;
            outdoorLife = false;
        }

        public void Sunrise()
        {
            outdoorLife = false;
            SetLoop("rain_window", 0f, 6f);
            SetLoop("drone_dread", 0f, 6f);
            SetLoop("breath_door", 0f, 1f);
            SetLoop("static_loop", 0f, 1f);
            tensionTarget = 0f;
            PlaySfx2D("dawn_birds", 0.6f);
        }

        public void SetRainOpen(bool open)
        {
            rainMultiplier = open ? 2.2f : 1f;
            SetLoop("rain_window", rainVolume * rainMultiplier, 0.6f);
        }

        public void SetBasement(bool inBasement)
        {
            SetLoop("basement_tone", inBasement ? 0.8f : 0f, 1.5f);
            SetLoop("rain_window", inBasement ? rainVolume * 0.3f : rainVolume * rainMultiplier, 1.5f);
            SetLoop("clock_tick", inBasement ? 0f : tickVolume, 1.5f);
        }

        public void SetPower(bool on)
        {
            SetLoop("house_tone", on ? houseVolume : houseVolume * 0.4f, 0.3f);
        }

        public void SetStatic(bool on) => SetLoop("static_loop", on ? 0.4f : 0f, on ? 0.1f : 0.3f);
        public void SetBreath(bool on) => SetLoop("breath_door", on ? 0.9f : 0f, on ? 2f : 1.5f);
        public void SetTension(float value) => tensionTarget = Mathf.Clamp01(value);

        // ------------------------------------------------------------------ one-shots

        AudioSource NextSource()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                var s = pool[(poolCursor + i) % pool.Count];
                if (!s.isPlaying)
                {
                    poolCursor = (poolCursor + i + 1) % pool.Count;
                    return s;
                }
            }
            poolCursor = (poolCursor + 1) % pool.Count;
            return pool[poolCursor];
        }

        public AudioSource Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return null;
            var s = NextSource();
            s.spatialBlend = 0f;
            s.transform.localPosition = Vector3.zero;
            s.clip = clip;
            s.volume = volume;
            s.pitch = pitch;
            s.loop = false;
            s.Play();
            return s;
        }

        public AudioSource PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float minDist = 1f, float maxDist = 16f)
        {
            if (clip == null) return null;
            var s = NextSource();
            s.transform.position = position;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = minDist;
            s.maxDistance = maxDist;
            s.clip = clip;
            s.volume = volume;
            s.pitch = pitch;
            s.loop = false;
            s.Play();
            return s;
        }

        public AudioSource PlaySfx(string id, Vector3 position, float volume = 1f, float pitch = 1f) => PlayAt(Sfx(id), position, volume, pitch);
        public AudioSource PlaySfx2D(string id, float volume = 1f, float pitch = 1f) => Play2D(Sfx(id), volume, pitch);

        public void UIClick()
        {
            var c = Sfx("ui_click");
            if (c == null || ui == null) return;
            ui.pitch = Random.Range(0.95f, 1.05f);
            ui.PlayOneShot(c, 0.5f);
        }

        public void PhoneBuzz() => PlaySfx2D("phone_buzz", 0.55f);

        /// <summary>Wood floorboards inside, wet boards and mud outside.</summary>
        public void Footstep(Vector3 position, float volume = 0.35f, float pitch = 1f)
        {
            var clip = Sfx(IsOutside(position) ? "footstep_wet" : "footstep");
            if (clip == null) clip = Sfx("footstep_" + Random.Range(0, 3));
            PlayAt(clip, position, volume, pitch * Random.Range(0.92f, 1.08f), 1f, 10f);
        }

        static bool IsOutside(Vector3 p) => p.y > -0.3f && (p.x > 3.1f || p.x < -6.65f || p.z > 3.1f || p.z < -8.1f);

        /// <summary>One syllable of mumbled placeholder voice. Mimics get a second voice an octave below.</summary>
        public void Voice(Vector3? position, float pitch, bool distorted, float volume)
        {
            var clip = Sfx("voice_" + Random.Range(0, 4));
            if (clip == null) return;
            float p = pitch * Random.Range(0.9f, 1.1f);
            if (position.HasValue) PlayAt(clip, position.Value, volume, p, 1.5f, 12f);
            else Play2D(clip, volume, p);
            if (distorted)
            {
                if (position.HasValue) PlayAt(clip, position.Value, volume * 0.8f, p * 0.5f, 1.5f, 12f);
                else Play2D(clip, volume * 0.8f, p * 0.5f);
            }
        }

        /// <summary>
        /// Plays a recorded line. Mimic lines get an inhuman under-voice; muffled lines (through a door)
        /// are quieter and slightly duller. Returns the clip length, or -1 if there's no recording yet.
        /// The recordings already carry each character's pitch, so <paramref name="pitch"/> only nudges it:
        /// anything under 1 drops the voice a little (death lines), anything over is ignored.
        /// </summary>
        public float PlayLine(string lineId, Vector3? position, bool muffled, bool mimic, float pitch = 1f)
        {
            return PlayLine(lineId, position, muffled, mimic, pitch, out _);
        }

        public float PlayLine(string lineId, Vector3? position, bool muffled, bool mimic, float pitch, out AudioSource source)
        {
            source = null;
            var clip = LineBank.Clip(lineId);
            if (clip == null) return -1f;
            float vol = muffled ? 0.55f : 1f;
            float p = Mathf.Clamp(pitch, 0.9f, 1f) * (muffled ? 0.97f : 1f);
            source = position.HasValue ? PlayAt(clip, position.Value, vol, p, 2f, 18f) : Play2D(clip, vol, p);
            if (mimic)
            {
                // A second copy, an octave down and a hair late, sits under the voice like something wearing it.
                StartCoroutine(Delayed(0.035f, () =>
                {
                    if (position.HasValue) PlayAt(clip, position.Value, vol * 0.35f, p * 0.5f, 2f, 18f);
                    else Play2D(clip, vol * 0.35f, p * 0.5f);
                }));
            }
            return clip.length / Mathf.Max(0.1f, p);
        }

        IEnumerator Delayed(float seconds, System.Action action)
        {
            yield return new WaitForSeconds(seconds);
            action();
        }
    }
}
