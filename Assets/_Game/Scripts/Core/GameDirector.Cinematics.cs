using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DontAnswer
{
    public partial class GameDirector
    {
        enum FinalOutcome { None, LetInReal, LetInFake, RefusedReal, RefusedFake }
        FinalOutcome finalOutcome;

        class Checkpoint
        {
            public int scheduleIndex;
            public float minute;
            public List<(int, string)> schedule;
            public List<string> saved, lost;
            public List<int> usedSlots, firedEvents;
            public PhoneSystem.Snapshot phone;
            public bool backDoorLocked, hasFlashlight, homeCall, noteRead;
            public bool[] windowsSecure;
            public int rulesStage;
            public List<string> memories;
            public List<string> roamers;
        }

        Checkpoint checkpoint;

        // ================================================================== intro & prologue

        IEnumerator IntroRoutine(bool quick)
        {
            State = GameState.Intro;
            ui.SetHudVisible(false);
            yield return ui.Fade(1f, 0.8f);
            sfx.Silence(1f);
            if (!quick)
            {
                foreach (var c in StoryScript.IntroCards) yield return ui.ShowCard(c, 3.4f);
                if (!SkipPrologueNow) yield return PrologueRoutine();
            }

            sfx.GameAmbience();
            player.Teleport(playerStart.position, playerStart.eulerAngles.y);
            if (StartMinuteNow > -999)
            {
                noteRead = true;
                clockFrozen = false;
                Minute = StartMinuteNow;
                lastHour = HourOf(Minute);
                while (scheduleIndex < schedule.Count && schedule[scheduleIndex].minute < Minute) scheduleIndex++;
                backDoor.SetLocked(true);
            }
            yield return ui.ShowCard(StoryScript.Objective, 4.5f);
            yield return ui.ShowCard(FormatTime(Minute), 1.6f);
            State = GameState.Playing;
            ui.SetHudVisible(true);
            yield return ui.Fade(0f, 1.6f);
            if (!noteRead) ui.SetHint("Read the rules by the front door.");
            StartCoroutine(NightRoutine());
            StartHaunting();
        }

        IEnumerator PrologueRoutine()
        {
            InCutscene = true;
            DuskLighting(true);
            door.SetAngle(door.openAngle * 0.9f);
            porchLight.SetMode(PorchLightMode.On);
            var voss = new VisitorLook
            {
                modelKey = "voss", height = 1.76f, width = 1.1f, skin = new Color(0.85f, 0.66f, 0.52f), shirt = new Color(0.33f, 0.24f, 0.15f),
                pants = new Color(0.14f, 0.13f, 0.12f), hat = HatStyle.Cap, hatColor = new Color(0.28f, 0.27f, 0.26f), glasses = true
            };
            visitor.Build(voss, false, Tells.None);
            visitor.transform.SetPositionAndRotation(new Vector3(0.05f, 0f, 3.35f), Quaternion.LookRotation(Vector3.forward));
            player.Teleport(prologueView.position, prologueView.eulerAngles.y, 6f);
            ui.SetLetterbox(true, true);
            sfx.SetLoop("rain_window", 0.25f, 1f);
            yield return ui.Fade(0f, 2f);

            yield return Say("VOSS_P01", "MR. VOSS", OutsideColor, visitor.HeadPosition, false, false, 0.9f, true);
            yield return Say("VOSS_P02", "MR. VOSS", OutsideColor, visitor.HeadPosition, false, false, 0.9f, true);
            yield return Say("VOSS_P03", "MR. VOSS", OutsideColor, visitor.HeadPosition, false, false, 0.9f, true);
            visitor.PlayKnock(1.5f);
            sfx.PlaySfx("doorknob_rattle", visitor.HeadPosition, 0.3f, 1.8f);
            yield return Say("VOSS_P04", "MR. VOSS", OutsideColor, visitor.HeadPosition, false, false, 0.9f, true);
            yield return Say("VOSS_P05", "MR. VOSS", OutsideColor, visitor.HeadPosition, false, false, 0.9f, true);
            yield return Say("VOSS_P06", "MR. VOSS", OutsideColor, visitor.HeadPosition, false, false, 0.9f, true);
            visitor.Leave(new Vector3(1.8f, -0.45f, 9f), 4.5f);
            yield return new WaitForSeconds(1.5f);
            yield return Say("VOSS_P07", "MR. VOSS", OutsideColor, visitor.HeadPosition, false, false, 0.9f, true);
            yield return new WaitForSeconds(1f);
            yield return ui.Fade(1f, 1.6f);
            visitor.Hide();
            door.SetAngle(0f);
            DuskLighting(false);
            ui.SetLetterbox(false, true);
            InCutscene = false;
        }

        // ================================================================== letting people in

        IEnumerator LetInRoutine(VisitorDef v)
        {
            InCutscene = true;
            ui.SetHint(null);
            ui.SetLetterbox(true, false);
            yield return player.MoveTo(doorStand.position, doorStand.eulerAngles.y, 4f, 0.8f);
            sfx.PlaySfx("door_creak", door.SoundPosition, 0.9f);
            sfx.SetRainOpen(true);
            yield return door.Swing(door.openAngle * 0.85f, 1.4f);
            if (RoamerSeesDoor())
            {
                yield return RoamerRushIn(v);
                yield break;
            }
            yield return Say(v.letIn, v.Speaker, OutsideColor, visitor.HeadPosition, false, false, v.voicePitch, true);

            if (v.id == "leo_real")
            {
                finalOutcome = FinalOutcome.LetInReal;
                yield return TrueEndingRoutine(v);
                yield break;
            }

            yield return ui.Fade(1f, 0.8f);
            door.SetAngle(0f);
            sfx.PlaySfx("door_slam", door.SoundPosition, 0.45f);
            sfx.SetRainOpen(false);
            visitor.Hide();
            saved.Add(v.id);
            if (v.sitsInside) SpawnGuest(v);
            yield return ui.ShowCard(v.outcome, 3.8f);
            if (v.helper == Helper.Flashlight)
            {
                flashlight.HasFlashlight = true;
                ui.ShowToast("Marco's flashlight", "Press [F] to switch it on.", false);
            }
            player.Teleport(doorStand.position - doorStand.forward * 0.9f, doorStand.eulerAngles.y + 180f);
            ui.SetLetterbox(false, false);
            yield return ui.Fade(0f, 0.9f);
            InCutscene = false;
        }

        // ================================================================== being taken

        /// <summary>You opened the door to the wrong thing.</summary>
        IEnumerator DeathRoutine(VisitorDef v, EntryPoint entry)
        {
            if (!BeginDeath()) yield break;
            InCutscene = true;
            Deaths++;
            ui.SetHint(null);
            ui.CancelConfirm();
            ui.SetLetterbox(true, false);
            var tells = currentTells;
            yield return player.MoveTo(doorStand.position, doorStand.eulerAngles.y, 3f, 0.8f);
            sfx.PlaySfx("door_creak", door.SoundPosition, 1f, 0.8f);
            sfx.SetRainOpen(true);
            yield return door.Swing(door.openAngle * 0.62f, 2f);
            sfx.Silence(0.8f);
            sfx.SetTension(1f);
            yield return new WaitForSeconds(1.1f);

            if (!string.IsNullOrEmpty(v.death))
                yield return Say(v.death, v.Speaker, AngryColor, visitor.HeadPosition, false, true, v.voicePitch * 0.95f, false);
            yield return Scare(visitor, v.id == "leo_x" ? StoryScript.ReplacedTitle : "IT GOT IN.",
                v.id == "leo_x" ? StoryScript.ReplacedText : Hint(v, tells));
        }

        /// <summary>A refused mimic found an unlocked way in (or was let in by someone who isn't you).</summary>
        IEnumerator BreakInRoutine(VisitorDef v, EntryPoint entry, string title = null, string hint = null)
        {
            if (!BeginDeath()) yield break;
            InCutscene = true;
            Deaths++;
            if (IsPeeping) ExitPeek();
            if (IsTalking) EndTalk();
            if (PhoneOpen) TogglePhone();
            if (NoteOpen) CloseNote();
            if (MemoryOpen) CloseMemory();
            ui.CancelConfirm();
            ui.SetHint(null);
            yield return entry.ForceOpen();
            sfx.Silence(0.8f);
            sfx.SetTension(1f);
            var from = entry.insideSpot != null ? entry.insideSpot.position : entry.transform.position;
            for (int i = 0; i < 4; i++)
            {
                var p = Vector3.Lerp(from, player.transform.position, (i + 1) / 5f);
                sfx.Footstep(p, 1f, 0.75f);
                yield return new WaitForSeconds(0.55f);
            }
            sfx.PlaySfx("breath_close", player.transform.position - player.transform.forward * 0.6f + Vector3.up * 1.6f, 1f, 0.9f);
            yield return new WaitForSeconds(1.2f);

            // It's right behind you (or as near to it as the walls allow).
            ui.SetLetterbox(true, false);
            prowler.Build(v.look, true, Plan.TellsFor(v, Mathf.RoundToInt(Minute)));
            var (spot, turnTo) = RightBehindYou(1.1f);
            prowler.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(Flat(player.transform.position - spot).normalized, Vector3.up));
            yield return player.MoveTo(player.transform.position, turnTo, 2f, 0.9f);
            if (!string.IsNullOrEmpty(v.death))
                yield return Say(v.death, v.Speaker, AngryColor, prowler.HeadPosition, false, true, v.voicePitch * 0.95f, false);
            string where = entry is BackDoor ? "The back door wasn't bolted." : "The " + entry.displayName + " was open.";
            yield return Scare(prowler, title ?? "IT GOT IN.", hint ?? where + "\nAn open door or window is an invitation.");
        }

        Light faceLight;   // a hard light on its face, for the split second you see it

        /// <summary>
        /// The scare. Every light dies, yours too, and the screen goes black. Then nothing at all - no rain, no house,
        /// not one sound - for a second and a half. Then, for a split second, its face, right in front of yours, and one
        /// short, sharp sound. Black.
        /// </summary>
        IEnumerator Scare(VisitorAvatar it, string title, string hint)
        {
            SetLamps(false);
            porchLight.SetMode(PorchLightMode.Off);
            if (flashlight.On) flashlight.Set(false);
            ui.SetHint(null);
            ui.HideSubtitleNow();
            ui.SetHudVisible(false);
            yield return ui.Fade(1f, 0.35f);

            // Silence. In the dark, it gets ready: face to yours, head tipped just wrong, mouth open.
            sfx.HardSilence();
            it.SetBreathVisible(false);
            StartCoroutine(it.Reveal(0.05f, 16f));
            yield return new WaitForSeconds(Random.Range(1.25f, 1.7f));
            FaceToFace(it, 0.42f);
            yield return new WaitForSeconds(0.25f);

            // There.
            FaceToFace(it, 0.42f);
            LightTheFace();
            ui.SetFade(0f);
            sfx.PlaySfx2D("death_flash", 1f);
            player.Shake(0.045f, 0.25f);
            yield return new WaitForSeconds(0.16f);
            ui.SetFade(1f);
            faceLight.enabled = false;
            it.Hide();
            yield return new WaitForSeconds(1.2f);
            SetLamps(true);
            yield return DeathEpilogue(title, hint);
        }

        /// <summary>
        /// Where something can stand "right behind you": straight behind if there's room, otherwise the nearest direction
        /// round from there that isn't a wall. Returns the spot and the way you turn to face it.
        /// </summary>
        (Vector3 spot, float yaw) RightBehindYou(float distance)
        {
            var eye = player.cam.transform.position;
            var feet = player.transform.position;
            var back = Flat(-player.transform.forward).normalized;
            foreach (float a in new[] { 0f, 40f, -40f, 80f, -80f, 120f, -120f, 180f })
            {
                var dir = Quaternion.Euler(0f, a, 0f) * back;
                float free = Physics.Raycast(eye, dir, out var hit, distance + 0.4f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance - 0.4f : distance;
                if (free < 0.7f) continue;
                var p = feet + dir * Mathf.Min(distance, free);
                return (new Vector3(p.x, feet.y, p.z), Quaternion.LookRotation(dir).eulerAngles.y);
            }
            return (feet + back * 0.6f, Quaternion.LookRotation(back).eulerAngles.y);
        }

        /// <summary>Its face, right in front of yours, looking at you.</summary>
        void FaceToFace(VisitorAvatar it, float distance)
        {
            var cam = player.cam.transform;
            var back = Flat(-cam.forward);
            if (back.sqrMagnitude < 0.0001f) back = Flat(-player.transform.forward);
            it.transform.rotation = Quaternion.LookRotation(back.normalized, Vector3.up);
            it.StareAt(cam.position);
            it.transform.position += cam.position + cam.forward * distance - it.HeadPosition;
        }

        /// <summary>A hard, cold light from just under your chin, on whatever is in front of you.</summary>
        void LightTheFace()
        {
            if (faceLight == null)
            {
                var go = new GameObject("Face Light");
                go.transform.SetParent(transform, false);
                faceLight = go.AddComponent<Light>();
                faceLight.type = LightType.Point;
                faceLight.shadows = LightShadows.None;
                faceLight.range = 1.6f;
                faceLight.intensity = 1.2f;
                faceLight.color = new Color(0.78f, 0.84f, 1f);
            }
            var cam = player.cam.transform;
            faceLight.transform.position = cam.position + cam.forward * 0.1f - cam.up * 0.28f + cam.right * 0.06f;
            faceLight.enabled = true;
        }

        IEnumerator DeathEpilogue(string title, string hint)
        {
            visitor.Hide();
            prowler.Hide();
            if (prowler2 != null) prowler2.Hide();
            if (roamer != null) roamer.Hide();
            foreach (var l in new[] { glassGlow, glassGlow2, yardGlow, roamGlow, faceLight }) if (l != null) l.enabled = false;
            player.ResetState();
            sfx.HardSilence();
            yield return null;
            InCutscene = false;
            State = GameState.Dead;
            ui.SetLetterbox(false, true);
            ui.SetHudVisible(false);
            ui.ShowDeath(title, hint, Deaths, StoryScript.MaxGotIn);
        }

        string Hint(VisitorDef v, Tells tells)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(v.extraHint)) sb.Append(v.extraHint);
            string d = NightPlan.Describe(tells);
            if (d.Length > 0) sb.Append(sb.Length > 0 ? "\n" : "").Append(d);
            return sb.ToString();
        }

        void SetLamps(bool on)
        {
            foreach (var l in lamps) if (l != null) l.ForcedOff = !on || PowerOut;
        }

        // ================================================================== endings

        IEnumerator TrueEndingRoutine(VisitorDef leo)
        {
            saved.Add("leo_real");
            yield return ui.Fade(1f, 1.4f);
            door.SetAngle(0f);
            visitor.Hide();
            sfx.SetRainOpen(false);
            SpawnGuest(leo);
            yield return ui.ShowCard("Leo.", 2.4f);
            yield return Sunrise(true);
            yield return ui.Fade(0f, 2f);
            ui.SetLetterbox(true, false);
            yield return new WaitForSeconds(2f);
            yield return Say("LEOR_E1", "LEO", InsideColor, null, false, false, 1.25f, true);
            yield return new WaitForSeconds(1.5f);
            yield return KnockPattern(KnockStyle.Human, door.SoundPosition, true);
            yield return Say("VOSS_E01", "MR. VOSS", OutsideColor, door.SoundPosition, true, false, 0.9f, true);
            yield return Say("VOSS_E03", "MR. VOSS", OutsideColor, door.SoundPosition, true, false, 0.9f, true);
            yield return ui.Fade(1f, 2.5f);
            yield return ui.ShowCard(StoryScript.TrueEndingText, 6f);
            ShowEndingScreen(true, StoryScript.TrueEndingTitle, "You kept your promise. Leo came home.");
        }

        IEnumerator EndingRoutine()
        {
            CurrentVisitor = null;
            yield return Sunrise(false);
            yield return new WaitForSeconds(6f);
            InCutscene = true;
            yield return ui.Fade(1f, 2f);
            ui.SetHudVisible(false);
            ui.SetHint(null);
            bool goAway = finalOutcome == FinalOutcome.RefusedReal;
            if (!goAway)
            {
                yield return KnockPattern(KnockStyle.Human, door.SoundPosition, false);
                yield return Say("VOSS_E01", "MR. VOSS", OutsideColor, null, true, false, 0.9f, true);
                yield return Say("VOSS_E02", "MR. VOSS", OutsideColor, null, true, false, 0.9f, true);
            }
            yield return ui.ShowCard(goAway ? StoryScript.GoAwayText : StoryScript.AloneText, 6f);
            InCutscene = false;
            ShowEndingScreen(true, goAway ? StoryScript.GoAwayTitle : StoryScript.AloneTitle,
                goAway ? "It was him. It was really him, and you told him to go away."
                       : "Leo didn't come home. Let " + StoryScript.SavedForRealLeo + " of the living in, and he might.");
        }

        /// <summary>
        /// You left too many of them outside. Out on the street, the last one you turned away stops calling for you;
        /// after that, nobody living knocks again. The night ends here.
        /// </summary>
        IEnumerator LeftTooManyOutside(VisitorDef last)
        {
            if (!BeginDeath()) yield break;
            InCutscene = true;
            if (IsPeeping) ExitPeek();
            if (IsTalking) EndTalk();
            if (PhoneOpen) TogglePhone();
            if (NoteOpen) CloseNote();
            if (MemoryOpen) CloseMemory();
            ui.CancelConfirm();
            ui.SetHint(null);
            ui.SetLetterbox(true, false);
            yield return new WaitForSeconds(1.5f);
            var at = streetPoint != null ? streetPoint.position : door.SoundPosition + Vector3.forward * 12f;
            bool heardAlready = result == VisitResult.Taken;   // it already happened on your porch
            if (last != null && !heardAlready && !string.IsNullOrEmpty(last.taken))
                yield return Say(last.taken, last.Speaker, AngryColor, at, false, false, last.voicePitch, false);
            yield return ui.Fade(1f, 2.2f);
            sfx.Silence(1.5f);
            yield return ui.ShowCard("You left them outside.\nThree of them.\n\nAfter that, nobody living knocked again.", 5.5f);
            InCutscene = false;
            ShowEndingScreen(false, null, "You left " + lost.Count + " of the living outside.");
        }

        IEnumerator Sunrise(bool quiet)
        {
            sfx.PlaySfx2D("church_bell", 0.8f);
            sfx.Sunrise();
            if (!quiet) ui.SetHint("6:00 AM.\nThe sun is coming up.");
            StartCoroutine(SunriseLighting(8f));
            yield return null;
        }

        /// <summary>The last screen: whether you made it to sunrise, which ending, and what became of everyone who knocked.</summary>
        void ShowEndingScreen(bool survived, string ending, string line)
        {
            State = GameState.Ending;
            var sb = new StringBuilder();
            int people = 0, helped = 0;
            foreach (var id in new[] { "marco", "ana", Plan.officerMimic ? null : "officer_h", Plan.tomMimic ? null : "tom_h", "eli", Plan.dadMimic ? null : "dad_h" })
            {
                if (id == null || !defs.ContainsKey(id)) continue;
                var v = defs[id];
                people++;
                bool ok = saved.Contains(id);
                if (ok) helped++;
                sb.Append(ok ? "<color=#8fe39a><b>SAVED</b></color>   " : "<color=#e05a4a><b>LOST</b></color>     ");
                sb.Append("<b>").Append(v.name).Append("</b>  -  ").Append(ok ? v.endingSaved : v.endingLost).Append("\n\n");
            }
            int mem = 0;
            foreach (var m in FoundMemories()) mem++;
            string stats = "Let in: " + helped + " / " + people +
                           "        Left outside: " + lost.Count + " / " + StoryScript.MaxLeftOutside +
                           "        They got in: " + Deaths + " / " + StoryScript.MaxGotIn +
                           "        Memories of Leo: " + mem + " / " + (memories != null ? memories.Length : 0);
            ui.SetHudVisible(false);
            ui.ShowEnding(survived ? "YOU SURVIVED THE NIGHT" : "THE NIGHT IS LOST",
                survived ? "Ending: " + ending + "   -   " + line : line, sb.ToString().TrimEnd(), stats, !survived);
        }

        // ================================================================== guests

        void SpawnGuest(VisitorDef v)
        {
            if (insideSeats == null || insideSeats.Length == 0 || guests.ContainsKey(v.id)) return;
            var seat = insideSeats[guests.Count % insideSeats.Length];
            var go = new GameObject("Guest - " + v.name);
            var av = go.AddComponent<VisitorAvatar>();
            av.CopyMaterialsFrom(visitor);
            av.Build(v.look, false, Tells.None);
            if (v.helper == Helper.GuardBackDoor && backDoor != null && backDoor.insideSpot != null)
            {
                // He doesn't sit. He stands in the kitchen facing the back door, all night.
                var spot = backDoor.insideSpot.position + Vector3.right * 0.35f;
                var face = Flat(backDoor.transform.position - spot);
                av.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(face.normalized, Vector3.up));
                av.SetBreathVisible(false);
            }
            else
            {
                av.Sit(seat);
                guestSeats[v.id] = seat;
            }
            guests[v.id] = av;
        }

        void ClearGuests()
        {
            foreach (var a in guests.Values) if (a != null) Destroy(a.gameObject);
            guests.Clear();
            guestSeats.Clear();
        }

        void RebuildGuests()
        {
            ClearGuests();
            foreach (var kv in defs)
                if (!kv.Value.mimic && kv.Value.sitsInside && saved.Contains(kv.Key)) SpawnGuest(kv.Value);
        }

        // ================================================================== checkpoints & menus

        Checkpoint Capture()
        {
            var cp = new Checkpoint
            {
                scheduleIndex = scheduleIndex,
                minute = Minute,
                schedule = new List<(int, string)>(schedule),
                saved = new List<string>(saved),
                lost = new List<string>(lost),
                usedSlots = new List<int>(usedReturnSlots),
                firedEvents = new List<int>(firedEvents),
                phone = phone.Capture(),
                backDoorLocked = backDoor.Locked,
                hasFlashlight = flashlight.HasFlashlight,
                homeCall = Plan.homeCallAnswered,
                noteRead = noteRead,
                rulesStage = rulesStage,
                windowsSecure = new bool[windows.Length],
                memories = new List<string>(memoriesFound),
                roamers = roamers.ConvertAll(r => r.id),
            };
            foreach (var q in probeQueue) if (!cp.roamers.Contains(q.id)) cp.roamers.Add(q.id);   // still outside, still angry
            for (int i = 0; i < windows.Length; i++) cp.windowsSecure[i] = windows[i].IsSecure;
            return cp;
        }

        public void RetryFromCheckpoint()
        {
            if (checkpoint == null) { RestartNight(); return; }
            sfx.UIClick();
            var cp = checkpoint;
            StopNight();
            probeQueue.Clear();
            ui.HideAllMenus();
            IsPeeping = IsTalking = NoteOpen = PhoneOpen = MemoryOpen = InCutscene = false;
            player.ResetState();
            player.ExitView();
            ui.ShowPhone(false);
            ui.HideNote();
            ui.HideMemory();
            ui.HideChoices();
            ui.SetPeephole(false);
            ui.SetBlinds(false);
            ui.HideSubtitleNow();
            ui.SetHint(null);
            ui.SetLetterbox(false, true);
            SetPeepholeVolume(0f);
            visitor.Hide();
            prowler.Hide();
            door.SetAngle(0f);
            door.ResetLock();
            porchLight.SetMode(PorchLightMode.On);
            landline.StopRinging();
            mailSlot.ResetState();
            if (tv != null) tv.SetMode(TVSet.TVMode.Off);
            if (tvFeedFigure != null) tvFeedFigure.gameObject.SetActive(false);
            PowerOut = false;
            power.SetPower(true);
            if (basementLeo != null) basementLeo.Hide();
            SetLamps(true);

            scheduleIndex = cp.scheduleIndex;
            schedule.Clear();
            schedule.AddRange(cp.schedule);
            Minute = cp.minute - 2f;
            lastHour = HourOf(Minute);
            saved.Clear(); foreach (var s in cp.saved) saved.Add(s);
            lost.Clear(); foreach (var s in cp.lost) lost.Add(s);
            usedReturnSlots.Clear(); foreach (var s in cp.usedSlots) usedReturnSlots.Add(s);
            firedEvents.Clear(); foreach (var e in cp.firedEvents) firedEvents.Add(e);
            phone.Restore(cp.phone);
            backDoor.ResetState(cp.backDoorLocked);
            for (int i = 0; i < windows.Length; i++) windows[i].ResetState(cp.windowsSecure[i]);
            flashlight.HasFlashlight = cp.hasFlashlight;
            flashlight.Set(false);
            Plan.homeCallAnswered = cp.homeCall;
            noteRead = cp.noteRead;
            rulesStage = cp.rulesStage;
            memoriesFound.Clear();
            foreach (var m in cp.memories) memoriesFound.Add(m);
            ResetHaunting();
            if (cp.roamers != null) foreach (var id in cp.roamers) if (defs.ContainsKey(id)) roamers.Add(defs[id]);
            if (memories != null) foreach (var m in memories) if (m != null) m.Found = memoriesFound.Contains(m.memoryId);
            RebuildGuests();
            player.Teleport(doorStand.position - doorStand.forward * 1.2f, doorStand.eulerAngles.y);
            CurrentVisitor = null;
            result = VisitResult.None;
            finalOutcome = FinalOutcome.None;
            clockFrozen = false;
            RestoreLighting();
            checkpoint = cp;
            StartCoroutine(RetryRoutine());
        }

        IEnumerator RetryRoutine()
        {
            State = GameState.Playing;
            ui.SetHudVisible(true);
            sfx.GameAmbience();
            retrying = true;
            StartCoroutine(NightRoutine());
            StartHaunting();
            yield return ui.Fade(0f, 0.9f);
        }

        void TogglePause()
        {
            if (Paused) { ResumeGame(); return; }
            if (State != GameState.Playing) return;
            Paused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            ui.ShowPause(true);
        }

        public void ResumeGame()
        {
            if (!Paused) return;
            sfx.UIClick();
            Paused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            ui.ShowPause(false);
        }

        public void RestartNight()
        {
            autoStart = true;
            Reload();
        }

        public void MainMenu()
        {
            autoStart = false;
            Reload();
        }

        void Reload()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ================================================================== lighting moods

        void DuskLighting(bool on)
        {
            if (on)
            {
                if (sunLight != null)
                {
                    sunLight.enabled = true;
                    sunLight.intensity = 0.9f;
                    sunLight.color = new Color(1f, 0.5f, 0.3f);
                }
                RenderSettings.ambientLight = new Color(0.2f, 0.16f, 0.18f);
                RenderSettings.fogColor = new Color(0.22f, 0.14f, 0.16f);
                if (mainCamera != null) mainCamera.backgroundColor = RenderSettings.fogColor;
                NightSky.Instance?.SetMood(new Color(0.95f, 0.55f, 0.5f), 4.5f);   // the last of the light
            }
            else RestoreLighting();
        }

        IEnumerator SunriseLighting(float duration)
        {
            porchLight.SetMode(PorchLightMode.Off);
            if (sunLight != null)
            {
                sunLight.enabled = true;
                sunLight.intensity = 0f;
                sunLight.color = new Color(1f, 0.62f, 0.38f);
            }
            Color amb0 = RenderSettings.ambientLight, fog0 = RenderSettings.fogColor;
            Color bg0 = mainCamera != null ? mainCamera.backgroundColor : Color.black;
            Color ambT = new Color(0.3f, 0.25f, 0.24f), fogT = new Color(0.42f, 0.31f, 0.27f);
            float moon0 = moonLight != null ? moonLight.intensity : 0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                NightSky.Instance?.SetMood(Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.9f, 0.7f, 0.6f), t), Mathf.Lerp(1f, 8f, t));
                RenderSettings.ambientLight = Color.Lerp(amb0, ambT, t);
                RenderSettings.fogColor = Color.Lerp(fog0, fogT, t);
                if (mainCamera != null) mainCamera.backgroundColor = Color.Lerp(bg0, fogT, t);
                if (sunLight != null) sunLight.intensity = Mathf.Lerp(0f, 1.4f, t);
                if (moonLight != null) moonLight.intensity = Mathf.Lerp(moon0, 0f, t);
                yield return null;
            }
        }

        void RestoreLighting()
        {
            RenderSettings.ambientLight = ambientBase;
            RenderSettings.fogColor = fogBase;
            if (mainCamera != null) mainCamera.backgroundColor = backgroundBase;
            if (sunLight != null) sunLight.enabled = false;
            if (moonLight != null) moonLight.intensity = moonBase;
            NightSky.Instance?.ResetMood();
        }
    }
}
