using System;
using System.Collections.Generic;

namespace DontAnswer
{
    public enum KnockStyle { Human, Child, Frantic, Even, Secret }
    public enum Helper { None, Flashlight, GuardBackDoor, FixPower }
    public enum ShadowMode { Normal, None, Wrong }

    /// <summary>Physical giveaways a mimic can show. The night plan picks a few per mimic, per run.</summary>
    [Flags]
    public enum Tells
    {
        None = 0,
        NoShadow = 1,      // casts no shadow under the porch light
        WrongShadow = 2,   // casts a shadow that moves on its own (after 2 AM - "they're learning")
        NoBreath = 4,      // no breath vapor in the cold air
        EvenKnock = 8,     // knocks like a metronome
        SilentSteps = 16,  // no footsteps on the porch
        Eyeshine = 32,     // eyes catch the light like an animal's
    }

    public class QA
    {
        public string ask;
        public string answer;   // line id
        public QA(string ask, string answer) { this.ask = ask; this.answer = answer; }
    }

    /// <summary>One person (or thing) that can knock. Text lives in Lines.csv; this holds ids and rules.</summary>
    public class VisitorDef
    {
        public string id;
        public string name;
        public bool mimic;
        [NonSerialized] public bool quiet;            // a mimic that sits among your guests instead of attacking at once
        public VisitorLook look = new VisitorLook();
        public string[] knock = new string[0];
        public QA[] qa = new QA[0];
        public string goAway, leave, letIn, angry, taken, death;
        public bool getsAngry = true;
        public float patience = 80f;
        public bool urgent;
        public KnockStyle knockStyle = KnockStyle.Human;
        public Helper helper;
        public bool sitsInside = true;
        public string outcome;             // card after letting a person in
        public string endingSaved, endingLost;
        public string returnAs;            // mimic that comes back wearing them if they're turned away
        public string takenText;           // phone message after they're taken (optional)
        public string takenSender;
        public Dictionary<string, string> witness = new Dictionary<string, string>();  // guest id -> line id
        public string extraHint;           // knowledge-based hint for the death screen
        public float voicePitch = 1f;
        public string peep;                // when it catches you at the peephole (line id)
        public string atWindow;            // through the glass while it works at a latch (line id)

        public string Speaker => name.ToUpperInvariant();
    }

    /// <summary>A text message that arrives on Sam's phone.</summary>
    public class Message
    {
        public int minute;
        public string sender;
        public string text;
        public bool creepy;
        public Func<NightPlan, bool> when;   // null = always
    }

    public enum EventKind
    {
        Thunder, LightsFlicker, WindowTap, WindowFigure, Footsteps, Doorknob, Breathing,
        MailSlot, WindBlowsWindow, BedroomMemory, HomeCall, MomCall, TVBroadcast, RulesAmended,
        Siege, ChairScrape, WallScratch, Dishes, ToiletFlush, Snoring
    }

    public class NightEvent
    {
        public int minute;
        public EventKind kind;
        public bool needsQuiet = true;   // wait until nobody is at the door
    }
}
