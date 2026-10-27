using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>
    /// Two more ways in. Nothing comes in unless someone lets it in - and your guests count: something at the back door
    /// cries like a lost child (for your father, it's Leo), and one of the people you saved gets up to open it. Stop them
    /// before they slide the bolt. And the front door's deadbolt can be worked from outside: you watch the thumb-turn
    /// creep round, and you have to hold it.
    /// </summary>
    public partial class GameDirector
    {
        readonly Dictionary<string, Transform> guestSeats = new Dictionary<string, Transform>();
        GuestInteractable luredGuest;
        bool guestStopped;

        // Who can be talked into it: what they say as they go, and what they say when you stop them.
        static readonly Dictionary<string, (string go, string stopped)> LureLines = new Dictionary<string, (string, string)>
        {
            ["ana"] = ("ANA_INL1", "ANA_INS1"),
            ["tom_h"] = ("TOMH_INL1", "TOMH_INS1"),
            ["eli"] = ("ELI_INL1", "ELI_INS1"),
            ["dad_h"] = ("DADH_INL1", "DADH_INS1"),
        };

        bool CanLure => !GuardingBackDoor && backDoor.Locked && !beingWorked.Contains(backDoor) && PickLurable() != null;

        string PickLurable()
        {
            var ids = new List<string>();
            foreach (var kv in guests)
                if (kv.Value != null && kv.Value.IsSitting && LureLines.ContainsKey(kv.Key) && LineBank.Has(LureLines[kv.Key].go)) ids.Add(kv.Key);
            return ids.Count > 0 ? ids[Random.Range(0, ids.Count)] : null;
        }

        public void StopGuest(GuestInteractable gi)
        {
            if (!CanFreelyInteract || gi == null || !gi.active) return;
            gi.active = false;
            guestStopped = true;
            sfx.PlaySfx("sheet_rustle", gi.transform.position + Vector3.up, 0.6f, 0.9f);
            player.Shake(0.008f, 0.15f);
        }

        // ================================================================== a guest lets it in

        IEnumerator LureAGuest(VisitorDef v, VisitorAvatar it)
        {
            string id = PickLurable();
            if (id == null || !CanLure) { yield return ForceBackDoor(v, it); yield break; }
            var guest = guests[id];
            var who = defs[id];
            probing = true;
            beingWorked.Add(backDoor);

            // At the back door, eyes in the gap of the blinds, something knocks softly and cries to be let in.
            it.Build(v.look.Single(), true, Plan.TellsFor(v, Mathf.RoundToInt(Minute)) | Tells.Eyeshine);
            var stand = backDoor.outsideSpot;
            it.transform.SetPositionAndRotation(stand.position + stand.forward * 0.42f, stand.rotation);
            var eyes = EyesAtTheGlass(backDoor);
            it.PutEyesAt(eyes);
            for (int i = 0; i < 3 && Running; i++)
            {
                sfx.PlaySfx("knock_soft", backDoor.SoundPosition, 0.8f, 1.15f);
                backDoor.Jolt(0.3f);
                yield return new WaitForSeconds(0.35f);
            }
            string cry = id == "dad_h" ? "LUREKID_K3" : Random.value < 0.5f ? "LUREKID_K1" : "LUREKID_K2";
            if (Running && LineBank.Has(cry)) yield return Say(cry, "THE BACK DOOR", OutsideColor, backDoor.SoundPosition, true, false, 1.25f, false);
            if (Running && guest != null) yield return SayInside(id, LureLines[id].go);
            if (!Running || guest == null || !guest.IsSitting) { EndLure(it); yield break; }

            // Up off the couch and across the house to the back door.
            Hint("lure", who.name + " is going to open the back door for it!\nGet to " + who.name + " and stop them.  [E]");
            guestSeats.TryGetValue(id, out var seat);
            var front = Flat(guest.transform.position) + Flat(guest.transform.forward).normalized * 0.45f;
            guest.StandUp(front);
            luredGuest = GuestInteractable.Attach(guest, who.name);
            luredGuest.active = true;
            guestStopped = false;
            guest.StartCoroutine(guest.WalkPath(PathToBackDoor(front), 1.15f));
            while (guest.IsWalking && !guestStopped && Running)
            {
                it.StareAt(player.cam.transform.position);
                it.PutEyesAt(eyes, Time.deltaTime * 10f);
                GlowOnTheGlass(glassGlow, it.HeadPosition);
                yield return null;
            }
            luredGuest.active = false;
            if (!Running)
            {
                // Something else took over (a call, a death): don't leave them standing in the kitchen.
                guest.Halt();
                if (seat != null) guest.Sit(seat);
                EndLure(it);
                yield break;
            }

            if (guestStopped)
            {
                // You caught their arm in time. They come back to themselves; it doesn't take that well.
                guest.Halt();
                var toYou = Flat(player.transform.position - guest.transform.position);
                if (toYou.sqrMagnitude > 0.01f) guest.transform.rotation = Quaternion.LookRotation(toYou.normalized, Vector3.up);
                sfx.PlaySfx("knock_heavy", backDoor.SoundPosition, 1f, 0.75f);
                backDoor.Jolt(1.4f);
                sfx.PlaySfx("creature_scream", backDoor.SoundPosition + Vector3.left * 0.8f, 0.5f, 1.1f);
                EndLure(it);
                yield return SayInside(id, LureLines[id].stopped);
                if (seat != null && guest != null && Running)
                {
                    var seatFront = Flat(seat.position) + Flat(seat.forward).normalized * 0.45f;
                    yield return guest.WalkPath(PathBack(guest.transform.position, seatFront), 1f);
                    if (guest != null) guest.Sit(seat);
                }
                yield break;
            }

            // They got there. They slide the bolt back and open the door to it.
            var toDoor = Flat(backDoor.transform.position - guest.transform.position);
            if (toDoor.sqrMagnitude > 0.01f) guest.transform.rotation = Quaternion.LookRotation(toDoor.normalized, Vector3.up);
            guest.PlayKnock(0.6f);
            if (backDoor.Locked)
            {
                backDoor.SetLocked(false);
                sfx.PlaySfx("deadbolt", backDoor.SoundPosition, 1f, 0.85f);
            }
            yield return new WaitForSeconds(0.6f);
            EndLure(it);
            if (!Running) yield break;
            // The door swings open and whoever opened it is gone through it, into the dark.
            guests.Remove(id);
            guestSeats.Remove(id);
            StartCoroutine(TakenThroughTheDoor(guest, -stand.forward));
            yield return BreakInRoutine(v, backDoor, "SOMEONE LET IT IN.",
                who.name + " opened the back door. Something out there cried like a child, and they couldn't bear it.\n" +
                "Your guests can be fooled too. When one of them gets up to open a door, stop them.  [E]");
        }

        IEnumerator TakenThroughTheDoor(VisitorAvatar guest, Vector3 outward)
        {
            yield return new WaitForSeconds(0.9f);   // the door is half open
            sfx.PlaySfx("creature_scream", backDoor.SoundPosition, 0.9f, 0.9f);
            yield return guest.DraggedAway(outward, 3f, 0.9f);
            Destroy(guest.gameObject);
        }

        void EndLure(VisitorAvatar it)
        {
            if (glassGlow != null) glassGlow.enabled = false;
            if (it != null) it.Hide();
            beingWorked.Remove(backDoor);
            probing = false;
        }

        /// <summary>From wherever they're standing to the back door, round the kitchen table rather than through it.</summary>
        List<Vector3> PathToBackDoor(Vector3 from)
        {
            var p = new List<Vector3>();
            if (from.x > -3.1f)
            {
                p.Add(new Vector3(Mathf.Clamp(from.x, -2.4f, 1.9f), 0f, -0.5f));
                p.Add(new Vector3(-2.6f, 0f, -0.5f));
                p.Add(new Vector3(-3.6f, 0f, -0.5f));
            }
            p.Add(new Vector3(-5.35f, 0f, -0.65f));
            var inside = backDoor.insideSpot != null ? backDoor.insideSpot.position : backDoor.transform.position + Vector3.right * 0.6f;
            p.Add(new Vector3(inside.x - 0.15f, 0f, inside.z));
            return p;
        }

        List<Vector3> PathBack(Vector3 from, Vector3 seatFront)
        {
            var p = new List<Vector3>();
            if (from.x < -3.1f)
            {
                if (from.x < -4.6f) p.Add(new Vector3(-5.35f, 0f, -0.65f));
                p.Add(new Vector3(-3.6f, 0f, -0.5f));
                p.Add(new Vector3(-2.6f, 0f, -0.5f));
            }
            p.Add(new Vector3(seatFront.x, 0f, seatFront.z));
            return p;
        }

        // ================================================================== the front door's deadbolt

        public void ShoveDeadbolt()
        {
            if (!CanFreelyInteract) return;
            door.ShoveBolt();
            player.Shake(0.01f, 0.15f);
        }

        public void SlamFrontDoor()
        {
            if (!CanFreelyInteract || !door.Unbolted) return;
            door.SetAngle(0f);
            door.Rebolt();
            sfx.PlaySfx("door_slam", door.SoundPosition, 1f);
            sfx.PlaySfx("deadbolt", door.SoundPosition, 1f);
            sfx.SetRainOpen(false);
            player.Shake(0.02f, 0.25f);
        }

        /// <summary>
        /// It's at the front door, working the deadbolt from outside; on the inside you can watch the thumb-turn creep
        /// round. Hold it. It's right there and it's angry: stay off the peephole.
        /// </summary>
        IEnumerator WorkTheFrontLock(VisitorDef v, VisitorAvatar it)
        {
            probing = true;
            it.Build(v.look.Single(), true, Plan.TellsFor(v, Mathf.RoundToInt(Minute)));
            PlaceAtFrontDoor(it, 0.4f);
            doorThreat = it;
            doorThreatDef = v;
            caughtAtPeephole = false;
            WarnAboutPeephole();
            Hint("deadbolt", "The front door's deadbolt is turning by itself.\nGet there and hold [E] on it. Stay off the peephole.");
            sfx.PlaySfx("doorknob_rattle", door.SoundPosition, 0.5f, 1.3f);
            door.RattleHandle(0.8f);
            float duration = ForceTime() * 1.15f;
            int shoves = door.Shoves;
            bool gave = false;
            door.BeginForcing();
            float k = 0f, scrape = 0.4f;
            while (Running && !caughtAtPeephole)
            {
                if (eyeContact) { yield return null; continue; }   // it stopped to look back at you
                float dt = Time.deltaTime;
                k += dt / duration;
                door.SetForce(k);
                scrape -= dt;
                if (scrape <= 0f)
                {
                    scrape = Random.Range(0.5f, 1f);
                    sfx.PlaySfx("window_scratch", door.SoundPosition + Vector3.down * 0.2f, 0.3f + 0.4f * k, Random.Range(1.45f, 1.75f));
                    door.Bump(0.12f + 0.3f * k);
                }
                if (door.Shoves != shoves) break;
                if (k >= 1f) { gave = true; break; }
                yield return null;
            }
            door.EndForcing();
            while (eyeContact && Running) yield return null;
            if (gave && Running)
            {
                yield return LockGives(v, it);
                yield break;
            }
            doorThreat = null;
            doorThreatDef = null;
            if (!Running) { probing = false; yield break; }
            // Held. One blow on the door, and it's gone from the step.
            door.Bump(2.5f);
            sfx.PlaySfx("knock_pound", door.SoundPosition, 1f, 0.85f);
            player.Shake(0.015f, 0.2f);
            it.Hide();
            probing = false;
            provoked = Mathf.Min(0.3f, provoked + 0.03f);
            if (caughtAtPeephole) { caughtAtPeephole = false; yield return FollowUp(v); }
        }

        /// <summary>The deadbolt is back and the door is being pushed in, slowly. Slam it - or it's open, and open is an invitation.</summary>
        IEnumerator LockGives(VisitorDef v, VisitorAvatar it)
        {
            door.Unbolt();
            Hint("deadboltgave", "The deadbolt is open and the door is moving.\nSLAM IT.  [E]");
            float grace = Mathf.Lerp(2.3f, 1.4f, TierK);
            sfx.PlaySfx("door_creak", door.SoundPosition, 0.8f, 0.6f);
            for (float t = 0f; t < grace && door.Unbolted && Running; t += Time.deltaTime)
            {
                door.SetAngle(Mathf.Lerp(0f, 11f, t / grace));
                yield return null;
            }
            if (!door.Unbolted)
            {
                // Slammed in its face.
                door.Bump(3f);
                sfx.PlaySfx("knock_pound", door.SoundPosition, 1f, 0.8f);
                sfx.PlaySfx("creature_scream", door.SoundPosition + Vector3.forward * 0.6f, 0.5f, 1.15f);
                doorThreat = null;
                doorThreatDef = null;
                if (it != null) it.Hide();
                probing = false;
                yield break;
            }
            doorThreat = null;
            doorThreatDef = null;
            if (!Running) { probing = false; yield break; }
            probing = false;
            yield return FrontDoorBreach(v, it);
        }

        /// <summary>You never opened it. But it's open.</summary>
        IEnumerator FrontDoorBreach(VisitorDef v, VisitorAvatar it)
        {
            if (!BeginDeath()) yield break;
            bool viaSlot = it == null;   // nothing at the door: it was the fingers in the mail slot
            InCutscene = true;
            Deaths++;
            if (IsPeeping) ExitPeek();
            if (IsTalking) EndTalk();
            if (PhoneOpen) TogglePhone();
            if (NoteOpen) CloseNote();
            if (MemoryOpen) CloseMemory();
            ui.CancelConfirm();
            ui.SetHint(null);
            sfx.Silence(0.5f);
            sfx.SetTension(1f);
            sfx.SetRainOpen(true);
            sfx.PlaySfx("door_creak", door.SoundPosition, 1f, 0.7f);
            yield return door.Swing(door.openAngle * 0.8f, 1.1f);
            if (it == null || !it.Visible)
            {
                it = prowler;
                it.Build(v.look.Single(), true, Plan.TellsFor(v, Mathf.RoundToInt(Minute)));
            }
            it.transform.SetPositionAndRotation(visitorSpot.position + visitorSpot.forward * 0.55f, visitorSpot.rotation);
            ui.SetLetterbox(true, false);
            var to = Flat(it.HeadPosition - player.transform.position);
            if (to.sqrMagnitude > 0.01f) yield return player.MoveTo(player.transform.position, Quaternion.LookRotation(to.normalized).eulerAngles.y, 0f, 0.6f);
            if (!string.IsNullOrEmpty(v.death))
                yield return Say(v.death, v.Speaker, AngryColor, it.HeadPosition, false, true, v.voicePitch * 0.95f, false);
            yield return Scare(it, "THE DEADBOLT TURNED.", viaSlot
                ? "The fingers through the mail slot found the deadbolt and turned it, and the door swung open. Open is an invitation.\n" +
                  "When something reaches through the mail slot, slam the flap on it.  [E]"
                : "It worked the front door's lock from outside, and the door swung open. Open is an invitation.\n" +
                  "When the deadbolt starts to turn, get there and hold it.  [E]");
        }
    }
}
