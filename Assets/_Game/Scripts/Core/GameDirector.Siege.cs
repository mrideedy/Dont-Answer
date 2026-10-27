using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>
    /// The night gets worse. Menace climbs with the clock and with every one of them you turn away; the higher it is,
    /// the more often they come back, the more ways they try, and the less time you get. Later on they work the window
    /// latches and the back door's bolt from outside - visibly, a little at a time - and you have to get there and force
    /// them back. And when one of them is angry at the front door, the peephole is the worst place to be: it sees the
    /// light in it go dark, and catching its eye is an invitation.
    /// </summary>
    public partial class GameDirector
    {
        VisitorAvatar prowler2;            // for when they come two at once
        Light glassGlow2, yardGlow, roamGlow;
        VisitorAvatar doorThreat;          // something hostile right outside the front door: the peephole is deadly
        VisitorDef doorThreatDef;
        VisitorDef lastOutside;
        float doorStare, provoked;
        bool eyeContact, caughtAtPeephole, caughtOnce, warnedPeephole, dying, vanished;
        int lastAssault = -1, lastAssault2 = -1, tierWarned;
        readonly HashSet<string> hinted = new HashSet<string>();
        readonly HashSet<EntryPoint> beingWorked = new HashSet<EntryPoint>();

        /// <summary>How bad the night has got, 0..1: the clock, how many of them are out there, and whether you've given yourself away.</summary>
        public float Menace => Mathf.Clamp01(Mathf.InverseLerp(20f, 300f, Minute) * 0.86f + Mathf.Min(0.27f, OutsideCount * 0.06f) + provoked);

        /// <summary>0 watching, 1 testing the latches, 2 angry, 3 the siege.</summary>
        public int Tier => Menace < 0.22f ? 0 : Menace < 0.47f ? 1 : Menace < 0.72f ? 2 : 3;

        int OutsideCount => roamers.Count + probeQueue.Count;
        float TierK => Mathf.InverseLerp(0.22f, 1f, Menace);   // 0 at the first tier, 1 at the worst
        bool Running => State == GameState.Playing && !InCutscene;
        float ForceTime() => Mathf.Lerp(10.5f, 5f, TierK);
        /// <summary>The last minutes before sunrise belong to the last two knocks: nothing new starts outside after 5:46.</summary>
        bool Dawn => Minute >= StoryScript.Sunrise - 14f;

        // ================================================================== setup

        void CreateSiege()
        {
            if (prowler2 != null || prowler == null || visitor == null) return;
            var go = new GameObject("Prowler 2");
            go.layer = prowler.gameObject.layer;
            go.transform.SetParent(prowler.transform.parent, false);
            prowler2 = go.AddComponent<VisitorAvatar>();
            prowler2.CopyMaterialsFrom(visitor);
            glassGlow2 = MakeGlow("Glass Glow 2", 2.2f);
            yardGlow = MakeGlow("Yard Glow", 4.5f);
            yardGlow.color = new Color(0.6f, 0.68f, 0.88f);
            roamGlow = MakeGlow("Roam Glow", 4f);   // the one in your front yard, so the peephole shows it plainly
            roamGlow.color = new Color(0.62f, 0.7f, 0.9f);
            roamGlow.intensity = 1.4f;
        }

        Light MakeGlow(string name, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.shadows = LightShadows.None;
            l.range = range;
            l.cullingMask = 1 << prowler.gameObject.layer;
            l.enabled = false;
            return l;
        }

        /// <summary>Clears whatever was happening outside (used on a new night and on every retry).</summary>
        void ResetSiege()
        {
            doorThreat = null;
            doorThreatDef = null;
            lastOutside = null;
            doorStare = provoked = 0f;
            eyeContact = caughtAtPeephole = dying = vanished = false;
            lastAssault = lastAssault2 = -1;
            beingWorked.Clear();
            guestStopped = false;
            if (luredGuest != null) luredGuest.active = false;
            luredGuest = null;
            if (prowler2 != null) prowler2.Hide();
            foreach (var l in new[] { glassGlow, glassGlow2, yardGlow, roamGlow }) if (l != null) l.enabled = false;
        }

        /// <summary>A whole new night: forget the warnings too.</summary>
        void ForgetSiege()
        {
            caughtOnce = warnedPeephole = false;
            tierWarned = 0;
            hinted.Clear();
        }

        /// <summary>A hint the first time something happens, never again.</summary>
        void Hint(string key, string text)
        {
            if (!hinted.Add(key)) return;
            ui.SetHint(text);
            StartCoroutine(ClearHintLater(text, 6f));
        }

        IEnumerator ClearHintLater(string text, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            ui.ClearHintIf(text);
        }

        /// <summary>Only one death at a time, however many of them are at it.</summary>
        bool BeginDeath()
        {
            if (dying) return false;
            dying = true;
            return true;
        }

        // ================================================================== how bad it is

        /// <summary>Mr. Voss texts as each stage of the night starts. Called every frame while playing.</summary>
        void CheckTier()
        {
            int tier = Tier;
            if (tier <= tierWarned || InCutscene) return;
            tierWarned = tier;
            switch (tier)
            {
                case 1:
                    phone.DeliverLater("Mr. Voss", "It's started. They test the latches now. If you hear one of them working at a window or the back door, GET THERE and shove it shut.", false, 2f);
                    break;
                case 2:
                    phone.DeliverLater("Mr. Voss", "They're angry now. More of them, more often. When one is pounding on your door, stay off the peephole. Don't let it catch your eye.", false, 2f);
                    break;
                case 3:
                    phone.DeliverLater("Mr. Voss", "This is the worst of it. They'll come two at a time now. Keep moving. Sunrise is at 6.", false, 2f);
                    break;
            }
        }

        // ================================================================== the ones outside keep coming back

        IEnumerator AssaultLoop()
        {
            float timer = 25f, settle = 0f;
            while (State == GameState.Playing)
            {
                yield return null;
                if (Paused || InCutscene || (PowerOut && InBasement) || Dawn) continue;
                float dt = Time.deltaTime;
                provoked = Mathf.MoveTowards(provoked, 0f, dt * 0.003f);
                int tier = Tier;
                if (tier == 0) continue;
                // The clock keeps running while you deal with the door; it just waits for the door to go quiet
                // (a few seconds after, never on the dot). Only the siege doesn't wait.
                timer -= dt;
                bool visiting = CurrentVisitor != null;
                settle = visiting || IsTalking ? Random.Range(2.5f, 5f) : settle - dt;
                if (timer > 0f) continue;
                if (probing || prowler.Visible || IsTalking || (visiting ? tier < 3 : settle > 0f)) continue;
                var who = PickOutside();
                if (who == null) { timer = 8f; continue; }
                yield return Assault(who, prowler, visiting);
                timer = Mathf.Lerp(58f, 17f, TierK) * Random.Range(0.8f, 1.25f);
            }
        }

        /// <summary>One of the things you turned away - not the same one twice running if there's a choice.</summary>
        VisitorDef PickOutside()
        {
            var pool = new List<VisitorDef>(roamers);
            foreach (var q in probeQueue) if (!pool.Contains(q)) pool.Add(q);
            if (pool.Count == 0)
            {
                // Nobody turned away yet, but the night still has its things in it.
                if (Tier >= 1 && defs.ContainsKey("masks")) pool.Add(defs["masks"]);
                else return null;
            }
            if (pool.Count > 1 && lastOutside != null) pool.Remove(lastOutside);
            lastOutside = pool[Random.Range(0, pool.Count)];
            return lastOutside;
        }

        /// <summary>What it tries this time. Never the same thing twice running; the worse the night, the worse the choices.</summary>
        IEnumerator Assault(VisitorDef v, VisitorAvatar it, bool duringVisit)
        {
            int tier = Tier;
            bool frontFree = !duringVisit && CurrentVisitor == null && doorThreat == null;
            bool anyOpen = !backDoor.IsSecure;
            foreach (var win in windows) anyOpen |= win != null && !win.IsSecure;
            float[] w =
            {
                tier >= 1 ? 3f : 0.4f,                                                                  // 0 work a window latch
                tier >= 1 && backDoor.Locked && !GuardingBackDoor && !beingWorked.Contains(backDoor) ? 2.2f : 0f, // 1 slide the back door bolt
                frontFree ? (tier >= 2 ? 2.2f : 1f) : 0f,                                               // 2 fists on the front door
                frontFree && tier >= 2 && !PowerOut ? 1.4f : 0f,                                        // 3 the porch light dies; it's at the door
                frontFree && tier >= 1 ? 1.2f : 0f,                                                     // 4 fingers through the mail slot
                tier >= 1 && !duringVisit ? 1.4f : 0f,                                                  // 5 closer every time you look away
                tier >= 2 && !duringVisit ? 1f : 0f,                                                    // 6 knocking from everywhere
                0.9f,                                                                                   // 7 a face at the glass
                duringVisit ? 0f : 0.6f,                                                                // 8 round the house
                duringVisit ? 0f : 0.7f,                                                                // 9 watching from the yard
                tier >= 2 && !windowsBurstUsed && !duringVisit ? 1.1f : 0f,                             // 10 every window at once
                anyOpen ? 5f : (tier == 0 ? 1.5f : 0.4f),                                               // 11 try the handles: is anything open?
                tier >= 1 && !duringVisit && CanLure ? 2.4f : 0f,                                       // 12 talk one of your guests into opening up
                frontFree && tier >= 1 ? 1.7f : 0f,                                                     // 13 work the front door's deadbolt
            };
            if (lastAssault >= 0) w[lastAssault] *= 0.12f;
            if (lastAssault2 >= 0) w[lastAssault2] *= 0.45f;
            int pick = Pick(w);
            lastAssault2 = lastAssault;
            lastAssault = pick;

            // Two of them at once, somewhere else in the house: now and then when they're angry, often in the siege.
            float twoAtOnce = tier >= 3 ? 0.5f : tier == 2 ? 0.22f : 0f;
            if (!duringVisit && prowler2 != null && !prowler2.Visible && pick != 6 && pick != 12 && Random.value < twoAtOnce)
                StartCoroutine(SecondAssault(PickOutside() ?? v, pick));

            switch (pick)
            {
                case 0: yield return ForceWindow(v, it, PickWindowToForce()); break;
                case 1: yield return ForceBackDoor(v, it); break;
                case 2: yield return PoundOnTheDoor(v, it); break;
                case 3: yield return DarkPorch(v, it); break;
                case 4: yield return TheMailSlot(v); break;
                case 5: yield return CreepingCloser(v, it); break;
                case 6: yield return KnockStorm(); break;
                case 7: yield return FaceAtTheGlass(v); break;
                case 8: yield return CircleTheHouse(v); break;
                case 9: yield return WatchFromTheYard(v); break;
                case 10: yield return WindowsBurstOpen(v); break;
                case 12: yield return LureAGuest(v, it); break;
                case 13: yield return WorkTheFrontLock(v, it); break;
                default: yield return ProbeRoutine(v, PickEntry()); break;
            }
        }

        IEnumerator SecondAssault(VisitorDef v, int first)
        {
            yield return new WaitForSeconds(Random.Range(2f, 6f));
            if (!Running || prowler2.Visible || Dawn) yield break;
            if (first != 1 && backDoor.Locked && !GuardingBackDoor && !beingWorked.Contains(backDoor) && Random.value < 0.45f)
                yield return ForceBackDoor(v, prowler2, true);
            else
            {
                var w = PickWindowToForce();
                if (w != null) yield return ForceWindow(v, prowler2, w, true);
            }
        }

        /// <summary>A latched window nobody is working on, preferably not the one you're standing at.</summary>
        WindowEntry PickWindowToForce()
        {
            var options = new List<WindowEntry>();
            foreach (var w in windows)
                if (w != null && w.IsSecure && !beingWorked.Contains(w) && Vector3.Distance(Flat(w.SoundPosition), Flat(player.transform.position)) > 2.2f)
                    options.Add(w);
            if (options.Count == 0)
                foreach (var w in windows) if (w != null && w.IsSecure && !beingWorked.Contains(w)) options.Add(w);
            return options.Count > 0 ? options[Random.Range(0, options.Count)] : null;
        }

        // ================================================================== working the latches

        /// <summary>
        /// It comes to a window and works the latch from outside: face to the glass, palms flat on it, the lever creeping
        /// round. Shove it back and it slaps the glass and drops away. Let it give and the sash starts up - slam it shut,
        /// or it climbs in.
        /// </summary>
        IEnumerator ForceWindow(VisitorDef v, VisitorAvatar it, WindowEntry w, bool second = false)
        {
            if (w == null || !w.IsSecure)
            {
                if (!second) yield return ProbeRoutine(v, w != null ? w : PickEntry());
                yield break;
            }
            beingWorked.Add(w);
            if (!second) probing = true;
            var glow = it == prowler ? glassGlow : glassGlow2;
            it.Build(v.look.Single(), true, Plan.TellsFor(v, Mathf.RoundToInt(Minute)));
            var stand = w.outsideSpot;
            it.transform.SetPositionAndRotation(stand.position + stand.forward * 0.42f, stand.rotation);
            var eyes = EyesAtTheGlass(w);
            it.PutEyesAt(eyes);
            var glass = w.SoundPosition + w.transform.forward * 0.02f;
            it.SetPalmsOnGlass(true, glass);
            w.Rattle(1f);
            sfx.PlaySfx("knock_heavy", w.SoundPosition, 0.8f, 1.15f);   // two hands, flat on the glass
            Hint("window", "Something is working the latch of the " + w.displayName + " from outside.\nGet there and hold [E] on the latch to shove it back.");
            if (!string.IsNullOrEmpty(v.atWindow) && LineBank.Has(v.atWindow))
                StartCoroutine(Say(v.atWindow, "AT THE WINDOW", OutsideColor, w.SoundPosition, true, true, v.voicePitch, false));

            float duration = ForceTime() * (second ? 1.15f : 1f);
            int rounds = Tier >= 2 && Random.value < (Tier >= 3 ? 0.45f : 0.25f) ? 2 : 1;
            int shoves = w.Shoves;
            bool gave = false;
            w.BeginForcing();
            for (int round = 0; round < rounds && !gave && Running; round++)
            {
                float k = 0f, scrape = 0.3f;
                bool shoved = false;
                while (Running)
                {
                    float dt = Time.deltaTime;
                    k += dt / duration;
                    w.SetForce(k);
                    it.StareAt(player.cam.transform.position);
                    it.PutEyesAt(eyes, dt * 10f);
                    GlowOnTheGlass(glow, it.HeadPosition);
                    scrape -= dt;
                    if (scrape <= 0f)
                    {
                        scrape = Random.Range(0.55f, 1.05f);
                        sfx.PlaySfx("window_scratch", w.SoundPosition, 0.45f + 0.45f * k, Random.Range(0.85f, 1.15f));
                        w.Rattle(0.25f + 0.6f * k);
                    }
                    if (w.Shoves != shoves) { shoves = w.Shoves; shoved = true; break; }
                    if (k >= 1f) { gave = true; break; }
                    yield return null;
                }
                if (shoved && Running)
                {
                    // Shoved home. It slaps the glass - and at the worst of the night, it goes again, harder.
                    sfx.PlaySfx("knock_heavy", w.SoundPosition, 1f, 0.8f);
                    w.Rattle(1.5f);
                    player.Shake(0.012f, 0.2f);
                    if (round + 1 < rounds)
                    {
                        sfx.PlaySfx("creature_growl", it.HeadPosition, 0.7f, 1.1f);
                        duration *= 0.7f;
                        yield return new WaitForSeconds(0.6f);
                    }
                }
            }

            if (gave && Running)
            {
                w.PryOpen();
                Hint("latchgave", "The latch gave.\nSLAM THE WINDOW SHUT.  [E]");
                float grace = Mathf.Lerp(2.2f, 1.4f, TierK);
                for (float t = 0f; t < grace && !w.IsSecure && Running; t += Time.deltaTime)
                {
                    it.PutEyesAt(eyes, Time.deltaTime * 10f);
                    GlowOnTheGlass(glow, it.HeadPosition);
                    yield return null;
                }
                if (!w.IsSecure && Running)
                {
                    glow.enabled = false;
                    it.SetPalmsOnGlass(false, glass);
                    it.Hide();
                    beingWorked.Remove(w);
                    if (!second) probing = false;
                    yield return BreakInRoutine(v, w, "IT GOT IN.",
                        "It worked the latch of the " + w.displayName + " open from outside.\n" +
                        "When you hear scraping at a window, get there and hold [E] on the latch. If it gives, slam the window.");
                    yield break;
                }
                if (Running)
                {
                    sfx.PlaySfx("knock_heavy", w.SoundPosition, 1f, 0.75f);
                    w.Rattle(1.5f);
                    player.Shake(0.015f, 0.25f);
                }
            }

            // Driven off: it drops away from the glass and into the dark.
            if (Running)
            {
                sfx.PlaySfx("creature_scream", w.SoundPosition + w.transform.forward * 0.8f, 0.45f, Random.Range(1.05f, 1.25f));
                yield return new WaitForSeconds(0.25f);
            }
            w.EndForcing();
            glow.enabled = false;
            it.SetPalmsOnGlass(false, glass);
            it.Hide();
            for (int i = 0; i < 3 && Running; i++)
            {
                sfx.Footstep(stand.position - stand.forward * (1f + i), 0.6f, 1.1f);
                yield return new WaitForSeconds(0.3f);
            }
            beingWorked.Remove(w);
            if (!second) probing = false;
            provoked = Mathf.Min(0.3f, provoked + 0.03f);
        }

        /// <summary>
        /// It's at the back door, eyes in the gap of the blinds, and the bolt starts sliding back on its own. Shove it
        /// home. If it comes out, the door is pushed open a crack - slam it, or it's in.
        /// </summary>
        IEnumerator ForceBackDoor(VisitorDef v, VisitorAvatar it, bool second = false)
        {
            if (!backDoor.Locked || GuardingBackDoor)
            {
                if (!second) yield return ProbeRoutine(v, backDoor);
                yield break;
            }
            beingWorked.Add(backDoor);
            if (!second) probing = true;
            var glow = it == prowler ? glassGlow : glassGlow2;
            it.Build(v.look.Single(), true, Plan.TellsFor(v, Mathf.RoundToInt(Minute)) | Tells.Eyeshine);
            var stand = backDoor.outsideSpot;
            it.transform.SetPositionAndRotation(stand.position + stand.forward * 0.42f, stand.rotation);
            var eyes = EyesAtTheGlass(backDoor);
            it.PutEyesAt(eyes);
            Hint("bolt", "Something is sliding the back door's bolt open from outside.\nGet to the kitchen and hold [E] on the bolt to shove it home.");
            backDoor.Rattle(1.5f);
            sfx.PlaySfx("doorknob_rattle", backDoor.SoundPosition, 1f, 0.9f);
            if (v.knock != null && v.knock.Length > 0)
                StartCoroutine(Say(v.knock[0], "THE BACK DOOR", OutsideColor, backDoor.SoundPosition, true, true, v.voicePitch, false));

            float duration = ForceTime() * 1.2f * (second ? 1.1f : 1f);
            int shoves = backDoor.Shoves;
            bool gave = false;
            backDoor.BeginForcing();
            float k = 0f, thump = 0.5f;
            while (Running)
            {
                float dt = Time.deltaTime;
                k += dt / duration;
                backDoor.SetForce(k);
                it.StareAt(player.cam.transform.position);
                it.PutEyesAt(eyes, dt * 10f);
                GlowOnTheGlass(glow, it.HeadPosition);
                thump -= dt;
                if (thump <= 0f)
                {
                    thump = Random.Range(0.8f, 1.5f);
                    sfx.PlaySfx(Random.value < 0.5f ? "knock_firm" : "doorknob_rattle", backDoor.SoundPosition, 0.6f + 0.4f * k, Random.Range(0.85f, 1.05f));
                    backDoor.Jolt(0.4f + 0.7f * k);
                }
                if (backDoor.Shoves != shoves) break;
                if (k >= 1f) { gave = true; break; }
                yield return null;
            }

            if (gave && Running)
            {
                backDoor.EndForcing();
                backDoor.PushAjar(14f);
                Hint("boltgave", "The bolt is out and the door is opening.\nSLAM IT.  [E]");
                float grace = Mathf.Lerp(2.4f, 1.5f, TierK);
                for (float t = 0f; t < grace && !backDoor.Locked && Running; t += Time.deltaTime) yield return null;
                if (!backDoor.Locked && Running)
                {
                    glow.enabled = false;
                    it.Hide();
                    beingWorked.Remove(backDoor);
                    if (!second) probing = false;
                    yield return BreakInRoutine(v, backDoor, "IT GOT IN.",
                        "It slid the back door's bolt open from outside.\n" +
                        "When the bolt starts to move, get to the kitchen and hold [E] on it. If it gives, slam the door.");
                    yield break;
                }
            }

            if (Running)
            {
                sfx.PlaySfx("knock_heavy", backDoor.SoundPosition, 1f, 0.8f);
                backDoor.Jolt(1.6f);
                sfx.PlaySfx("creature_growl", backDoor.SoundPosition + Vector3.left * 0.6f, 0.6f, 1.1f);
                yield return new WaitForSeconds(0.3f);
            }
            backDoor.EndForcing();
            glow.enabled = false;
            it.Hide();
            beingWorked.Remove(backDoor);
            if (!second) probing = false;
            provoked = Mathf.Min(0.3f, provoked + 0.03f);
        }

        public void ShoveLatch(WindowEntry w)
        {
            if (!CanFreelyInteract || w == null) return;
            w.ShoveLatch();
            player.Shake(0.01f, 0.15f);
        }

        public void ShoveBolt(BackDoor d)
        {
            if (!CanFreelyInteract || d == null) return;
            d.ShoveBolt();
            player.Shake(0.012f, 0.18f);
        }

        public void SlamBackDoor(BackDoor d)
        {
            if (!CanFreelyInteract || d == null) return;
            d.SetLocked(true);
            sfx.PlaySfx("door_slam", d.SoundPosition, 1f);
            sfx.PlaySfx("deadbolt", d.SoundPosition, 1f);
            player.Shake(0.02f, 0.25f);
        }

        // ================================================================== at the front door

        void PlaceAtFrontDoor(VisitorAvatar it, float distance)
        {
            it.transform.SetPositionAndRotation(visitorSpot.position - visitorSpot.forward * distance, visitorSpot.rotation);
        }

        /// <summary>The first time one of them is hostile at your door, Mr. Voss warns you about the peephole.</summary>
        void WarnAboutPeephole()
        {
            if (warnedPeephole) return;
            warnedPeephole = true;
            phone.DeliverLater("Mr. Voss", "If one of them is angry at your door, get AWAY from the peephole. When you look through it, the light in it goes dark. They see that. Don't let it catch your eye.", false, 1.2f);
        }

        /// <summary>Fists on the door. You can watch through the peephole - for about a second.</summary>
        IEnumerator PoundOnTheDoor(VisitorDef v, VisitorAvatar it)
        {
            probing = true;
            it.Build(v.look.Single(), true, Plan.TellsFor(v, Mathf.RoundToInt(Minute)));
            PlaceAtFrontDoor(it, 0.45f);
            doorThreat = it;
            doorThreatDef = v;
            caughtAtPeephole = false;
            WarnAboutPeephole();
            int rounds = Random.Range(2, 4) + (Tier >= 3 ? 1 : 0);
            for (int round = 0; round < rounds && Running && !caughtAtPeephole; round++)
            {
                while (eyeContact && Running) yield return null;
                if (caughtAtPeephole || !Running) break;
                it.PlayPound(1.4f);
                yield return KnockPattern(KnockStyle.Frantic, door.SoundPosition, true);
                door.RattleHandle(1f);
                sfx.PlaySfx("doorknob_rattle", door.SoundPosition, 1f, 0.9f);
                if (round == 0 && !string.IsNullOrEmpty(v.angry))
                    yield return Say(v.angry, "OUTSIDE", AngryColor, door.SoundPosition, true, true, v.voicePitch, false);
                for (float t = Random.Range(1.5f, 3f); t > 0f && Running && !eyeContact; t -= Time.deltaTime) yield return null;
            }
            while (eyeContact && Running) yield return null;
            doorThreat = null;
            doorThreatDef = null;
            if (!Running) { probing = false; yield break; }
            it.Hide();
            probing = false;
            if (caughtAtPeephole) { caughtAtPeephole = false; yield return FollowUp(v); }
            else if (Random.value < 0.5f) yield return ProbeRoutine(v, backDoor);   // then it tries round the back
        }

        /// <summary>
        /// The porch light buzzes and dies (it took the bulb). No knock. It's right at the door in the dark, looking
        /// at the peephole. After a long time, three slow, soft knocks.
        /// </summary>
        IEnumerator DarkPorch(VisitorDef v, VisitorAvatar it)
        {
            probing = true;
            porchLight.SetMode(PorchLightMode.Flicker);
            sfx.PlaySfx("light_buzz", porchLight.transform.position, 0.8f, 0.9f);
            yield return new WaitForSeconds(1.3f);
            if (!Running) { probing = false; yield break; }
            porchLight.SetMode(PorchLightMode.Off);
            sfx.PlaySfx("glass_break", porchLight.transform.position, 0.22f, 1.7f);   // the bulb
            it.Build(v.look.Single(), true, Plan.TellsFor(v, Mathf.RoundToInt(Minute)) | Tells.Eyeshine);
            PlaceAtFrontDoor(it, 0.5f);
            doorThreat = it;
            doorThreatDef = v;
            caughtAtPeephole = false;
            WarnAboutPeephole();
            for (float t = Random.Range(6f, 10f); t > 0f && Running && !caughtAtPeephole; t -= Time.deltaTime)
            {
                if (!eyeContact) it.StareAt(peepholeAnchor.position);
                yield return null;
            }
            for (int i = 0; i < 3 && Running && !caughtAtPeephole; i++)
            {
                while (eyeContact && Running) yield return null;
                sfx.PlaySfx("knock_soft", door.SoundPosition, 0.75f, 0.8f);
                door.Bump(0.25f);
                it.PlayKnock(0.4f);
                yield return new WaitForSeconds(1.15f);
            }
            for (float t = Random.Range(8f, 14f); t > 0f && Running && !caughtAtPeephole; t -= Time.deltaTime)
            {
                if (!eyeContact) it.StareAt(peepholeAnchor.position);
                yield return null;
            }
            while (eyeContact && Running) yield return null;
            doorThreat = null;
            doorThreatDef = null;
            if (!Running) { probing = false; yield break; }
            it.Hide();
            porchLight.SetMode(PorchLightMode.Flicker);
            yield return new WaitForSeconds(0.9f);
            porchLight.SetMode(PorchLightMode.On);
            probing = false;
            if (caughtAtPeephole) { caughtAtPeephole = false; yield return FollowUp(v); }
        }

        /// <summary>It caught you looking. It knows you're home and where you stand - it goes straight for another way in.</summary>
        IEnumerator FollowUp(VisitorDef v)
        {
            yield return new WaitForSeconds(Random.Range(2f, 4f));
            if (!Running || prowler.Visible) yield break;
            if (backDoor.Locked && !GuardingBackDoor && !beingWorked.Contains(backDoor) && Random.value < 0.45f) yield return ForceBackDoor(v, prowler);
            else yield return ForceWindow(v, prowler, PickWindowToForce());
        }

        /// <summary>The flap lifts. Something whispers through it, and long fingers come in and feel about for the lock.</summary>
        IEnumerator TheMailSlot(VisitorDef v)
        {
            probing = true;
            bool slammed = false, done = false;
            StartCoroutine(mailSlot.Fingers(Mathf.Lerp(8f, 5f, TierK), r => { slammed = r; done = true; }));
            yield return new WaitForSeconds(1.2f);
            Hint("slot", "Something is reaching through the mail slot - feeling for the lock.\n[E] Slam the flap on its fingers.");
            string line = !string.IsNullOrEmpty(v.leave) && Random.value < 0.5f ? v.leave : (Random.value < 0.5f ? "SLOT_01" : "SLOT_02");
            if (LineBank.Has(line)) yield return Say(line, "THE MAIL SLOT", OutsideColor, mailSlot.SoundPosition, true, true, v.voicePitch, false);
            while (!done && Running) yield return null;
            if (slammed && Running)
            {
                sfx.PlaySfx("creature_scream", mailSlot.SoundPosition + Vector3.forward * 0.5f, 0.6f, 1.25f);
                door.Bump(1.6f);
                player.Shake(0.01f, 0.2f);
            }
            else if (Running && Tier >= 2 && Random.value < (Tier >= 3 ? 1f : 0.6f))
            {
                // Left alone long enough, the fingers find what they were feeling for.
                yield return LockGives(v, null);
                yield break;
            }
            probing = false;
        }

        // ================================================================== out in the yard

        /// <summary>The house's light reaching something standing in the yard, just enough to make it out.</summary>
        void LightInTheYard(VisitorAvatar it, Vector3 towardHouse)
        {
            if (yardGlow == null || !it.Visible) return;
            yardGlow.transform.position = it.HeadPosition + towardHouse.normalized * 1f + Vector3.up * 0.3f;
            yardGlow.intensity = PowerOut ? 0.7f : 2.1f;
            yardGlow.enabled = true;
        }

        /// <summary>How far out from a window the yard goes before a fence or the shed is in the way (metres from the outside spot).</summary>
        static float YardDepth(WindowEntry w)
        {
            if (w.displayName.Contains("bedroom")) return 2.9f;        // the back fence
            if (w.displayName.Contains("living")) return 5.2f;         // the shed
            return 8f;                                                 // the side yard toward the street
        }

        /// <summary>
        /// Something standing out in the yard, facing a window. Every time you look away, it's closer. When it reaches
        /// the glass, it starts on the latch.
        /// </summary>
        IEnumerator CreepingCloser(VisitorDef v, VisitorAvatar it)
        {
            var w = PickWindowToForce();
            if (w == null) yield break;
            probing = true;
            beingWorked.Add(w);
            it.Build(v.look.Single(), true, Plan.TellsFor(v, Mathf.RoundToInt(Minute)));
            var outward = Flat(w.transform.forward).normalized;
            var across = Vector3.Cross(Vector3.up, outward);
            var ground = w.outsideSpot.position;
            // As far out as that side of the yard goes (the back fence is close behind the bedroom; the shed east of the living room).
            float far = YardDepth(w);
            float[] distances = { far, far * 0.68f, far * 0.42f, 1.3f };
            float lateral = Random.Range(-1f, 1f) * Mathf.Min(1.3f, far * 0.3f);
            bool noticed = false;
            for (int i = 0; i < distances.Length && Running; i++)
            {
                var at = ground + outward * distances[i] + across * lateral * (1f - i / (float)distances.Length);
                it.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-outward, Vector3.up));
                float waited = 0f, seen = 0f, away = 0f;
                while (waited < 15f && Running)
                {
                    waited += Time.deltaTime;
                    it.StareAt(player.cam.transform.position);
                    LightInTheYard(it, -outward);
                    if (PlayerSees(it.HeadPosition, 24f, 24f)) { seen += Time.deltaTime; away = 0f; }
                    else if (seen > 0.3f && (away += Time.deltaTime) > 0.4f) break;   // you looked away: it moves
                    yield return null;
                }
                if (seen > 0.3f && !noticed)
                {
                    noticed = true;
                    sfx.PlaySfx2D("sting_reveal", 0.35f);
                }
                else if (seen > 0.3f && i == distances.Length - 2) sfx.PlaySfx2D("hit_bass", 0.5f);
            }
            if (yardGlow != null) yardGlow.enabled = false;
            beingWorked.Remove(w);
            probing = false;
            if (!Running) { it.Hide(); yield break; }
            yield return ForceWindow(v, it, w);   // and now it's at the glass
        }

        /// <summary>Knocking from everywhere at once - the front door, the back, every window - faster and faster. Then nothing.</summary>
        IEnumerator KnockStorm()
        {
            probing = true;
            int n = Random.Range(9, 14) + Tier * 2, last = -1;
            for (int i = 0; i < n && Running; i++)
            {
                int k;
                do k = Random.Range(0, windows.Length + 2); while (k == last);
                last = k;
                float vol = Mathf.Lerp(0.6f, 1f, i / (float)n);
                if (k == 0)
                {
                    sfx.PlaySfx(Random.value < 0.5f ? "knock_heavy" : "knock_single", door.SoundPosition, vol, Random.Range(0.9f, 1.1f));
                    door.Bump(0.9f);
                }
                else if (k == 1)
                {
                    sfx.PlaySfx("knock_firm", backDoor.SoundPosition, vol);
                    backDoor.Jolt(0.8f);
                }
                else if (windows[k - 2] != null)
                {
                    var w = windows[k - 2];
                    sfx.PlaySfx("window_tap", w.SoundPosition, vol, Random.Range(0.9f, 1.1f));
                    w.Rattle(0.8f);
                }
                yield return new WaitForSeconds(Mathf.Lerp(0.75f, 0.12f, i / (float)n) * Random.Range(0.8f, 1.2f));
            }
            yield return new WaitForSeconds(2.5f);
            probing = false;
        }

        // ================================================================== the peephole

        /// <summary>Called every frame: are you at the peephole while something hostile is right outside?</summary>
        void WatchThePeephole()
        {
            bool looking = doorThreat != null && doorThreat.Visible && IsPeeping && peepholeActive && Running && !Paused;
            if (!looking) { doorStare = 0f; return; }
            doorStare += Time.deltaTime;
            if (!eyeContact && doorStare > (caughtOnce ? 0.4f : 0.75f)) StartCoroutine(EyeContact());
        }

        /// <summary>
        /// It stops dead - it saw the light in the peephole change - and its face comes right up to the glass. Get away
        /// from the door now, or it has caught your eye.
        /// </summary>
        IEnumerator EyeContact()
        {
            eyeContact = true;
            var it = doorThreat;
            var v = doorThreatDef;
            bool first = !caughtOnce;
            caughtOnce = true;
            var lens = peepholeAnchor.position + peepholeAnchor.forward * 0.2f;   // its whole face in the peephole
            sfx.PlaySfx("breath_close", door.SoundPosition, 1f, 0.75f);
            it.StareAt(peepholeAnchor.position);
            it.GlowEyes();
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.28f)
            {
                it.PutEyesAt(lens, 0.08f + k * k * 0.6f);
                yield return null;
            }
            it.PutEyesAt(lens);
            sfx.PlaySfx2D("sting_high", 0.9f);
            sfx.PlaySfx2D("hit_bass", 0.6f);
            player.Shake(0.02f, 0.3f);
            if (v != null && !string.IsNullOrEmpty(v.peep) && LineBank.Has(v.peep))
                StartCoroutine(Say(v.peep, v.Speaker, AngryColor, door.SoundPosition, true, true, v.voicePitch, false));
            if (first) ui.SetHint("It saw the peephole go dark.\nGET AWAY FROM THE DOOR.  [E] Step back");

            float grace = first ? 1.9f : 0.9f;
            for (float t = 0f; t < grace && IsPeeping && Running; t += Time.deltaTime)
            {
                it.StareAt(peepholeAnchor.position);
                it.PutEyesAt(lens, Time.deltaTime * 12f);
                yield return null;
            }
            if (IsPeeping && Running)
            {
                yield return PeepholeDeath(it, v);
                yield break;
            }
            if (first) ui.SetHint(null);
            if (!Running) { eyeContact = false; yield break; }

            // You got away in time. It knows exactly where you were standing.
            door.Bump(3f);
            sfx.PlaySfx("knock_pound", door.SoundPosition, 1f, 0.9f);
            player.Shake(0.03f, 0.35f);
            provoked = Mathf.Min(0.3f, provoked + 0.12f);
            if (it != null && it.Visible) PlaceAtFrontDoor(it, 0.45f);
            caughtAtPeephole = true;
            doorStare = 0f;
            eyeContact = false;
        }

        /// <summary>It caught your eye. The door is open - and you never opened it.</summary>
        IEnumerator PeepholeDeath(VisitorAvatar it, VisitorDef v)
        {
            if (!BeginDeath()) yield break;
            InCutscene = true;
            Deaths++;
            ui.SetHint(null);
            ui.CancelConfirm();
            sfx.Silence(0.3f);
            sfx.SetTension(1f);
            yield return new WaitForSeconds(0.75f);
            sfx.PlaySfx("door_slam", door.SoundPosition, 1f, 0.6f);
            sfx.PlaySfx("knock_pound", door.SoundPosition, 1f, 0.7f);
            ExitPeek();
            ui.SetLetterbox(true, false);
            // The door slams inward and throws you back; it's already over the threshold.
            player.Teleport(doorStand.position, doorStand.eulerAngles.y);
            door.SetAngle(door.openAngle * 0.85f);
            sfx.SetRainOpen(true);
            player.Shake(0.05f, 0.5f);
            doorThreat = null;
            eyeContact = false;
            it.transform.SetPositionAndRotation(visitorSpot.position + visitorSpot.forward * 0.55f, visitorSpot.rotation);
            yield return Scare(it, "IT CAUGHT YOUR EYE.",
                (v != null ? v.name + " was angry, and you looked. " : "") +
                "When one of them is pounding on your door, stay off the peephole.\nIf it catches your eye, that's an invitation too.");
        }
    }
}
