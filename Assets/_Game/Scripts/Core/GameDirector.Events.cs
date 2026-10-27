using System.Collections;
using UnityEngine;

namespace DontAnswer
{
    public partial class GameDirector
    {
        // ================================================================== scheduled house events

        void TickEvents()
        {
            eventCooldown -= Time.deltaTime;
            if (eventCooldown > 0f || InCutscene) return;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (firedEvents.Contains(i) || Minute < e.minute) continue;
                if (e.needsQuiet && (CurrentVisitor != null || IsTalking || (PowerOut && e.kind != EventKind.Thunder))) return;
                firedEvents.Add(i);
                StartCoroutine(RunEvent(e.kind));
                eventCooldown = 8f;
                return;
            }
        }

        IEnumerator RunEvent(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Thunder: yield return Thunder(); break;
                case EventKind.LightsFlicker:
                    foreach (var l in lamps) if (l != null) l.Burst(2.6f);
                    if (lamps.Length > 0) sfx.PlaySfx("light_buzz", lamps[0].transform.position, 0.8f);
                    break;
                case EventKind.WindowTap:
                    var w = windows[0];
                    for (int i = 0; i < 4; i++)
                    {
                        sfx.PlaySfx("window_tap", w.SoundPosition, 0.9f, Random.Range(0.95f, 1.05f));
                        yield return new WaitForSeconds(Random.Range(0.4f, 0.8f));
                    }
                    break;
                case EventKind.WindowFigure:
                    if (windowFigure != null) windowFigure.Play();
                    yield return new WaitForSeconds(1.2f);
                    sfx.PlaySfx2D("sting_reveal", 0.25f);
                    sfx.PlaySfx2D("hit_bass", 0.35f);
                    break;
                case EventKind.Dishes:
                {
                    // Someone is putting the dishes away. You live alone.
                    var sink = new Vector3(-4.9f, 1.0f, 0.6f);
                    sfx.PlaySfx("dishes", sink, 0.9f);
                    yield return new WaitForSeconds(2.2f);
                    sfx.PlaySfx("dishes", sink, 0.5f, 0.92f);
                    if (!InKitchen) StartCoroutine(Thought("Was that the kitchen?", 2.2f));
                    break;
                }
                case EventKind.ToiletFlush:
                {
                    var toilet = new Vector3(2.65f, 0.5f, -6.4f);
                    if (Vector3.Distance(player.transform.position, toilet) < 3f) break;
                    sfx.PlaySfx("toilet_flush", toilet, 1f);
                    yield return new WaitForSeconds(4f);
                    StartCoroutine(Thought("The toilet just flushed.\nThere's nobody in the bathroom.", 2.8f));
                    break;
                }
                case EventKind.Snoring:
                {
                    // Something is asleep in your bed. It stops the moment you open the door.
                    var bed = new Vector3(-2.35f, 0.7f, -6.6f);
                    for (int i = 0; i < 4 && Vector3.Distance(player.transform.position, bed) > 3.2f; i++)
                    {
                        sfx.PlaySfx("snore", bed, 0.75f, 0.85f);
                        yield return new WaitForSeconds(Random.Range(3.4f, 4.2f));
                    }
                    break;
                }
                case EventKind.Footsteps:
                    sfx.PlaySfx("footsteps_ceiling", upstairsPoint.position, 1f);
                    yield return new WaitForSeconds(5f);
                    sfx.PlaySfx("chair_scrape", upstairsPoint.position + Vector3.right * 2f, 0.6f, 0.8f);
                    break;
                case EventKind.Doorknob:
                    door.RattleHandle(1.4f);
                    door.Bump(0.3f);
                    sfx.PlaySfx("doorknob_rattle", door.SoundPosition, 1f);
                    yield return new WaitForSeconds(2.4f);
                    door.RattleHandle(0.6f);
                    sfx.PlaySfx("doorknob_rattle", door.SoundPosition, 0.8f, 0.9f);
                    break;
                case EventKind.Breathing:
                    sfx.SetBreath(true);
                    yield return new WaitForSeconds(9f);
                    sfx.SetBreath(false);
                    break;
                case EventKind.MailSlot:
                    yield return mailSlot.Whisper(Minute > 250f ? "SLOT_02" : "SLOT_01", true);
                    break;
                case EventKind.WindBlowsWindow:
                    var target = windows[Random.Range(1, windows.Length)];
                    if (target.IsSecure) target.BlowOpen();
                    yield return new WaitForSeconds(1.5f);
                    if (!target.IsSecure) StartCoroutine(Thought("A window just banged open somewhere. The " + target.displayName + "?"));
                    break;
                case EventKind.BedroomMemory: yield return BedroomMemory(); break;
                case EventKind.HomeCall: yield return LandlineCall("HOME  (555) 0131", "home"); break;
                case EventKind.MomCall: yield return LandlineCall("MOM", "mom"); break;
                case EventKind.TVBroadcast: yield return Broadcast(); break;
                case EventKind.RulesAmended:
                    rulesStage = Minute >= 240f ? 2 : 1;
                    ui.ShowToast("The rules", rulesStage == 1 ? "Someone has written on the rules by the door." : "There's new writing on the rules.", true);
                    sfx.PlaySfx2D("sting_reveal", 0.18f);
                    break;
                case EventKind.Siege: yield return Siege(); break;
                case EventKind.ChairScrape:
                    sfx.PlaySfx("chair_scrape", backDoor.SoundPosition + Vector3.right * 1.5f, 0.9f);
                    yield return new WaitForSeconds(3f);
                    sfx.PlaySfx("glass_break", backDoor.SoundPosition + Vector3.right * 1.2f, 0.7f);
                    break;
                case EventKind.WallScratch:
                    var p = player.transform.position + player.transform.right * 1.6f + Vector3.up * 1f;
                    sfx.PlaySfx("wall_scratch", p, 1f);
                    break;
            }
        }

        IEnumerator Thunder()
        {
            if (LightningSystem.Instance != null)
            {
                yield return LightningSystem.Instance.Strike(true, true);
                yield break;
            }
            float moon = moonLight != null ? moonLight.intensity : 0f;
            for (int i = 0; i < 2; i++)
            {
                if (lightningLight != null) lightningLight.intensity = 3.2f;
                if (moonLight != null) moonLight.intensity = moon * 6f;
                yield return new WaitForSeconds(Random.Range(0.05f, 0.1f));
                if (lightningLight != null) lightningLight.intensity = 0f;
                if (moonLight != null) moonLight.intensity = moon;
                yield return new WaitForSeconds(Random.Range(0.08f, 0.2f));
            }
            yield return new WaitForSeconds(Random.Range(0.4f, 1.2f));
            sfx.PlaySfx2D("thunder", 0.85f, Random.Range(0.85f, 1.05f));
        }

        /// <summary>Someone knocks on the bedroom door from inside the apartment. It's a memory. Probably.</summary>
        IEnumerator BedroomMemory()
        {
            if (bedroomDoor == null) yield break;
            bedroomDoor.SetOpen(false, true);
            yield return new WaitForSeconds(1.2f);
            yield return KnockPatternAt(bedroomDoor);
            yield return Say("MEM_ASK", "LEO", MemoryColor, bedroomDoor.SoundPosition, true, false, 1.25f, false);
            int token = ui.ShowSubtitle("YOU  (a year ago)", YouColor, "<b>GO AWAY, LEO.</b>");
            sfx.PlaySfx2D("sting_reveal", 0.2f, 0.8f);
            yield return new WaitForSeconds(2.2f);
            ui.HideSubtitle(token);
            yield return Say("MEM_OKAY", "LEO", MemoryColor, bedroomDoor.SoundPosition, true, false, 1.25f, false);
            sfx.PlaySfx("footsteps_room", bedroomDoor.SoundPosition - bedroomDoor.transform.forward * 2f, 0.6f);
            yield return new WaitForSeconds(3f);
            bedroomDoor.SetOpen(true, true);
        }

        IEnumerator KnockPatternAt(InteriorDoor d)
        {
            for (int k = 0; k < 3; k++)
            {
                sfx.PlaySfx("knock_soft", d.SoundPosition, 0.8f, 1.15f);
                d.Jolt(0.6f);
                yield return new WaitForSeconds(Random.Range(0.3f, 0.45f));
            }
        }

        // ================================================================== the landline

        bool callAnswered;

        IEnumerator LandlineCall(string callerId, string key)
        {
            callAnswered = false;
            landline.StartRinging(callerId, key);
            ui.ShowToast("The phone is ringing", "Caller ID: " + callerId, key == "home");
            float t = 0f;
            while (t < 24f && !callAnswered && landline.Ringing)
            {
                t += Time.deltaTime;
                yield return null;
            }
            if (callAnswered) yield break;
            landline.StopRinging();
            if (key == "mom")
            {
                // Nobody picked up. The answering machine does.
                sfx.PlaySfx("walkie_static", landline.SpeakerPosition, 0.5f);
                yield return new WaitForSeconds(0.6f);
                yield return Say("MOM_CALL1", "MOM  (answering machine)", InsideColor, landline.SpeakerPosition, true, false, 1f, false);
            }
        }

        public void AnswerLandline(Landline l)
        {
            if (!CanFreelyInteract || !l.Ringing) return;
            callAnswered = true;
            string key = l.CallKey;
            l.StopRinging();
            StartCoroutine(PhoneConversation(key));
        }

        IEnumerator PhoneConversation(string key)
        {
            InCutscene = true;
            sfx.PlaySfx2D("intercom", 0.6f, 0.7f);
            yield return new WaitForSeconds(0.5f);
            if (key == "home")
            {
                // Rule 6. Too late now.
                Plan.homeCallAnswered = true;
                yield return Say("HOME_01", "MOM", InsideColor, null, false, true, 1f, false);
                yield return Say("HOME_02", "MOM", InsideColor, null, false, true, 0.97f, false);
                sfx.PlaySfx2D("whisper_swirl", 0.8f);
                yield return Say("HOME_03", "???", AngryColor, null, false, true, 0.9f, false);
                sfx.PlaySfx2D("sting_reveal", 0.35f);
                yield return Thought("It was calling from your own number. Rule 6.", 2.6f);
            }
            else
            {
                yield return Say("MOM_CALL1", "MOM", InsideColor, null, false, false, 1f, true);
            }
            InCutscene = false;
        }

        // ================================================================== the TV

        IEnumerator Broadcast()
        {
            if (tv == null || PowerOut) yield break;
            sfx.PlaySfx("tv_on", tv.transform.position, 0.8f);
            tv.SetMode(TVSet.TVMode.News);
            yield return new WaitForSeconds(1f);
            yield return Say("NEWS_01", "TV", StaticColor, tv.transform.position, false, false, 1f, false);
            yield return Say("NEWS_02", "TV", StaticColor, tv.transform.position, false, false, 1f, false);
            tv.SetMode(TVSet.TVMode.Static);
            sfx.PlaySfx("tv_glitch", tv.transform.position, 0.8f);
            yield return new WaitForSeconds(0.8f);
            tv.SetMode(TVSet.TVMode.News);
            yield return Say("NEWS_03", "TV", StaticColor, tv.transform.position, false, true, 1f, false);
            tv.SetMode(TVSet.TVMode.Static);
            sfx.PlaySfx("tv_glitch", tv.transform.position, 1f, 0.8f);
            yield return new WaitForSeconds(1.2f);

            // A live picture of your living room, from outside the window. Something is standing behind you.
            if (tvFeedFigure != null)
            {
                var back = player.transform.position - player.transform.forward * 1.1f;
                back.y = player.transform.position.y;
                tvFeedFigure.position = back;
                tvFeedFigure.rotation = Quaternion.LookRotation(player.transform.forward, Vector3.up);
                tvFeedFigure.gameObject.SetActive(true);
            }
            tv.SetMode(TVSet.TVMode.Feed);
            yield return Say("NEWS_04", "TV", AngryColor, tv.transform.position, false, true, 0.9f, false);
            float t = 0f;
            while (t < 10f)
            {
                t += Time.deltaTime;
                if (tvFeedFigure != null && Vector3.Dot(player.cam.transform.forward, (tvFeedFigure.position - player.transform.position).normalized) > 0.6f) break;
                yield return null;
            }
            if (tvFeedFigure != null) tvFeedFigure.gameObject.SetActive(false);
            sfx.PlaySfx2D("sting_high", 0.55f);
            sfx.PlaySfx("tv_glitch", tv.transform.position, 1f, 1.2f);
            tv.SetMode(TVSet.TVMode.Off);
        }

        // ================================================================== 5 AM: everything at once

        IEnumerator Siege()
        {
            sfx.PlaySfx2D("whisper_swirl", 0.7f);
            foreach (var l in lamps) if (l != null) l.Burst(6f);
            for (int round = 0; round < 3; round++)
            {
                StartCoroutine(KnockPattern(KnockStyle.Frantic, door.SoundPosition, true));
                yield return new WaitForSeconds(1.4f);
                sfx.PlaySfx("doorknob_rattle", backDoor.SoundPosition, 1f);
                yield return backDoor.Probe(null);
                var w = windows[Random.Range(0, windows.Length)];
                sfx.PlaySfx("window_scratch", w.SoundPosition, 1f);
                if (!w.IsSecure || !backDoor.IsSecure)
                {
                    var entry = !backDoor.IsSecure ? (EntryPoint)backDoor : w;
                    yield return BreakInRoutine(defs["hale"], entry);
                    yield break;
                }
                yield return new WaitForSeconds(2f);
            }
            yield return mailSlot.Whisper("SLOT_01", false);
        }

        // ================================================================== 3:03 AM: the power goes

        IEnumerator OutageRoutine()
        {
            PowerOut = true;
            sfx.PlaySfx2D("breaker_off", 0.9f);
            power.SetPower(false);
            sfx.SetPower(false);
            checkpoint = Capture();
            yield return new WaitForSeconds(1.5f);

            if (guests.ContainsKey("tom_h"))
            {
                guests.TryGetValue("tom_h", out var tomAv);
                yield return Say("TOMH_IN2", "TOM", InsideColor, tomAv != null ? tomAv.HeadPosition : (Vector3?)null, false, false, 1f, false);
                if (tomAv != null) tomAv.gameObject.SetActive(false);
                ui.SetHint("Tom went down to the basement.\nWait in the dark. [F] Light");
                float t = 0f;
                while (t < 45f) { t += Time.deltaTime; yield return null; }
                sfx.PlaySfx2D("breaker_on", 0.9f);
                RestorePower();
                if (tomAv != null) tomAv.gameObject.SetActive(true);
                yield return Say("TOMH_IN3", "TOM", InsideColor, tomAv != null ? tomAv.HeadPosition : (Vector3?)null, false, false, 1f, false);
                ui.SetHint(null);
                yield break;
            }

            ui.SetHint(flashlight.HasFlashlight
                ? "The power's out. The fuse box is in the basement, through the kitchen.\n[F] Flashlight"
                : "The power's out. The fuse box is in the basement, through the kitchen.\n[F] Phone light. It isn't much.");
            if (basementDoor != null) basementDoor.SetOpen(false, false);
            bool warned = false, nudged = false;
            float dark = 0f, sounds = Random.Range(6f, 10f);
            while (PowerOut)
            {
                if (!warned && InBasement)
                {
                    warned = true;
                    ui.SetHint("The fuse box is on the far wall.\nSomeone has written on the paper above it.");
                }
                // Nothing down here. Just small, light footsteps somewhere behind you in the dark.
                if (InBasement && !InCutscene && (sounds -= Time.deltaTime) <= 0f)
                {
                    sounds = Random.Range(7f, 13f);
                    StartCoroutine(SmallSteps(player.transform.position - player.transform.forward * Random.Range(2.5f, 4f)));
                }
                if (!InCutscene) dark += Time.deltaTime;
                if (!nudged && !InBasement && dark > 75f)
                {
                    nudged = true;
                    ui.SetHint("The basement door is in the kitchen, past the fridge.\nThe stairs go down. The fuse box is on the far wall.");
                }
                if (dark > 240f && !InBasement && !InCutscene)
                {
                    // Too long. Something down there throws the breaker for you, and you hear it climb the stairs.
                    yield return PowerComesBackOnItsOwn();
                    yield break;
                }
                Minute = Mathf.Min(NextItemMinute() - 3f, Minute + Time.deltaTime * visitMinutesPerSecond);
                yield return null;
            }
        }

        IEnumerator PowerComesBackOnItsOwn()
        {
            yield return SmallSteps(new Vector3(-4f, -2.8f, -7f));   // the middle of the basement
            yield return new WaitForSeconds(1.2f);
            sfx.PlaySfx2D("breaker_on", 0.7f, 0.9f);
            RestorePower();
            if (basementDoor != null)
            {
                // Up the stairs (they run south from the door, down to the basement floor).
                var top = basementDoor.SoundPosition;
                for (int i = 0; i < 5; i++)
                {
                    float k = i / 4f;
                    sfx.Footstep(new Vector3(top.x, Mathf.Lerp(-2.5f, -0.2f, k), Mathf.Lerp(-7.0f, -4.4f, k)), 0.9f, 0.7f);
                    yield return new WaitForSeconds(0.55f);
                }
                sfx.PlaySfx("door_creak", basementDoor.SoundPosition, 0.8f, 0.7f);
                basementDoor.SetOpen(true, true);
            }
            yield return Thought("The lights came back on by themselves.\nSomething down there turned them on.", 3f);
        }

        public void ThrowBreaker()
        {
            if (!PowerOut) return;
            sfx.PlaySfx2D("breaker_on", 0.9f);
            RestorePower();
            StartCoroutine(AfterBreaker());
        }

        void RestorePower()
        {
            PowerOut = false;
            power.SetPower(true);
            sfx.SetPower(true);
            ui.SetHint(null);
        }

        IEnumerator AfterBreaker()
        {
            if (InBasement) yield return LeoInTheBasement();
            yield return Thought("The lights are back. Go upstairs.", 2.2f);
        }

        // ================================================================== the basement: nobody down here

        VisitorAvatar basementLeo;
        static readonly Vector3 FootOfTheStairs = new Vector3(-5.1f, -2.8f, -7.85f);   // just out of the stairwell, where you have to pass

        /// <summary>Three or four quick, light steps - a child's - and then nothing.</summary>
        IEnumerator SmallSteps(Vector3 at)
        {
            int n = Random.Range(3, 5);
            var dir = Random.insideUnitSphere;
            dir.y = 0f;
            for (int i = 0; i < n; i++)
            {
                sfx.Footstep(at + dir.normalized * 0.35f * i, 0.32f, 1.3f);
                yield return new WaitForSeconds(Random.Range(0.18f, 0.26f));
            }
        }

        /// <summary>
        /// The bulb comes back on, and there's a little boy in a tuxedo at the foot of the stairs, where you'll have to
        /// walk past him, looking at you. The moment you see him the bulb pops - and he's right in front of you. Then
        /// he's gone. Nothing is down here. Nothing ever was.
        /// </summary>
        IEnumerator LeoInTheBasement()
        {
            if (!defs.TryGetValue("leo_real", out var leo)) yield break;
            if (basementLeo == null)
            {
                // Indoors, on the default layer, so the basement bulb lights him like anything else down here.
                var go = new GameObject("Basement Leo");
                go.transform.SetParent(transform, false);
                basementLeo = go.AddComponent<VisitorAvatar>();
                basementLeo.CopyMaterialsFrom(visitor);
            }
            var look = leo.look.Single();
            look.idle = IdleStyle.Still;
            basementLeo.Build(look, true, Tells.Eyeshine);
            basementLeo.SetBreathVisible(false);
            var toYou = Flat(player.transform.position - FootOfTheStairs);
            basementLeo.transform.SetPositionAndRotation(FootOfTheStairs, Quaternion.LookRotation(toYou.normalized, Vector3.up));

            // Wait until you see him, or walk into him. If you never turn round, he was never there.
            var cam = player.cam.transform;
            float waited = 0f;
            bool seen = false;
            while (waited < 40f && PowerOut == false && State == GameState.Playing && !InCutscene)
            {
                waited += Time.deltaTime;
                basementLeo.StareAt(cam.position);
                var to = basementLeo.HeadPosition - cam.position;
                bool inView = Vector3.Angle(cam.forward, to) < 17f && !Physics.Raycast(cam.position, to.normalized, to.magnitude - 0.3f, ~0, QueryTriggerInteraction.Ignore);
                if ((inView && waited > 0.6f) || Flat(player.transform.position - FootOfTheStairs).magnitude < 1.6f) { seen = true; break; }
                if (!InBasement) break;
                yield return null;
            }
            if (!seen || !InBasement || InCutscene || State != GameState.Playing)
            {
                basementLeo.Hide();
                yield break;
            }

            InCutscene = true;
            yield return new WaitForSeconds(0.45f);   // long enough to know what you're looking at

            // The bulb dies...
            SetLamps(false);
            if (flashlight.On) flashlight.Set(false);
            sfx.PlaySfx2D("light_buzz", 0.6f, 1.4f);
            yield return new WaitForSeconds(0.22f);

            // ...and comes back with his face right in front of yours.
            FaceToFace(basementLeo, 0.48f);
            LightTheFace();
            SetLamps(true);
            sfx.PlaySfx2D("death_flash", 0.85f, 1.12f);
            player.Shake(0.035f, 0.25f);
            yield return new WaitForSeconds(0.3f);

            // Dark again. When the bulb comes back, there's nobody there.
            SetLamps(false);
            faceLight.enabled = false;
            basementLeo.Hide();
            yield return new WaitForSeconds(0.35f);
            SetLamps(true);
            yield return new WaitForSeconds(0.3f);
            InCutscene = false;
            yield return Thought("...Leo?", 1.8f);
        }
    }
}
