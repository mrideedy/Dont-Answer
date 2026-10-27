using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>
    /// What happens around the visits. The house: knocks on windows, the back door handle turning, doors that
    /// move on their own. The things you turned away: each one tries something different. And the street:
    /// the ones you refused walk it, and if one of them is out there when you open the door, it comes running.
    /// </summary>
    public partial class GameDirector
    {
        public InteriorDoor bathroomDoor;

        VisitorAvatar roamer;
        Light glassGlow;   // the room's lamplight on a face pressed to the glass (house lamps don't light the Outside layer)
        VisitorDef roamDef;
        readonly List<VisitorDef> roamers = new List<VisitorDef>();
        bool roamActive, roamRushing, roamWarned, windowsBurstUsed;
        int lastScare = -1, lastScare2 = -1;

        const string RoamWarning =
            "Before you open your door, LOOK through the peephole. If one of them is standing in your yard, DON'T open it. Not for anyone. Wait until it walks off. - V.";

        // Where something stands in the yard to be seen from a window (or the peephole), looking at the house.
        static readonly (Vector3 at, Vector3 lookAt)[] WatchSpots =
        {
            (new Vector3(-1.6f, -0.45f, 10.8f), new Vector3(0f, 1f, 3f)),      // front yard: the peephole
            (new Vector3(7.3f, -0.45f, -0.3f), new Vector3(3.1f, 1f, -1f)),    // east yard: the living room window (clear of the floor lamp)
            (new Vector3(-5.8f, -0.45f, 8.2f), new Vector3(-4.9f, 1f, 1.1f)),  // by the fence: the kitchen window
            (new Vector3(-1.2f, -0.45f, -11.3f), new Vector3(-1.65f, 1f, -8.1f)), // back yard: the bedroom window
        };

        // The way round the outside of the house, front porch to back door.
        static readonly Vector3[] CircleRoute =
        {
            new Vector3(1.8f, -0.45f, 6.0f), new Vector3(4.2f, -0.45f, 1.5f), new Vector3(4.2f, -0.45f, -3.5f),
            new Vector3(2.2f, -0.45f, -9.2f), new Vector3(-3.5f, -0.45f, -11.0f), new Vector3(-8.2f, -0.45f, -6.0f),
            new Vector3(-7.7f, -0.45f, -1.9f),
        };

        // ================================================================== setup

        void CreateRoamer()
        {
            if (roamer != null || visitor == null) return;
            var go = new GameObject("Roamer");
            go.layer = visitor.gameObject.layer;
            go.transform.SetParent(visitor.transform.parent, false);
            roamer = go.AddComponent<VisitorAvatar>();
            roamer.CopyMaterialsFrom(visitor);

            var glow = new GameObject("Glass Glow");
            glow.transform.SetParent(transform, false);
            glassGlow = glow.AddComponent<Light>();
            glassGlow.type = LightType.Point;
            glassGlow.shadows = LightShadows.None;
            glassGlow.range = 2.2f;
            glassGlow.enabled = false;
            CreateSiege();
        }

        void StartHaunting()
        {
            StartCoroutine(HouseScares());
            StartCoroutine(RoamLoop());
            StartCoroutine(AssaultLoop());
        }

        void ResetHaunting()
        {
            roamers.Clear();
            roamDef = null;
            roamActive = roamRushing = roamWarned = windowsBurstUsed = false;
            lastScare = lastScare2 = -1;
            if (roamer != null) roamer.Hide();
            if (glassGlow != null) glassGlow.enabled = false;
            ResetSiege();
        }

        static int Pick(float[] weights)
        {
            float total = 0f;
            foreach (var w in weights) total += Mathf.Max(0f, w);
            if (total <= 0f) return 0;
            float r = Random.value * total;
            for (int i = 0; i < weights.Length; i++)
            {
                r -= Mathf.Max(0f, weights[i]);
                if (r <= 0f) return i;
            }
            return weights.Length - 1;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        WindowEntry WindowAwayFromPlayer()
        {
            var options = new List<WindowEntry>();
            foreach (var w in windows)
                if (w != null && Vector3.Distance(Flat(w.SoundPosition), Flat(player.transform.position)) > 2.5f) options.Add(w);
            if (options.Count == 0) foreach (var w in windows) if (w != null) options.Add(w);
            return options.Count > 0 ? options[Random.Range(0, options.Count)] : null;
        }

        /// <summary>Is the player looking at this point, close enough and with nothing (but glass) in the way?</summary>
        bool PlayerSees(Vector3 point, float maxDistance, float angle = 28f)
        {
            var cam = player.cam.transform;
            var to = point - cam.position;
            float dist = to.magnitude;
            if (dist > maxDistance || Vector3.Angle(cam.forward, to) > angle) return false;
            // Windows (glass, sash, the blocker that keeps you inside) and the back door's glass don't hide it.
            foreach (var hit in Physics.RaycastAll(cam.position, to / dist, dist - 0.5f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<WindowEntry>() == null && hit.collider.GetComponentInParent<BackDoor>() == null)
                    return false;
            return true;
        }

        // ================================================================== the house between visits

        IEnumerator HouseScares()
        {
            float timer = Random.Range(40f, 65f);
            float interfere = Random.Range(20f, 32f);
            while (State == GameState.Playing)
            {
                bool quiet = !Paused && !InCutscene && !IsTalking && CurrentVisitor == null && !probing && !prowler.Visible && !(PowerOut && InBasement);
                if (quiet) timer -= Time.deltaTime;
                if (timer <= 0f && !Dawn)
                {
                    yield return HouseScare();
                    timer = Random.Range(36f, 60f) * Mathf.Lerp(0.9f, 0.35f, Menace);   // the night gets worse
                }
                // Late at night they don't wait for you to finish at the door: something at a window while you talk.
                bool visiting = CurrentVisitor != null && !Paused && !InCutscene && !probing && Tier >= 2 && !Dawn;
                if (visiting && (interfere -= Time.deltaTime) <= 0f)
                {
                    interfere = Random.Range(22f, 36f) * Mathf.Lerp(1.15f, 0.7f, TierK);
                    int k = Pick(new[] { 2f, 1.4f, 1f });
                    yield return k == 0 ? KnockOnAWindow() : k == 1 ? TryTheBackDoor() : NailsOnGlass();
                }
                yield return null;
            }
        }

        IEnumerator HouseScare()
        {
            // window knock, back door, knock + nobody, a door moves, a window opens, steps on the porch, nails on glass,
            // the TV, the lights going out toward you, something walking past the window
            float[] w =
            {
                3f, 2f, 1.4f, 1.5f, 0.9f, 1.2f, 1f, PowerOut ? 0f : 0.8f,
                PowerOut || Tier < 1 ? 0f : 1.2f,
                Tier < 1 ? 0.3f : 1.3f,
            };
            if (lastScare >= 0) w[lastScare] = 0f;
            if (lastScare2 >= 0) w[lastScare2] *= 0.4f;
            int pick = Pick(w);
            lastScare2 = lastScare;
            lastScare = pick;
            switch (pick)
            {
                case 0: yield return KnockOnAWindow(); break;
                case 1: yield return TryTheBackDoor(); break;
                case 2: yield return KnockAndNobody(); break;
                case 3: yield return DoorMovesByItself(); break;
                case 4: yield return AWindowOpens(); break;
                case 5: yield return StepsOnThePorch(); break;
                case 6: yield return NailsOnGlass(); break;
                case 7: yield return TvComesOn(); break;
                case 8: yield return LightsGoOutTowardYou(); break;
                case 9: yield return SomethingPassesTheWindow(); break;
            }
        }

        /// <summary>The lamps go out one by one, from the far end of the apartment toward wherever you're standing.</summary>
        IEnumerator LightsGoOutTowardYou()
        {
            if (PowerOut || lamps == null || lamps.Length == 0) yield break;
            var order = new List<FlickerLight>();
            foreach (var l in lamps) if (l != null) order.Add(l);
            var here = player.transform.position;
            order.Sort((a, b) => Vector3.Distance(b.transform.position, here).CompareTo(Vector3.Distance(a.transform.position, here)));
            foreach (var l in order)
            {
                if (!Running || PowerOut) break;
                l.ForcedOff = true;
                sfx.PlaySfx("light_buzz", l.transform.position, 0.45f, 0.75f);
                yield return new WaitForSeconds(0.6f);
            }
            if (Running && !PowerOut)
            {
                // In the dark, close behind you: breathing.
                sfx.PlaySfx("breath_close", player.cam.transform.position - player.cam.transform.forward * 0.7f, 0.8f, 0.85f);
                yield return new WaitForSeconds(2.3f);
            }
            foreach (var l in order)
            {
                l.ForcedOff = PowerOut;
                if (!PowerOut) l.Burst(0.5f);
            }
            if (Running) sfx.PlaySfx2D("sting_reveal", 0.3f);
        }

        /// <summary>Something walks past outside the window of the room you're in, slowly. At the end, it turns and looks in.</summary>
        IEnumerator SomethingPassesTheWindow()
        {
            WindowEntry w = null;
            float best = 7f;
            foreach (var win in windows)
            {
                if (win == null) continue;
                float d = Vector3.Distance(Flat(win.SoundPosition), Flat(player.transform.position));
                if (d < best) { best = d; w = win; }
            }
            var who = PickOutside() ?? (defs.ContainsKey("hale") ? defs["hale"] : null);
            if (w == null || who == null) { yield return NailsOnGlass(); yield break; }
            probing = true;
            var outward = Flat(w.transform.forward).normalized;
            var along = Vector3.Cross(Vector3.up, outward) * (Random.value < 0.5f ? -1f : 1f);
            var center = w.outsideSpot.position + outward * 0.8f;
            prowler.Build(who.look.Single(), true, Plan.TellsFor(who, Mathf.RoundToInt(Minute)));
            prowler.Arrive(center - along * 3.2f, center + along * 0.6f, Quaternion.LookRotation(-outward, Vector3.up), 4.6f);
            for (float t = 0f; t < 6.2f && Running; t += Time.deltaTime)
            {
                if (t > 4.6f) prowler.StareAt(player.cam.transform.position);
                LightInTheYard(prowler, -outward);
                yield return null;
            }
            if (yardGlow != null) yardGlow.enabled = false;
            prowler.Hide();
            probing = false;
        }

        IEnumerator KnockOnAWindow()
        {
            var w = WindowAwayFromPlayer();
            if (w == null) yield break;
            int n = Random.Range(2, 5);
            bool slap = Random.value < 0.3f;
            for (int i = 0; i < n; i++)
            {
                bool last = i == n - 1;
                w.Rattle(slap && last ? 1f : 0.55f);
                sfx.PlaySfx(slap && last ? "knock_heavy" : "window_tap", w.SoundPosition, slap && last ? 0.7f : 0.9f, Random.Range(0.88f, 1.04f));
                yield return new WaitForSeconds(Random.Range(0.35f, 0.9f));
            }
        }

        IEnumerator TryTheBackDoor()
        {
            if (InKitchen) { yield return KnockOnAWindow(); yield break; }
            backDoor.Rattle(1.3f);
            sfx.PlaySfx("doorknob_rattle", backDoor.SoundPosition, 1f, 0.9f);
            yield return new WaitForSeconds(1.6f);
            for (int i = 0; i < 3; i++)
            {
                backDoor.Jolt(0.7f);
                sfx.PlaySfx("knock_single", backDoor.SoundPosition, 0.9f, Random.Range(0.84f, 0.94f));
                yield return new WaitForSeconds(Random.Range(0.4f, 0.75f));
            }
        }

        IEnumerator KnockAndNobody()
        {
            yield return KnockPattern(Random.value < 0.5f ? KnockStyle.Even : KnockStyle.Human, door.SoundPosition, true);
            yield return new WaitForSeconds(2.5f);
            if (Random.value < 0.6f)
            {
                door.RattleHandle(1.2f);
                sfx.PlaySfx("doorknob_rattle", door.SoundPosition, 0.7f, 0.85f);
            }
        }

        IEnumerator DoorMovesByItself()
        {
            var options = new List<InteriorDoor>();
            foreach (var d in new[] { bedroomDoor, bathroomDoor })
                if (d != null && Vector3.Distance(Flat(d.SoundPosition), Flat(player.transform.position)) > 2.2f) options.Add(d);
            if (options.Count == 0) { yield return KnockOnAWindow(); yield break; }
            var dr = options[Random.Range(0, options.Count)];
            if (dr.IsOpen)
            {
                dr.SetOpen(false, false);
                yield return new WaitForSeconds(0.45f);
                sfx.PlaySfx("door_slam", dr.SoundPosition, 0.95f, 1.05f);
                dr.Jolt(1f);
            }
            else
            {
                // Slowly, by itself, the way a door only opens when somebody is pushing it.
                sfx.PlaySfx("door_creak", dr.SoundPosition, 0.9f, 0.72f);
                dr.SetOpen(true, false);
            }
        }

        IEnumerator AWindowOpens()
        {
            int shut = 0;
            foreach (var w in windows) if (w != null && w.IsSecure) shut++;
            if (shut < windows.Length) { yield return NailsOnGlass(); yield break; }   // one open window at a time
            var target = windows[Random.Range(0, windows.Length)];
            target.BlowOpen();
            yield return new WaitForSeconds(1.5f);
            StartCoroutine(Thought("A window banged open somewhere. The " + target.displayName + "?", 2.4f));
        }

        IEnumerator StepsOnThePorch()
        {
            // Up the steps, across the boards, right up to the door. Then nothing. Then, quietly, away.
            float x = door.SoundPosition.x;
            for (int i = 0; i < 5; i++)
            {
                sfx.Footstep(new Vector3(x + Random.Range(-0.1f, 0.1f), 0f, Mathf.Lerp(6.2f, 3.7f, i / 4f)), 0.85f, 0.85f);
                yield return new WaitForSeconds(0.55f);
            }
            yield return new WaitForSeconds(3f);
            for (int i = 0; i < 4; i++)
            {
                sfx.Footstep(new Vector3(x, 0f, Mathf.Lerp(3.9f, 6.4f, i / 3f)), 0.4f, 0.8f);
                yield return new WaitForSeconds(0.75f);
            }
        }

        IEnumerator NailsOnGlass()
        {
            var w = WindowAwayFromPlayer();
            if (w == null) yield break;
            w.Rattle(0.4f);
            sfx.PlaySfx("window_scratch", w.SoundPosition, 0.9f, Random.Range(0.75f, 0.9f));
            yield return new WaitForSeconds(2.5f);
        }

        IEnumerator TvComesOn()
        {
            if (tv == null || PowerOut || tv.IsOn) yield break;
            tv.SetMode(TVSet.TVMode.Static);
            sfx.PlaySfx("tv_glitch", tv.transform.position, 0.9f);
            sfx.SetStatic(true);
            yield return new WaitForSeconds(Random.Range(3.5f, 6f));
            sfx.SetStatic(false);
            if (tv.Mode == TVSet.TVMode.Static) tv.SetMode(TVSet.TVMode.Off);
            sfx.PlaySfx("tv_glitch", tv.transform.position, 0.6f, 1.3f);
        }

        // ================================================================== the ones you sent away

        /// <summary>A mimic you turned away doesn't leave. What it tries next is never quite the same.</summary>
        IEnumerator RefusedRoutine(VisitorDef v)
        {
            // Its first try, soon after you turn it away - picked like every attack after it (see the siege).
            yield return Assault(v, prowler, CurrentVisitor != null);
            if (State == GameState.Playing && !roamers.Contains(v)) roamers.Add(v);
        }

        IEnumerator WindowsBurstOpen(VisitorDef v)
        {
            windowsBurstUsed = true;
            probing = true;
            var opened = new List<WindowEntry>();
            foreach (var w in windows)
            {
                if (w == null || !w.IsSecure) continue;
                w.BlowOpen();
                opened.Add(w);
                yield return new WaitForSeconds(Random.Range(0.5f, 1.1f));
            }
            sfx.PlaySfx2D("sting_high", 0.45f);
            StartCoroutine(Thought("Every window just flew open.\nLatch them. Now.", 2.8f));
            yield return new WaitForSeconds(9f);
            probing = false;
            WindowEntry target = null;
            foreach (var w in opened) if (!w.IsSecure) { target = w; break; }   // it goes for one that's still open
            if (State == GameState.Playing) yield return ProbeRoutine(v, target != null ? (EntryPoint)target : PickEntry());
        }

        IEnumerator FaceAtTheGlass(VisitorDef v)
        {
            // It presses up against the glass of the back door, or a window, and waits for you to notice.
            probing = true;
            EntryPoint spot = Random.value < 0.55f ? backDoor : (EntryPoint)WindowAwayFromPlayer() ?? backDoor;
            var stand = spot.outsideSpot;
            prowler.Build(v.look.Single(), true, Plan.TellsFor(v, Mathf.RoundToInt(Minute)));
            var close = stand.position + stand.forward * 0.42f;
            prowler.transform.SetPositionAndRotation(new Vector3(close.x, stand.position.y, close.z), stand.rotation);
            var eyes = EyesAtTheGlass(spot);
            prowler.PutEyesAt(eyes);
            sfx.PlaySfx("breath_close", spot.SoundPosition, 0.45f, 0.85f);
            float seen = 0f, waited = 0f;
            while (waited < 30f && seen < 1.1f && Running)
            {
                waited += Time.deltaTime;
                prowler.StareAt(player.cam.transform.position);
                // The head turns to follow you; the face stays on the glass.
                prowler.PutEyesAt(eyes, Time.deltaTime * 10f);
                GlowOnTheGlass(glassGlow, prowler.HeadPosition);
                if (PlayerSees(prowler.HeadPosition, 10f)) seen += Time.deltaTime;
                yield return null;
            }
            glassGlow.enabled = false;
            if (!Running) { prowler.Hide(); probing = false; yield break; }
            if (seen >= 1.1f)
            {
                sfx.PlaySfx2D("sting_reveal", 0.6f);
                sfx.PlaySfx2D("hit_bass", 0.5f);
                player.Shake(0.015f, 0.3f);
                yield return new WaitForSeconds(0.3f);
            }
            prowler.Hide();
            probing = false;
            if (Tier >= 1 && Random.value < 0.55f)
            {
                // It didn't leave. It's working the latch of the same glass.
                yield return new WaitForSeconds(Random.Range(1.5f, 3f));
                if (!Running) yield break;
                if (spot is WindowEntry win) yield return ForceWindow(v, prowler, win);
                else yield return ForceBackDoor(v, prowler);
                yield break;
            }
            for (int i = 0; i < 3; i++)
            {
                sfx.Footstep(spot.SoundPosition - stand.forward * (1.2f + i * 0.9f) + Vector3.down, 0.7f, 1.1f);
                yield return new WaitForSeconds(0.28f);
            }
            yield return new WaitForSeconds(Random.Range(5f, 9f));
            if (Running) yield return ProbeRoutine(v, backDoor);
        }

        /// <summary>Where its eyes go when it puts its face to this glass: the clear upper pane of a window, or level
        /// with the gap in the back door's blinds. Just outside the glass, so the nose nearly touches it.</summary>
        static Vector3 EyesAtTheGlass(EntryPoint spot)
        {
            if (spot is WindowEntry w)
            {
                var middle = w.SoundPosition;   // a window's sound point is the middle of the glass; its pivot is the sill
                return middle + Vector3.up * (middle.y - w.transform.position.y) * 0.5f + w.transform.forward * 0.08f;
            }
            var blinds = spot.GetComponentInChildren<BlindsInteractable>();
            var glass = spot.transform.position;
            float y = blinds != null && blinds.viewAnchor != null ? blinds.viewAnchor.position.y : glass.y + 1.5f;
            return new Vector3(glass.x, y, glass.z) - spot.outsideSpot.forward * 0.07f;
        }

        /// <summary>The house lamps don't reach anything outside, so a face at the glass would be a black shape.
        /// Light it the way the room behind the glass would: the colour and direction of the lamps that can see it.</summary>
        void GlowOnTheGlass(Light glow, Vector3 face)
        {
            if (glow == null) return;
            Color colour = Color.clear;
            Vector3 from = Vector3.zero;
            float total = 0f;
            foreach (var l in power.lights)
            {
                if (l == null || !l.isActiveAndEnabled || l.intensity < 0.01f) continue;
                var p = l.transform.position;
                float d = Vector3.Distance(p, face);
                if (d > l.range) continue;
                if (Physics.Linecast(p, face + (p - face).normalized * 0.4f, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;   // another room
                float k = l.intensity * (1f - d / l.range);
                colour += l.color * k;
                from += p * k;
                total += k;
            }
            if (total < 0.03f)
            {
                glow.enabled = false;
                return;
            }
            glow.color = colour / total;
            glow.intensity = Mathf.Min(total, 1.2f);
            glow.transform.position = face + (from / total - face).normalized * 0.8f;
            glow.cullingMask = 1 << prowler.gameObject.layer;
            glow.enabled = true;
        }

        IEnumerator CircleTheHouse(VisitorDef v)
        {
            // Footsteps all the way round the outside, a tap on every window it passes, ending at the back door.
            probing = true;
            var tapped = new HashSet<WindowEntry>();
            for (int i = 0; i < CircleRoute.Length - 1 && State == GameState.Playing; i++)
            {
                Vector3 a = CircleRoute[i], b = CircleRoute[i + 1];
                int steps = Mathf.CeilToInt(Vector3.Distance(a, b) / 0.75f);
                for (int s = 0; s < steps; s++)
                {
                    var p = Vector3.Lerp(a, b, (s + 0.5f) / steps);
                    sfx.Footstep(p, 0.75f, 0.85f);
                    foreach (var w in windows)
                    {
                        if (w == null || tapped.Contains(w) || Vector3.Distance(Flat(w.SoundPosition), Flat(p)) > 1.8f) continue;
                        tapped.Add(w);
                        w.Rattle(0.7f);
                        sfx.PlaySfx("window_tap", w.SoundPosition, 0.85f, 0.9f);
                    }
                    yield return new WaitForSeconds(0.52f);
                }
            }
            probing = false;
            if (State == GameState.Playing) yield return ProbeRoutine(v, backDoor);
        }

        IEnumerator WatchFromTheYard(VisitorDef v)
        {
            // It stands out in the yard where a window (or the peephole) can see it, and doesn't move.
            var spot = WatchSpots[Random.Range(0, WatchSpots.Length)];
            prowler.Build(v.look.Single(), true, Plan.TellsFor(v, Mathf.RoundToInt(Minute)));
            var look = Flat(spot.lookAt - spot.at);
            prowler.transform.SetPositionAndRotation(spot.at, Quaternion.LookRotation(look.normalized, Vector3.up));
            float seen = 0f, waited = 0f;
            while (waited < 32f && seen < 2f && Running)
            {
                waited += Time.deltaTime;
                prowler.StareAt(player.cam.transform.position);
                LightInTheYard(prowler, look);
                if (PlayerSees(prowler.HeadPosition, 22f, 24f)) seen += Time.deltaTime;
                yield return null;
            }
            if (seen >= 2f && Running && LightningSystem.Instance != null && !LightningSystem.Instance.Flashing)
            {
                // You've seen it. The lightning goes, and when the dark comes back, so has it.
                StartCoroutine(LightningSystem.Instance.Strike(true, true));
                yield return new WaitForSeconds(0.12f);
            }
            if (yardGlow != null) yardGlow.enabled = false;
            prowler.Hide();
        }

        // ================================================================== the street

        IEnumerator RoamLoop()
        {
            float timer = Random.Range(45f, 80f);
            while (State == GameState.Playing)
            {
                if (roamers.Count > 0 && !roamActive && !InCutscene && !Paused && !Dawn)
                {
                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                    {
                        StartCoroutine(RoamPass(roamers[Random.Range(0, roamers.Count)]));
                        timer = Random.Range(70f, 130f);
                    }
                }
                yield return null;
            }
        }

        /// <summary>Called when a real person arrives: sometimes one of them happens by on the street while they wait.</summary>
        void MaybeRoamDuringVisit()
        {
            if (roamers.Count == 0 || roamActive || Dawn || Random.value > 0.55f) return;
            StartCoroutine(RoamPassAfter(Random.Range(5f, 14f)));
        }

        IEnumerator RoamPassAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!roamActive && roamers.Count > 0 && State == GameState.Playing && !InCutscene && !Dawn)
                yield return RoamPass(roamers[Random.Range(0, roamers.Count)]);
        }

        IEnumerator RoamPass(VisitorDef v)
        {
            roamActive = true;
            roamDef = v;
            if (!roamWarned)
            {
                roamWarned = true;
                phone.DeliverLater("Mr. Voss", RoamWarning, false, 4f);
            }
            // Its eyes catch the light, so through the peephole you can't miss it standing out there.
            roamer.Build(v.look, true, Plan.TellsFor(v, Mathf.RoundToInt(Minute)) | Tells.Eyeshine);
            bool fromEast = Random.value < 0.5f;
            float z = Random.Range(15.3f, 16.2f);
            var a = new Vector3(fromEast ? 16f : -17f, -0.45f, z);
            var b = new Vector3(fromEast ? -17f : 16f, -0.45f, z);
            var gate = new Vector3(-1.5f, -0.45f, 14.3f);
            var yard = new Vector3(Random.Range(-2.2f, -1.6f), -0.45f, Random.Range(9.3f, 9.9f));
            const float pace = 1.05f;

            // Along the street to the gate...
            float t1 = Vector3.Distance(a, gate) / pace;
            roamer.Arrive(a, gate, Quaternion.LookRotation(Vector3.back), t1);
            yield return WhileRoaming(t1 + 0.2f);
            // ...through it - you hear it go - and up the path. It stands in the yard and looks at your door, in plain
            // sight of the peephole, behind whoever is on your step.
            if (!roamRushing)
            {
                sfx.PlaySfx("door_creak", gate + Vector3.up, 0.45f, 1.7f);
                var toDoor = Flat(new Vector3(0f, 0f, 3.4f) - yard).normalized;
                float t2 = Vector3.Distance(gate, yard) / pace;
                roamer.Arrive(gate, yard, Quaternion.LookRotation(toDoor, Vector3.up), t2);
                yield return WhileRoaming(t2 + 0.2f);
            }
            if (!roamRushing)
            {
                roamGlow.enabled = true;
                for (float w = Random.Range(5f, 8f); w > 0f && !roamRushing && State == GameState.Playing; w -= Time.deltaTime)
                {
                    roamer.StareAt(peepholeAnchor.position);
                    roamGlow.transform.position = roamer.HeadPosition + Flat(peepholeAnchor.position - roamer.HeadPosition).normalized * 0.9f + Vector3.up * 0.25f;
                    yield return null;
                }
                roamGlow.enabled = false;
            }
            // Back out through the gate, and on down the street.
            if (!roamRushing)
            {
                float t3 = Vector3.Distance(yard, gate) / pace;
                roamer.Arrive(yard, gate, Quaternion.LookRotation((gate - yard).normalized), t3);
                yield return WhileRoaming(t3 + 0.2f);
            }
            if (!roamRushing)
            {
                float t4 = Vector3.Distance(gate, b) / pace;
                roamer.Arrive(gate, b, Quaternion.LookRotation((b - gate).normalized), t4);
                yield return WhileRoaming(t4 + 0.2f);
            }
            if (roamRushing) yield break;
            roamer.Hide();
            roamActive = false;
            roamDef = null;
        }

        IEnumerator WhileRoaming(float seconds)
        {
            for (float t = 0f; t < seconds && !roamRushing && State == GameState.Playing; t += Time.deltaTime) yield return null;
        }

        /// <summary>
        /// One of them is in your front yard and would see the door swing open. It only counts if you could have seen it
        /// too: inside the gate, in plain view of the peephole, with nothing in the way. Out on the street, it's too far
        /// off to see your door - and so are you.
        /// </summary>
        bool RoamerSeesDoor()
        {
            if (!roamActive || roamRushing || roamer == null || !roamer.Visible) return false;
            return roamer.transform.position.z < 13.6f && InPeepholeView(roamer.HeadPosition);
        }

        /// <summary>Would this point show up plainly through the peephole: well inside the lens, nothing in the way?</summary>
        bool InPeepholeView(Vector3 point)
        {
            if (peepholeAnchor == null) return false;
            var eye = peepholeAnchor.position;
            var to = point - eye;
            float dist = to.magnitude;
            if (dist < 0.5f || dist > 18f || Vector3.Angle(peepholeAnchor.forward, to) > 34f) return false;
            var dir = to / dist;
            return !Physics.Raycast(eye + dir * 0.35f, dir, dist - 0.8f, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>You opened the door with one of them watching. It runs.</summary>
        IEnumerator RoamerRushIn(VisitorDef guest)
        {
            if (!BeginDeath()) yield break;
            roamRushing = true;
            var v = roamDef;
            Deaths++;
            roamer.Halt();
            sfx.Silence(0.4f);
            sfx.SetTension(1f);
            var toDoor = Flat(door.SoundPosition - roamer.transform.position);
            roamer.transform.rotation = Quaternion.LookRotation(toDoor.normalized, Vector3.up);
            sfx.PlaySfx("creature_growl", roamer.HeadPosition, 1f, 0.8f);
            sfx.PlaySfx2D("sting_high", 0.7f);
            yield return new WaitForSeconds(0.45f);
            if (roamGlow != null) roamGlow.enabled = false;
            if (roamer.transform.position.z > 13.4f) yield return roamer.RunTo(new Vector3(0f, -0.45f, 13.4f), 6.5f);   // the gate
            yield return roamer.RunTo(new Vector3(0f, -0.45f, 6.5f), 6.5f);    // the steps
            if (visitor.Visible)
            {
                // Whoever you were letting in is in the way. Not for long.
                StartCoroutine(visitor.DraggedAway(Vector3.right * 0.8f + Vector3.forward * 0.4f, 4.5f, 0.8f));
                sfx.PlaySfx("creature_scream", door.SoundPosition + Vector3.forward * 1.5f, 1f, 0.9f);
            }
            yield return roamer.RunTo(new Vector3(0.3f, 0f, 4.1f), 6.5f);       // the porch
            string name = v != null ? v.name : "One of them";
            yield return Scare(roamer, "IT CAME IN WITH THEM.",
                name + " was standing in your front yard when you opened the door. It saw.\nBefore you open the door, look through the peephole. If one of them is in the yard, wait until it's gone.");
            roamRushing = false;
            roamActive = false;
            roamDef = null;
        }
    }
}
