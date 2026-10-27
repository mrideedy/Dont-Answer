using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DontAnswer
{
    public partial class GameDirector
    {
        enum VisitResult { None, LetIn, Left, Taken }

        VisitResult result;
        Tells currentTells;
        bool toldToGoAway, angry, staring, knocking, talkBusy, probing, witnessed, soonerBack;
        Coroutine knockRoutine, talkRoutine, probeRoutine;
        readonly HashSet<int> asked = new HashSet<int>();
        readonly Queue<VisitorDef> probeQueue = new Queue<VisitorDef>();

        // ================================================================== the night's main loop

        IEnumerator NightRoutine()
        {
            while (scheduleIndex < schedule.Count)
            {
                var item = schedule[scheduleIndex];
                float wait = retrying ? 2.5f : (!firstVisitDone ? firstVisitorDelay : minIdleSeconds);
                yield return IdleUntil(item.minute, wait);
                retrying = false;
                if (item.id == "OUTAGE")
                {
                    yield return OutageRoutine();
                    scheduleIndex++;
                    continue;
                }
                // Whatever is at the door right now has to be gone before anyone else comes up the steps.
                while (doorThreat != null || mailSlot.Reaching || (luredGuest != null && luredGuest.active)) yield return null;
                var def = ResolveVisitor(item.id);
                checkpoint = Capture();
                yield return VisitRoutine(def);
                firstVisitDone = true;
                if (State != GameState.Playing) yield break;
                if (lost.Count >= StoryScript.MaxLeftOutside)
                {
                    yield return LeftTooManyOutside(def);
                    yield break;
                }
                scheduleIndex++;
                checkpoint = Capture();   // a death between visits starts again here, not before the last visit
            }
            CurrentVisitor = null;
            yield return IdleUntil(StoryScript.Sunrise, 5f);
            yield return EndingRoutine();
        }

        VisitorDef ResolveVisitor(string id)
        {
            if (id == "leo_final") return defs[saved.Count >= StoryScript.SavedForRealLeo ? "leo_real" : "leo_x"];
            return defs[Plan.Resolve(id)];
        }

        void ScheduleReturn(VisitorDef human)
        {
            if (string.IsNullOrEmpty(human.returnAs) || !defs.ContainsKey(human.returnAs)) return;
            foreach (var slot in StoryScript.ReturnSlots)
            {
                if (slot < Minute + 25f || usedReturnSlots.Contains(slot)) continue;
                usedReturnSlots.Add(slot);
                int insertAt = schedule.Count;
                for (int i = scheduleIndex + 1; i < schedule.Count; i++)
                    if (schedule[i].minute > slot) { insertAt = i; break; }
                schedule.Insert(insertAt, (slot, human.returnAs));
                return;
            }
        }

        // ================================================================== one visit

        enum Arrival { Knock, Silent, WindowFirst }

        /// <summary>How they turn up is never quite the same: most knock; some come to a window first; some of them don't knock at all.</summary>
        Arrival PickArrival(VisitorDef v)
        {
            if (!firstVisitDone || v.urgent || v.id == "leo_real" || v.id == "leo_x" || v.id == "voss_f") return Arrival.Knock;
            bool knowsYou = v.look.modelKey == "ana" || v.look.modelKey == "dad" || v.look.modelKey == "tom" || v.id == "leo_f";
            float window = knowsYou && v.look.groupCount <= 1 ? (v.mimic ? 0.25f : 0.3f) : 0f;
            float silent = v.mimic ? 0.35f : 0f;
            float r = Random.value;
            return r < window ? Arrival.WindowFirst : r < window + silent ? Arrival.Silent : Arrival.Knock;
        }

        IEnumerator VisitRoutine(VisitorDef v)
        {
            CurrentVisitor = v;
            result = VisitResult.None;
            toldToGoAway = false;
            angry = staring = false;
            vanished = false;
            witnessed = false;
            asked.Clear();
            currentTells = Plan.TellsFor(v, Mathf.RoundToInt(Minute));
            porchLight.SetMode(Plan.LightFor(v, Mathf.RoundToInt(Minute)));

            var arrival = PickArrival(v);
            if (arrival == Arrival.WindowFirst) yield return AtTheWindowFirst(v);

            visitor.Build(v.look, v.mimic, currentTells);
            visitor.SetPanicked(v.urgent);
            Vector3 stand = visitorSpot.position - visitorSpot.forward * v.look.distanceFromDoor;
            visitor.Arrive(visitorApproach.position, stand, visitorSpot.rotation, 2.8f);
            yield return new WaitForSeconds(2.8f);
            if (!v.mimic) MaybeRoamDuringVisit();

            float patience = v.patience * Mathf.Lerp(1f, 0.78f, TierK);   // late at night nobody waits long
            // A silent one just stands there. If you don't look, you won't know until it finally knocks.
            float knockTimer = arrival == Arrival.Silent ? Random.Range(11f, 18f) : 0f;
            int knockCount = arrival == Arrival.WindowFirst ? 1 : 0;
            float clamp = Mathf.Max(Minute, NextItemMinute() - 3f);
            while (result == VisitResult.None)
            {
                float dt = Time.deltaTime;
                Minute = Mathf.Min(clamp, Minute + dt * visitMinutesPerSecond);
                bool engaged = IsTalking || talkBusy || angry || InCutscene || ui.ConfirmOpen;
                if (!engaged || v.urgent)
                {
                    patience -= dt;
                    knockTimer -= dt;
                    if (knockTimer <= 0f && !knocking && !talkBusy && !angry)
                    {
                        knockRoutine = StartCoroutine(KnockRoutine(v, knockCount++));
                        knockTimer = v.urgent ? Random.Range(6f, 9f) : Random.Range(13f, 18f);
                    }
                    if (patience <= 0f && !knocking && !talkBusy)
                        result = v.urgent && !v.mimic ? VisitResult.Taken : VisitResult.Left;
                }
                if (!angry) visitor.SetWatched(IsPeeping && peepholeActive, peepholeAnchor.position);
                yield return null;
            }

            StopKnocking();
            if (talkRoutine != null) { StopCoroutine(talkRoutine); talkRoutine = null; }
            talkBusy = false;
            angry = false;
            if (IsTalking) EndTalk();
            if (PhoneOpen) TogglePhone();
            if (NoteOpen) CloseNote();
            if (MemoryOpen) CloseMemory();
            visitor.SetTalking(false);
            visitor.SetWatched(false, peepholeAnchor.position);

            switch (result)
            {
                case VisitResult.LetIn:
                    if (IsPeeping) ExitPeek();
                    if (v.mimic) { yield return DeathRoutine(v, null); yield break; }
                    yield return LetInRoutine(v);
                    break;
                case VisitResult.Taken:
                    yield return TakenOnPorchRoutine(v);
                    break;
                default:
                    if (IsPeeping && v.mimic) ExitPeek();
                    yield return LeaveRoutine(v);
                    break;
            }

            CurrentVisitor = null;
            porchLight.SetMode(PorchLightMode.On);
            if (!firstVisitDone) ui.SetHint(null);
        }

        IEnumerator KnockRoutine(VisitorDef v, int n)
        {
            knocking = true;
            visitor.PlayKnock(1.2f);
            var style = v.knockStyle;
            if (v.mimic) style = (currentTells & Tells.EvenKnock) != 0 ? KnockStyle.Even : KnockStyle.Human;
            if (v.id == "leo_x") style = Plan.homeCallAnswered ? KnockStyle.Secret : KnockStyle.Even;
            if (v.id == "leo_real") style = KnockStyle.Secret;
            if (v.id == "masks") style = KnockStyle.Even;
            yield return KnockPattern(style, door.SoundPosition, true);

            if (n == 0 && !firstVisitDone)
                ui.SetHint("Someone is knocking.\nLook through the peephole first.");
            yield return new WaitForSeconds(0.6f);
            if (v.knock != null && v.knock.Length > 0)
                yield return Say(v.knock[n % v.knock.Length], "OUTSIDE", OutsideColor, door.SoundPosition, true, v.mimic, v.voicePitch, false);
            if (!witnessed)
            {
                witnessed = true;
                var w = FindWitness(v);
                if (w.HasValue)
                {
                    yield return new WaitForSeconds(0.7f);
                    yield return SayInside(w.Value.guestId, w.Value.lineId);
                }
            }
            knocking = false;
            knockRoutine = null;
        }

        /// <summary>The rhythm matters: people knock unevenly, they knock like clocks, and Leo has his own knock.</summary>
        IEnumerator KnockPattern(KnockStyle style, Vector3 at, bool jolt)
        {
            switch (style)
            {
                case KnockStyle.Even:
                    for (int k = 0; k < 4; k++)
                    {
                        sfx.PlaySfx("knock_single", at, 0.95f, 1f);
                        if (jolt) door.Bump(0.45f);
                        yield return new WaitForSeconds(0.3f);
                    }
                    break;
                case KnockStyle.Secret:
                    foreach (var gap in new[] { 0.16f, 0.6f, 0.6f, 0.16f, 0f })
                    {
                        sfx.PlaySfx("knock_single", at, 0.75f, Random.Range(1.03f, 1.08f));
                        if (jolt) door.Bump(0.3f);
                        if (gap > 0f) yield return new WaitForSeconds(gap);
                    }
                    break;
                case KnockStyle.Frantic:
                    int hits = Random.Range(6, 10);
                    for (int k = 0; k < hits; k++)
                    {
                        sfx.PlaySfx(Random.value < 0.4f ? "knock_heavy" : "knock_single", at, Random.Range(0.8f, 1f), Random.Range(0.9f, 1.08f));
                        if (jolt) door.Bump(0.9f);
                        yield return new WaitForSeconds(Random.Range(0.09f, 0.2f));
                    }
                    break;
                case KnockStyle.Child:
                    for (int k = 0; k < Random.Range(2, 4); k++)
                    {
                        sfx.PlaySfx("knock_soft", at, Random.Range(0.6f, 0.8f), Random.Range(1.1f, 1.2f));
                        if (jolt) door.Bump(0.2f);
                        yield return new WaitForSeconds(Random.Range(0.25f, 0.45f));
                    }
                    break;
                default:
                    int count = Random.Range(3, 5);
                    for (int k = 0; k < count; k++)
                    {
                        sfx.PlaySfx("knock_single", at, Random.Range(0.75f, 1f), Random.Range(0.93f, 1.07f));
                        if (jolt) door.Bump(0.45f);
                        yield return new WaitForSeconds(Random.Range(0.16f, 0.36f));
                    }
                    break;
            }
        }

        void StopKnocking()
        {
            if (knockRoutine != null) StopCoroutine(knockRoutine);
            knockRoutine = null;
            if (knocking) ui.HideSubtitleNow();
            knocking = false;
        }

        (string guestId, string lineId)? FindWitness(VisitorDef v)
        {
            foreach (var kv in v.witness)
                if (guests.ContainsKey(kv.Key)) return (kv.Key, kv.Value);
            return null;
        }

        IEnumerator SayInside(string guestId, string lineId)
        {
            guests.TryGetValue(guestId, out var av);
            Vector3? pos = av != null ? av.HeadPosition : (Vector3?)null;
            var who = defs.ContainsKey(guestId) ? defs[guestId] : null;
            yield return Say(lineId, (who != null ? who.Speaker : "INSIDE") + "  (inside)", InsideColor, pos, false, false, who != null ? who.voicePitch : 1f, false);
        }

        // ================================================================== the peephole

        bool peepholeActive;

        public void EnterPeephole()
        {
            if (!CanFreelyInteract) return;
            IsPeeping = true;
            peepholeActive = true;
            viewFrame = Time.frameCount;
            player.EnterView(peepholeAnchor, 100f, new Vector2(16f, 14f));
            ui.SetPeephole(true);
            ui.SetPrompt(null);
            SetPeepholeVolume(1f);
            sfx.PlaySfx("doorknob_rattle", door.SoundPosition, 0.1f, 1.7f);
            if (CurrentVisitor != null && !firstVisitDone)
                ui.SetHint("Under the porch light: is there a shadow? Is there breath?\nThen talk to them with the intercom beside the door.");
        }

        // ================================================================== the intercom

        public void UseIntercom()
        {
            if (!CanFreelyInteract) return;
            if (PowerOut)
            {
                StartCoroutine(Thought("The intercom is dead. No power."));
                return;
            }
            sfx.PlaySfx2D("intercom", 0.7f);
            if (CurrentVisitor == null || result != VisitResult.None || angry || talkBusy)
            {
                StartCoroutine(EmptyIntercom());
                return;
            }
            StopKnocking();
            IsTalking = true;
            sfx.SetStatic(true);
            ShowTalkChoices();
        }

        IEnumerator EmptyIntercom()
        {
            sfx.SetStatic(true);
            string line = angry && staring ? "(Nothing. Just breathing - slow, and very close to the microphone.)"
                : angry ? "(The screaming drowns everything out.)"
                : CurrentVisitor == null && Minute > 150f && Random.value < 0.35f ? "...Sammy... let me in..."
                : "Only static.";
            int token = ui.ShowSubtitle("INTERCOM", StaticColor, line);
            yield return new WaitForSeconds(1.8f);
            ui.HideSubtitle(token);
            sfx.SetStatic(false);
        }

        // You don't get to send someone away before you've heard them out: "Go away" only comes up after a couple of answers.
        const int QuestionsBeforeGoAway = 2;
        bool goAwayOffered;

        void ShowTalkChoices()
        {
            var v = CurrentVisitor;
            var labels = new List<string>();
            var dim = new List<bool>();
            for (int i = 0; i < v.qa.Length; i++)
            {
                labels.Add("\"" + v.qa[i].ask + "\"");
                dim.Add(asked.Contains(i));
            }
            goAwayOffered = asked.Count >= Mathf.Min(QuestionsBeforeGoAway, v.qa.Length);
            if (goAwayOffered)
            {
                labels.Add("\"Go away.\"");
                dim.Add(false);
            }
            labels.Add("Hang up");
            dim.Add(false);
            ui.ShowChoices("INTERCOM   -   13 ELM ST, APT 3", labels, dim, OnTalkChoice);
        }

        void OnTalkChoice(int index)
        {
            var v = CurrentVisitor;
            if (v == null || result != VisitResult.None) { EndTalk(); return; }
            if (index < v.qa.Length) talkRoutine = StartCoroutine(AskRoutine(v, index));
            else if (index == v.qa.Length && goAwayOffered) talkRoutine = StartCoroutine(GoAwayRoutine(v));
            else EndTalk();
        }

        IEnumerator AskRoutine(VisitorDef v, int index)
        {
            talkBusy = true;
            var q = v.qa[index];
            yield return SayYou(q.ask);
            yield return new WaitForSeconds(0.35f);
            visitor.SetTalking(true);
            yield return Say(q.answer, v.Speaker, OutsideColor, door.SoundPosition, false, v.mimic, v.voicePitch, true);
            visitor.SetTalking(false);
            asked.Add(index);
            talkBusy = false;
            talkRoutine = null;
            if (!firstVisitDone)
                ui.SetHint("Open the door for them (use the door)... or let them leave.\nCheck your phone [TAB] and the rules [N].");
            if (IsTalking && result == VisitResult.None) ShowTalkChoices();
        }

        IEnumerator GoAwayRoutine(VisitorDef v)
        {
            talkBusy = true;
            toldToGoAway = true;
            yield return SayYou("Go away. I'm not opening the door.");
            yield return new WaitForSeconds(0.3f);
            if (v.mimic && v.getsAngry)
            {
                EndTalk(false);
                talkBusy = false;
                // What it does when you turn it away is never quite the same: fists on the door, a long silent stare
                // at the peephole, or a laugh and a sprint round the side of the house.
                switch (Pick(new[] { 3f, Tier >= 1 ? 1.6f : 0.8f, 1f }))
                {
                    case 0: yield return AngryRoutine(v); break;
                    case 1: yield return SilentStare(v); break;
                    default: yield return LaughAndRun(v); break;
                }
                yield break;
            }
            visitor.SetTalking(true);
            yield return Say(v.goAway, v.Speaker, OutsideColor, door.SoundPosition, false, v.mimic, v.voicePitch, true);
            visitor.SetTalking(false);
            talkBusy = false;
            EndTalk(false);
            result = VisitResult.Left;
        }

        IEnumerator AngryRoutine(VisitorDef v)
        {
            angry = true;
            var previous = porchLight.Mode;
            porchLight.SetMode(PorchLightMode.Flicker);
            doorThreat = visitor;
            doorThreatDef = v;
            caughtAtPeephole = false;
            WarnAboutPeephole();
            visitor.SetWatched(false, peepholeAnchor.position);
            for (int round = 0; round < 3 && !caughtAtPeephole && Running; round++)
            {
                int hits = Random.Range(5, 8);
                for (int h = 0; h < hits && !eyeContact && Running; h++)
                {
                    sfx.PlaySfx("knock_heavy", door.SoundPosition, 1f, Random.Range(0.85f, 1.05f));
                    door.Bump(1.8f);
                    visitor.PlayPound(0.35f);
                    player.Shake(0.015f, 0.12f);
                    yield return new WaitForSeconds(Random.Range(0.11f, 0.2f));
                }
                while (eyeContact && Running) yield return null;
                if (caughtAtPeephole || !Running) break;
                if (round == 0)
                {
                    door.RattleHandle(1.2f);
                    sfx.PlaySfx("doorknob_rattle", door.SoundPosition, 1f);
                }
                if (round == 1 && !string.IsNullOrEmpty(v.angry))
                    yield return Say(v.angry, v.Speaker, AngryColor, door.SoundPosition, false, true, v.voicePitch * 0.9f, false);
                for (float t = Random.Range(0.5f, 1.1f); t > 0f && !eyeContact; t -= Time.deltaTime) yield return null;
            }
            while (eyeContact && Running) yield return null;
            doorThreat = null;
            doorThreatDef = null;
            if (!Running) yield break;
            if (!caughtAtPeephole) yield return new WaitForSeconds(1.8f);
            porchLight.SetMode(previous);
            angry = false;
            result = VisitResult.Left;
        }

        /// <summary>
        /// No screaming. It stops talking, steps right up to the door and stares at the peephole, perfectly still, for
        /// a long time. Then the porch light stutters, and it isn't there.
        /// </summary>
        IEnumerator SilentStare(VisitorDef v)
        {
            angry = staring = true;
            doorThreat = visitor;
            doorThreatDef = v;
            caughtAtPeephole = false;
            WarnAboutPeephole();
            Vector3 from = visitor.transform.position, to = visitorSpot.position - visitorSpot.forward * 0.36f;
            for (float k = 0f; k < 1f && !eyeContact && Running; k += Time.deltaTime / 1.4f)
            {
                visitor.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k));
                visitor.StareAt(peepholeAnchor.position);
                yield return null;
            }
            for (float t = Random.Range(9f, 14f); t > 0f && !caughtAtPeephole && Running; t -= Time.deltaTime)
            {
                if (!eyeContact) visitor.StareAt(peepholeAnchor.position);
                yield return null;
            }
            while (eyeContact && Running) yield return null;
            doorThreat = null;
            doorThreatDef = null;
            if (!Running) yield break;
            porchLight.SetMode(PorchLightMode.Off);
            sfx.PlaySfx("light_buzz", porchLight.transform.position, 0.7f, 0.8f);
            yield return new WaitForSeconds(0.55f);
            visitor.Hide();
            vanished = true;
            porchLight.SetMode(PorchLightMode.Flicker);
            yield return new WaitForSeconds(0.6f);
            porchLight.SetMode(PorchLightMode.On);
            angry = staring = false;
            result = VisitResult.Left;
        }

        /// <summary>It laughs - wrong, and too long - and runs. Off the porch and round the side of the house. It'll be back sooner.</summary>
        IEnumerator LaughAndRun(VisitorDef v)
        {
            angry = true;
            bool small = v.look.groupCount > 1 || v.id.StartsWith("leo");
            sfx.PlaySfx("child_giggle", door.SoundPosition + Vector3.forward * 0.6f, 0.9f, small ? 1f : 0.62f);
            yield return new WaitForSeconds(1.3f);
            vanished = true;
            soonerBack = true;
            if (visitorApproach != null) yield return visitor.RunTo(visitorApproach.position, 5.5f);
            visitor.Hide();
            for (int i = 0; i < 6 && Running; i++)
            {
                sfx.Footstep(CircleRoute[Mathf.Min(i, CircleRoute.Length - 1)], 0.7f, 1.15f);
                yield return new WaitForSeconds(0.3f);
            }
            angry = false;
            result = VisitResult.Left;
        }

        /// <summary>
        /// Someone who knows you comes to the living room window first: a tap on the glass, your name, a face peering in.
        /// Then round the side of the house to the door.
        /// </summary>
        IEnumerator AtTheWindowFirst(VisitorDef v)
        {
            var w = windows != null && windows.Length > 0 ? windows[0] : null;
            var it = prowler2;
            if (w == null || it == null || it.Visible) yield break;
            it.Build(v.look.Single(), v.mimic, currentTells);
            var stand = w.outsideSpot;
            it.transform.SetPositionAndRotation(stand.position + stand.forward * 0.3f, stand.rotation);
            var eyes = EyesAtTheGlass(w) + w.transform.forward * 0.16f;   // peering in, not pressed to it
            it.PutEyesAt(eyes);
            for (int i = 0; i < 3; i++)
            {
                sfx.PlaySfx("window_tap", w.SoundPosition, 0.9f, 1.05f);
                w.Rattle(0.5f);
                yield return new WaitForSeconds(0.38f);
            }
            if (v.knock != null && v.knock.Length > 0)
                StartCoroutine(Say(v.knock[0], "AT THE WINDOW", OutsideColor, w.SoundPosition, true, v.mimic, v.voicePitch, false));
            for (float t = 0f; t < 3.4f && Running; t += Time.deltaTime)
            {
                it.StareAt(player.cam.transform.position);
                it.PutEyesAt(eyes, Time.deltaTime * 8f);
                GlowOnTheGlass(glassGlow2, it.HeadPosition);
                yield return null;
            }
            if (glassGlow2 != null) glassGlow2.enabled = false;
            it.Hide();
            for (int i = 0; i < 4 && Running; i++)
            {
                sfx.Footstep(Vector3.Lerp(stand.position, visitorApproach.position, i / 3f), 0.6f, 0.95f);
                yield return new WaitForSeconds(0.5f);
            }
        }

        void EndTalk(bool stopRoutine = true)
        {
            if (stopRoutine && talkRoutine != null)
            {
                StopCoroutine(talkRoutine);
                talkRoutine = null;
                talkBusy = false;
                ui.HideSubtitleNow();
                visitor.SetTalking(false);
            }
            if (!IsTalking) return;
            IsTalking = false;
            ui.HideChoices();
            sfx.SetStatic(false);
            sfx.PlaySfx2D("intercom", 0.45f, 0.9f);
        }

        // ================================================================== the door

        public void RequestOpenDoor()
        {
            if (!CanFreelyInteract) return;
            if (CurrentVisitor == null || result != VisitResult.None)
            {
                StartCoroutine(Thought("You're not opening that door tonight. Not for nothing."));
                return;
            }
            ui.ShowConfirm("Open the door?", "Open it", "Don't", yes =>
            {
                if (yes && CurrentVisitor != null && result == VisitResult.None && State == GameState.Playing)
                    result = VisitResult.LetIn;
            });
        }

        // ================================================================== the people you leave outside

        IEnumerator LeaveRoutine(VisitorDef v)
        {
            if (!vanished)
            {
                if (!toldToGoAway && !string.IsNullOrEmpty(v.leave))
                    yield return Say(v.leave, "OUTSIDE", OutsideColor, door.SoundPosition, true, v.mimic, v.voicePitch, false);
                visitor.Leave(visitorExit != null ? visitorExit.position : visitorApproach.position, v.mimic ? 4.5f : 3f);
                for (float waited = 0f; visitor.Visible && waited < 14f; waited += Time.deltaTime) yield return null;
            }
            visitor.Hide();
            vanished = false;

            if (v.id == "leo_real") { finalOutcome = FinalOutcome.RefusedReal; yield break; }
            if (v.id == "leo_x") { finalOutcome = FinalOutcome.RefusedFake; yield break; }

            if (!v.mimic)
            {
                LeftOutside(v);
                ScheduleReturn(v);
                if (lost.Count < StoryScript.MaxLeftOutside) StartCoroutine(TakenLater(v));
            }
            else if (caughtAtPeephole)
            {
                // It caught you looking. It doesn't go round the house; it goes straight for a way in.
                caughtAtPeephole = false;
                if (!roamers.Contains(v)) roamers.Add(v);
                StartCoroutine(FollowUp(v));
            }
            else
            {
                // It doesn't leave. It goes around the house.
                probeQueue.Enqueue(v);
                if (probeRoutine == null) probeRoutine = StartCoroutine(ProbeLoop());
            }
        }

        /// <summary>One of the living, left outside. You're told straight away: it counts against the night.</summary>
        void LeftOutside(VisitorDef v)
        {
            if (!lost.Add(v.id)) return;
            int n = lost.Count, max = StoryScript.MaxLeftOutside;
            if (n >= max) return;   // that was the last one: the night is lost, and the ending says so
            ui.ShowToast("LEFT OUTSIDE  " + n + " / " + max,
                v.name + " was one of the living.\n" + (max - n == 1 ? "One more and the night is lost." : (max - n) + " more and the night is lost."), true);
        }

        IEnumerator TakenLater(VisitorDef v)
        {
            yield return new WaitForSeconds(Random.Range(18f, 30f));
            while (InCutscene) yield return null;
            var at = streetPoint != null ? streetPoint.position : door.SoundPosition + Vector3.forward * 12f;
            if (!string.IsNullOrEmpty(v.taken))
                yield return Say(v.taken, v.Speaker, AngryColor, at, false, false, v.voicePitch, false);
            sfx.PlaySfx("creature_scream", at, 0.45f, 0.8f);
            yield return new WaitForSeconds(1.2f);
            sfx.PlaySfx("dog_distant", at + Vector3.right * 10f, 0.6f);
            if (!string.IsNullOrEmpty(v.takenText)) phone.DeliverLater(v.takenSender, v.takenText, v.takenSender == "Ana", 20f);
        }

        /// <summary>An urgent visitor you didn't let in fast enough. It happens right outside your door.</summary>
        IEnumerator TakenOnPorchRoutine(VisitorDef v)
        {
            ScheduleReturn(v);
            bool watching = IsPeeping && peepholeActive;
            porchLight.SetMode(PorchLightMode.Flicker);
            yield return new WaitForSeconds(0.6f);
            porchLight.SetMode(PorchLightMode.Off);
            sfx.PlaySfx("creature_click", door.SoundPosition + Vector3.forward * 1.5f, 1f);
            yield return new WaitForSeconds(0.9f);
            if (!string.IsNullOrEmpty(v.taken))
                StartCoroutine(Say(v.taken, v.Speaker, AngryColor, door.SoundPosition, false, false, v.voicePitch, false));
            porchLight.SetMode(PorchLightMode.Flicker);
            door.Bump(2.5f);
            sfx.PlaySfx("knock_pound", door.SoundPosition, 1f);
            if (watching) player.Shake(0.02f, 0.4f);
            yield return visitor.DraggedAway(visitorSpot.forward * -1f + Vector3.right * 0.3f, 6f, 1.3f);
            sfx.PlaySfx("drag", door.SoundPosition + Vector3.forward * 2f, 0.9f);
            sfx.PlaySfx("window_scratch", door.SoundPosition, 0.8f, 0.7f);
            yield return new WaitForSeconds(2.5f);
            porchLight.SetMode(PorchLightMode.Off);
            yield return new WaitForSeconds(2f);
            if (IsPeeping) ExitPeek();
            LeftOutside(v);
            yield return Thought("...", 1.5f);
        }

        // ================================================================== mimics trying other ways in

        IEnumerator ProbeLoop()
        {
            while (probeQueue.Count > 0)
            {
                var v = probeQueue.Dequeue();
                yield return new WaitForSeconds(soonerBack ? Random.Range(5f, 9f) : Random.Range(14f, 26f));
                soonerBack = false;
                // One thing at a time (unless the night is far gone: then it won't wait for you to finish at the door).
                while (InCutscene || (PowerOut && InBasement) || probing || prowler.Visible || (CurrentVisitor != null && Tier < 2))
                    yield return null;
                if (State != GameState.Playing) break;
                if (Dawn)
                {
                    if (!roamers.Contains(v)) roamers.Add(v);
                    continue;
                }
                yield return RefusedRoutine(v);
                if (State != GameState.Playing) break;
            }
            probeRoutine = null;
        }

        EntryPoint PickEntry()
        {
            // Unsecured ways in are found first. Otherwise: usually the back door.
            if (!backDoor.IsSecure) return backDoor;
            foreach (var w in windows) if (!w.IsSecure) return w;
            if (Random.value < 0.6f) return backDoor;
            return windows[Random.Range(0, windows.Length)];
        }

        IEnumerator ProbeRoutine(VisitorDef v, EntryPoint entry)
        {
            probing = true;
            var look = v.look;
            prowler.Build(look, true, Plan.TellsFor(v, Mathf.RoundToInt(Minute)));
            if (entry.outsideSpot != null)
                prowler.transform.SetPositionAndRotation(entry.outsideSpot.position, entry.outsideSpot.rotation);
            yield return entry.Probe(v);
            if (v.knock != null && v.knock.Length > 0)
                yield return Say(v.knock[0], "OUTSIDE", OutsideColor, entry.SoundPosition, true, true, v.voicePitch, false);
            yield return new WaitForSeconds(1.5f);
            if (!entry.IsSecure && State == GameState.Playing && !InCutscene)
            {
                probing = false;
                yield return BreakInRoutine(v, entry);
                yield break;
            }
            prowler.Hide();
            probing = false;
        }
    }
}
