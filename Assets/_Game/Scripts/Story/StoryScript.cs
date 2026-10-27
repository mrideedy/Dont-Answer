using System.Collections.Generic;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>
    /// The whole night of "Don't Answer": who knocks, when, what they say (line ids from Lines.csv),
    /// what gives them away, and what happens to the people you leave outside.
    /// </summary>
    public static class StoryScript
    {
        public const int NightStart = -20;     // 11:40 PM
        public const int Sunrise = 360;        // 6:00 AM
        public const int OutageMinute = 183;   // 3:03 AM
        public const int SavedForRealLeo = 3;  // people you must save for the real Leo to come home

        // The two ways to lose the night.
        public const int MaxLeftOutside = 3;   // leave this many of the living outside...
        public const int MaxGotIn = 5;         // ...or let them in this many times.

        public const string Objective =
            "SURVIVE UNTIL 6:00 AM\n\n<size=34>Leave 3 of the living outside, and the night is lost.\n" +
            "Let them in 5 times, and the night is lost.</size>";

        public static readonly int[] ReturnSlots = { 160, 232, 268, 300, 322, 338 };

        public const string RulesText =
            "13 ELM STREET  -  RULES FOR HALLOWEEN NIGHT\n\n" +
            "1.  Nothing comes in unless you let it in. An open door or window IS an invitation.\n\n" +
            "2.  They wear the faces of people you know. Never perfectly.\n\n" +
            "3.  Under the porch light, they cast no shadow.\n\n" +
            "4.  The living breathe smoke in the cold. They don't breathe.\n\n" +
            "5.  Ask what only the real one would know.\n\n" +
            "6.  Never answer a call from your own number.\n\n" +
            "7.  I will never knock before sunrise.\n\n" +
            "8.  When one of them is ANGRY, stay off the peephole. If it catches your eye, that's an invitation too.\n\n" +
            "Lock the back door. Latch every window.\nAnd don't let fear make you cruel. Real people are out there too.\n\n" +
            "                                                  - R. Voss";

        public const string RulesAmendment =
            "\n\n<color=#7a1a12>2 AM - THEY'RE LEARNING. Rule 3 isn't safe anymore.\nWatch the SHADOW, not the face. If it moves on its own, it isn't a person.   - V.</color>";

        public const string RulesAmendment2 =
            "\n<color=#7a1a12>4 AM - They breathe now too. Rule 4 is gone. ASK THEM THINGS.   - V.</color>";

        static Color C(float r, float g, float b) => new Color(r, g, b);

        public static Dictionary<string, VisitorDef> BuildVisitors()
        {
            var d = new Dictionary<string, VisitorDef>();
            void Add(VisitorDef v) => d[v.id] = v;

            var skinA = C(0.85f, 0.66f, 0.52f);
            var skinB = C(0.62f, 0.44f, 0.32f);
            var skinC = C(0.42f, 0.29f, 0.21f);

            VisitorLook Marco() => new VisitorLook
            {
                modelKey = "marco", height = 1.78f, skin = skinB, shirt = C(0.62f, 0.08f, 0.07f), pants = C(0.16f, 0.17f, 0.22f),
                hat = HatStyle.Cap, hatColor = C(0.7f, 0.09f, 0.08f), pizzaBox = true, idle = IdleStyle.Nervous
            };
            VisitorLook Ana() => new VisitorLook
            {
                modelKey = "ana", height = 1.66f, width = 0.92f, skin = skinA, shirt = C(0.95f, 0.78f, 0.12f), pants = C(0.18f, 0.2f, 0.3f),
                hat = HatStyle.Hood, hatColor = C(0.95f, 0.78f, 0.12f), coat = true, idle = IdleStyle.Shiver
            };
            VisitorLook Officer() => new VisitorLook
            {
                modelKey = "officer", height = 1.88f, width = 1.05f, skin = skinA, shirt = C(0.07f, 0.09f, 0.2f), pants = C(0.05f, 0.06f, 0.12f),
                hat = HatStyle.PoliceCap, hatColor = C(0.06f, 0.07f, 0.16f), badge = true, idle = IdleStyle.Breathe
            };
            VisitorLook Tom() => new VisitorLook
            {
                modelKey = "tom", height = 1.8f, skin = C(0.92f, 0.91f, 0.86f), shirt = C(0.05f, 0.05f, 0.06f), pants = C(0.05f, 0.05f, 0.06f),
                eyes = EyeStyle.Hollow, mouth = MouthStyle.Wide, skeletonCostume = true, idle = IdleStyle.Sway, distanceFromDoor = 0.95f
            };
            // A little boy, as small as the trick-or-treaters, in the tuxedo he went out in a year ago. Sweet-faced and
            // much too pale.
            VisitorLook Leo() => new VisitorLook
            {
                modelKey = "leo", height = 1.06f, width = 0.85f, skin = C(0.76f, 0.77f, 0.72f), shirt = C(0.05f, 0.05f, 0.06f),
                pants = C(0.05f, 0.05f, 0.06f), hat = HatStyle.Hair, hatColor = C(0.22f, 0.15f, 0.09f), headTilt = 6f,
                orangeShoes = true, idle = IdleStyle.Shiver, distanceFromDoor = 0.9f
            };
            VisitorLook Eli() => new VisitorLook
            {
                modelKey = "eli", height = 1.83f, skin = skinC, shirt = C(0.3f, 0.34f, 0.28f), pants = C(0.25f, 0.18f, 0.12f),
                hat = HatStyle.Hair, hatColor = C(0.08f, 0.06f, 0.05f), bloodStains = true, idle = IdleStyle.Nervous, distanceFromDoor = 1.0f
            };
            VisitorLook Dad() => new VisitorLook
            {
                modelKey = "dad", height = 1.8f, width = 1.12f, skin = skinA, shirt = C(0.22f, 0.24f, 0.2f), pants = C(0.15f, 0.14f, 0.13f),
                hat = HatStyle.Hair, hatColor = C(0.35f, 0.33f, 0.3f), idle = IdleStyle.Sway, distanceFromDoor = 0.95f
            };

            // ---------------------------------------------------------------- people
            var marco = new VisitorDef
            {
                id = "marco", name = "Marco", look = Marco(), voicePitch = 1f,
                knock = new[] { "MARCO_K1", "MARCO_K2", "MARCO_K3" },
                qa = new[]
                {
                    new QA("Who are you?", "MARCO_A1"), new QA("What do you want?", "MARCO_A2"),
                    new QA("What's the order number?", "MARCO_A3"), new QA("Did you see anyone outside?", "MARCO_A4"),
                },
                goAway = "MARCO_GA", leave = "MARCO_L", letIn = "MARCO_LI", taken = "MARCO_TK",
                helper = Helper.Flashlight, sitsInside = false, returnAs = "marco_m",
                takenSender = "Gino's Pizza", takenText = "We're sorry - your driver was unable to complete delivery #31. Our thoughts are with his family.",
                outcome = "Marco pressed the pizza and a heavy flashlight into your hands.\n\"Keep it. Trust me.\"\nHe jogged back to his car without looking back.",
                endingSaved = "Marco quit Gino's the next morning. He still keeps a flashlight in every room.",
                endingLost = "Marco's car was found on Elm Street with the engine running and the door open.",
            };
            Add(marco);

            var ana = new VisitorDef
            {
                id = "ana", name = "Ana", look = Ana(), voicePitch = 1.1f,
                knock = new[] { "ANA_K1", "ANA_K2", "ANA_K3" },
                qa = new[]
                {
                    new QA("Who are you?", "ANA_A1"), new QA("What do you want?", "ANA_A2"),
                    new QA("What did Leo call you?", "ANA_A3"), new QA("Why are you here?", "ANA_A4"),
                },
                goAway = "ANA_GA", leave = "ANA_L", letIn = "ANA_LI", taken = "ANA_TK", returnAs = "ana_m",
                takenSender = "Ana", takenText = "theres someone in the road its",
                outcome = "Ana hugged you so hard it hurt.\nShe was soaked, and shaking, and very much alive.",
                endingSaved = "Ana stayed a week. Then a month. She says she's not leaving you alone on Halloween ever again.",
                endingLost = "Ana's car was still on Route 9 in the morning. Her phone was on the driver's seat, still ringing.",
            };
            Add(ana);

            Add(new VisitorDef
            {
                id = "officer_h", name = "Officer Daniels", look = Officer(), voicePitch = 0.95f,
                knock = new[] { "OFFH_K1", "OFFH_K2", "OFFH_K3" },
                qa = new[]
                {
                    new QA("Who are you?", "OFFH_A1"), new QA("What do you want?", "OFFH_A2"),
                    new QA("What's your badge number?", "OFFH_A3"), new QA("Have you seen a little boy in a tuxedo?", "OFFH_A4"),
                },
                goAway = "OFFH_GA", leave = "OFFH_L", letIn = "OFFH_LI", taken = "OFFH_TK",
                helper = Helper.GuardBackDoor, returnAs = "officer_m",
                outcome = "Officer Daniels checked every window twice,\nthen planted himself by the back door with his hand on his holster.",
                endingSaved = "Officer Daniels wrote 'nothing to report' in his log. He's never been more grateful to lie.",
                endingLost = "Officer Daniels' cruiser was found at the end of Elm Street. Lights still flashing. Nobody inside.",
            });

            Add(new VisitorDef
            {
                id = "tom_h", name = "Tom", look = Tom(), voicePitch = 1f, knockStyle = KnockStyle.Frantic,
                knock = new[] { "TOMH_K1", "TOMH_K2", "TOMH_K3" },
                qa = new[]
                {
                    new QA("Who are you?", "TOMH_A1"), new QA("What do you want?", "TOMH_A2"),
                    new QA("Why are you wearing a mask?", "TOMH_A3"), new QA("Do you know the basement?", "TOMH_A4"),
                },
                goAway = "TOMH_GA", leave = "TOMH_L", letIn = "TOMH_LI", taken = "TOMH_TK",
                helper = Helper.FixPower, returnAs = "tom_m",
                outcome = "Tom stumbled in and claimed the armchair like a throne,\nstill wearing the skull mask.",
                endingSaved = "Tom woke up at noon with no memory of the night and an unexplained fear of stairwells.",
                endingLost = "Tom's skeleton mask was found at the bottom of the stairwell. Tom wasn't.",
            });

            Add(new VisitorDef
            {
                id = "eli", name = "Eli", look = Eli(), voicePitch = 0.95f, urgent = true, patience = 40f, knockStyle = KnockStyle.Frantic,
                knock = new[] { "ELI_K1", "ELI_K2", "ELI_K3" },
                qa = new[]
                {
                    new QA("Who are you?", "ELI_A1"), new QA("What do you want?", "ELI_A2"),
                    new QA("What attacked you?", "ELI_A3"), new QA("Have you seen a little boy in a tuxedo?", "ELI_A4"),
                },
                goAway = "ELI_GA", leave = "ELI_L", letIn = "ELI_LI", taken = "ELI_TK", returnAs = "eli_m",
                outcome = "You wrapped Eli's arm in a dish towel.\nHe sat in the chair by the hall, staring at the door, and didn't stop shaking.",
                endingSaved = "Eli's arm healed. He left Hollow Creek a week later and sent you a postcard from the coast. It just says THANK YOU.",
                endingLost = "There's a dark stain on the porch boards that no amount of scrubbing will lift.",
            });

            Add(new VisitorDef
            {
                id = "dad_h", name = "Dad", look = Dad(), voicePitch = 0.9f,
                knock = new[] { "DADH_K1", "DADH_K2", "DADH_K3" },
                qa = new[]
                {
                    new QA("Who are you?", "DADH_A1"), new QA("Why are you out there?", "DADH_A2"),
                    new QA("What did Leo want to be when he grew up?", "DADH_A3"), new QA("Why did you come?", "DADH_A4"),
                },
                goAway = "DADH_GA", leave = "DADH_L", letIn = "DADH_LI", taken = "DADH_TK", returnAs = "dad_m",
                takenSender = "Mom", takenText = "Your father's phone keeps going to voicemail. Sam, is he with you??",
                outcome = "Your father held on to you in the hallway for a long time.\nNeither of you said anything. You didn't have to.",
                endingSaved = "Your dad stopped drinking. Not right away, but he did. He says that night gave him his kids back.",
                endingLost = "They found your father's keys on the porch of 13 Elm. Your mother hasn't spoken to you since.",
            });

            // ---------------------------------------------------------------- things wearing faces
            VisitorDef Mimic(string id, string name, VisitorLook look, string prefix, string[] asks, float pitch, string hint)
            {
                var v = new VisitorDef
                {
                    id = id, name = name, mimic = true, look = look, voicePitch = pitch, knockStyle = KnockStyle.Even,
                    knock = new[] { prefix + "_K1", prefix + "_K2", prefix + "_K1" },
                    goAway = prefix + "_GA", leave = prefix + "_L", angry = prefix + "_AN", death = prefix + "_D",
                    peep = prefix + "_PEEP", atWindow = prefix + "_WIN",
                    extraHint = hint,
                };
                var qa = new List<QA>();
                for (int i = 0; i < asks.Length; i++) qa.Add(new QA(asks[i], prefix + "_A" + (i + 1)));
                v.qa = qa.ToArray();
                look.idle = IdleStyle.Still;
                return v;
            }

            var mm = Mimic("marco_m", "Marco", Marco(), "MARCOM",
                new[] { "Who are you?", "What do you want?", "What's the order number?", "Did you see anyone outside?" }, 0.95f,
                "Marco's order was #31. And Marco was already taken.");
            mm.knock = new[] { "MARCOM_K1", "MARCOM_K2", "MARCOM_K1" };
            Add(mm);

            var masks = Mimic("masks", "Trick-or-treaters", new VisitorLook
            {
                modelKey = "masks", groupCount = 3, height = 1.22f, width = 0.82f, hat = HatStyle.None,
                eyes = EyeStyle.Hollow, mouth = MouthStyle.Wide, distanceFromDoor = 1.0f
            }, "MASKS", new[] { "Who are you?", "What do you want?", "Where are your parents?", "Take off your masks." }, 1.3f,
                "Children don't stand that still. And they don't come in threes, alone, after midnight.");
            masks.knock = new[] { "MASKS_K1", "MASKS_K2", "MASKS_K3" };
            masks.look.idle = IdleStyle.Still;
            Add(masks);

            var anaM = Mimic("ana_m", "Ana", Ana(), "ANAM",
                new[] { "Who are you?", "What do you want?", "What did Leo call you?", "Why are you here?" }, 1.05f,
                "Leo called her Banana-Ana. The real Ana would never have forgotten.");
            anaM.witness["ana"] = "ANA_IN1";
            Add(anaM);

            var offM = Mimic("officer_m", "Officer Daniels", Officer(), "OFFM",
                new[] { "Who are you?", "What do you want?", "What's your badge number?", "Have you seen a little boy in a tuxedo?" }, 0.85f,
                "Check your messages. Was anyone really going door to door tonight?");
            offM.quiet = true;
            Add(offM);

            var tomM = Mimic("tom_m", "Tom", Tom(), "TOMM",
                new[] { "Who are you?", "What do you want?", "Why are you wearing a mask?", "Do you know the basement?" }, 0.95f,
                "Tom texted you. He was staying at the party all night.");
            tomM.quiet = true;
            Add(tomM);

            var leoF = Mimic("leo_f", "Leo", Leo(), "LEOF",
                new[] { "Who are you?", "Where have you been?", "What's our knock?", "What did you want to be when you grew up?" }, 1.25f,
                "Leo would know your knock. Leo wanted to be a lighthouse keeper.");
            leoF.knock = new[] { "LEOF_K1", "LEOF_K2", "LEOF_K3" };
            leoF.witness["ana"] = "ANA_IN2";
            Add(leoF);

            var eliM = Mimic("eli_m", "Eli", Eli(), "ELIM",
                new[] { "Who are you?", "What do you want?", "What attacked you?", "Have you seen a little boy in a tuxedo?" }, 0.95f,
                "You left Eli outside. Whatever took him is wearing him now.");
            eliM.look.bloodStains = true;
            eliM.knock = new[] { "ELIM_K1", "ELIM_K2", "ELIM_K1" };
            Add(eliM);

            var dadM = Mimic("dad_m", "Dad", Dad(), "DADM",
                new[] { "Who are you?", "Why are you out there?", "What did Leo want to be when he grew up?", "Why did you come?" }, 0.85f,
                "Mom texted you: Dad was asleep on the couch at home.");
            Add(dadM);

            var hale = Mimic("hale", "Mrs. Hale", new VisitorLook
            {
                modelKey = "hale", body = BodyShape.Tall, height = 2.3f, width = 0.85f, skin = C(0.8f, 0.76f, 0.72f),
                shirt = C(0.33f, 0.2f, 0.33f), pants = C(0.2f, 0.18f, 0.2f), hat = HatStyle.Bun, hatColor = C(0.62f, 0.62f, 0.64f),
                glasses = true, eyes = EyeStyle.Black, mouth = MouthStyle.Wide, headTilt = 32f, idle = IdleStyle.Twitch, distanceFromDoor = 0.7f
            }, "HALE", new[] { "Who are you?", "What do you want?", "What's your cat's name?", "Did you see my brother?" }, 1.2f,
                "Mrs. Hale died last year. And nobody stands like that.");
            hale.knock = new[] { "HALE_K1", "HALE_K2", "HALE_K3" };
            hale.look.idle = IdleStyle.Twitch;
            hale.witness["tom_h"] = "TOMH_IN1";
            Add(hale);

            var voss = Mimic("voss_f", "Mr. Voss", new VisitorLook
            {
                modelKey = "voss", height = 1.76f, width = 1.1f, skin = skinA, shirt = C(0.33f, 0.24f, 0.15f), pants = C(0.14f, 0.13f, 0.12f),
                hat = HatStyle.Cap, hatColor = C(0.28f, 0.27f, 0.26f), glasses = true, distanceFromDoor = 0.8f
            }, "VOSSF", new[] { "Who are you?", "What do you want?", "What time is it?", "What's rule seven?" }, 0.85f,
                "Rule 7: Mr. Voss will NEVER knock before sunrise. The clock said 5:50.");
            voss.knock = new[] { "VOSSF_K1", "VOSSF_K2", "VOSSF_K3" };
            voss.witness["ana"] = "ANA_IN3";
            voss.witness["eli"] = "ELI_IN1";
            Add(voss);

            // ---------------------------------------------------------------- the final knock
            Add(new VisitorDef
            {
                id = "leo_real", name = "Leo", look = Leo(), voicePitch = 1.25f, knockStyle = KnockStyle.Secret, patience = 120f,
                knock = new[] { "LEOR_K1", "LEOR_K1", "LEOR_K1" },
                qa = new[]
                {
                    new QA("Who are you?", "LEOR_A1"), new QA("What's our knock?", "LEOR_A2"),
                    new QA("What did you want to be when you grew up?", "LEOR_A3"), new QA("I'm sorry, Leo.", "LEOR_A4"),
                },
                goAway = "LEOR_GA", leave = "LEOR_GA", letIn = "LEOR_LI", sitsInside = false,
            });
            var leoX = Mimic("leo_x", "Leo", Leo(), "LEOX",
                new[] { "Who are you?", "What's our knock?", "What did you want to be when you grew up?", "I'm sorry, Leo." }, 1.25f,
                "The real Leo only comes home to a house that opened its door to the living.");
            leoX.knock = new[] { "LEOX_K1", "LEOX_K1", "LEOX_K1" };
            leoX.patience = 120f;
            Add(leoX);

            return d;
        }

        /// <summary>The fixed visits of the night. "officer", "tom", "dad" and "leo_final" are resolved by the NightPlan.</summary>
        public static readonly (int minute, string id)[] Schedule =
        {
            (10, "marco"),
            (36, "masks"),
            (62, "officer"),
            (92, "ana"),
            (122, "tom"),
            (142, "leo_f"),
            (212, "eli"),
            (252, "dad"),
            (282, "hale"),
            (350, "voss_f"),
            (357, "leo_final"),
        };

        public static List<Message> Messages()
        {
            return new List<Message>
            {
                new Message { minute = -18, sender = "Mom", text = "Are you really in Hollow Creek tonight? Sam, please come home. Please. I can't lose you too." },
                new Message { minute = -6, sender = "Ana", text = "landed. driving up now. don't do anything stupid. ill be there by 1:30" },
                new Message { minute = 2, sender = "Gino's Pizza", text = "Order #31 is on its way! Your driver MARCO (red Gino's cap) will be there soon." },
                new Message { minute = 50, sender = "Hollow Creek PD", when = p => p.officerMimic,
                    text = "ALERT: Stay indoors until sunrise. Officers will NOT go door to door tonight. Do not open your door for anyone claiming to be police." },
                new Message { minute = 50, sender = "Hollow Creek PD", when = p => !p.officerMimic,
                    text = "ALERT: Stay indoors until sunrise. Officer Daniels (badge #4471) is doing welfare checks on Elm Street. Do not let anyone in." },
                new Message { minute = 66, sender = "Tom (2B)", text = "hey its Tom from upstairs! at a costume party, going as a skeleton lol. if i lose my keys again can i crash on ur couch" },
                new Message { minute = 80, sender = "Ana", text = "car died on route 9 like a mile out. walking. yellow raincoat. look for the drowned rat" },
                new Message { minute = 108, sender = "Tom (2B)", when = p => p.tomMimic, text = "ok change of plans, staying at the party tonight. dont wait up neighbor!" },
                new Message { minute = 120, sender = "Mr. Voss", text = "They're learning. Watch the shadow, not the face." },
                new Message { minute = 240, sender = "Mr. Voss", text = "They breathe now. I've never seen them learn this fast. Ask them things only the real ones would know." },
                new Message { minute = 138, sender = "Unknown Number", creepy = true, text = "he's outside" },
                new Message { minute = 190, sender = "Mom", text = "Sam why aren't you answering" },
                new Message { minute = 200, sender = "Unknown Number", creepy = true, text = "a man is going door to door saying he's hurt. he is NOT hurt. dont open" },
                new Message { minute = 240, sender = "Mom", when = p => !p.dadMimic, text = "Your father went out an hour ago. Drunk. He said he's going to find Leo. Is he with you?" },
                new Message { minute = 240, sender = "Mom", when = p => p.dadMimic, text = "Dad finally fell asleep on the couch. I'm glad you're not alone tonight. Are you alone?" },
                new Message { minute = 330, sender = "Mr. Voss", text = "Almost there. Sunrise is at 6:00. Remember rule 7." },
                new Message { minute = 346, sender = "Unknown Number", creepy = true, text = "sam its me. im coming home" },
            };
        }

        public static List<NightEvent> Events()
        {
            return new List<NightEvent>
            {
                new NightEvent { minute = 20, kind = EventKind.Thunder },
                new NightEvent { minute = 28, kind = EventKind.WindowTap },
                new NightEvent { minute = 42, kind = EventKind.Dishes },
                new NightEvent { minute = 50, kind = EventKind.LightsFlicker },
                new NightEvent { minute = 75, kind = EventKind.Footsteps },
                new NightEvent { minute = 100, kind = EventKind.BedroomMemory },
                new NightEvent { minute = 112, kind = EventKind.MailSlot },
                new NightEvent { minute = 120, kind = EventKind.RulesAmended, needsQuiet = false },
                new NightEvent { minute = 135, kind = EventKind.HomeCall },
                new NightEvent { minute = 150, kind = EventKind.WindBlowsWindow },
                new NightEvent { minute = 165, kind = EventKind.MomCall },
                new NightEvent { minute = 172, kind = EventKind.WindowFigure },
                new NightEvent { minute = 176, kind = EventKind.Doorknob },
                new NightEvent { minute = 196, kind = EventKind.ToiletFlush },
                new NightEvent { minute = 205, kind = EventKind.Thunder },
                new NightEvent { minute = 225, kind = EventKind.TVBroadcast },
                new NightEvent { minute = 240, kind = EventKind.RulesAmended, needsQuiet = false },
                new NightEvent { minute = 242, kind = EventKind.WallScratch },
                new NightEvent { minute = 262, kind = EventKind.ChairScrape },
                new NightEvent { minute = 275, kind = EventKind.WindBlowsWindow },
                new NightEvent { minute = 288, kind = EventKind.Snoring },
                new NightEvent { minute = 300, kind = EventKind.Siege },
                new NightEvent { minute = 316, kind = EventKind.Breathing },
                new NightEvent { minute = 336, kind = EventKind.Thunder },
            };
        }

        public static readonly string[] IntroCards =
        {
            "One year ago, on Halloween night,\nyour little brother knocked on your bedroom door\nand asked you to take him trick-or-treating.",
            "You told him to go away.",
            "Leo Hollis was last seen on the porch of\n13 Elm Street, Hollow Creek.\nHe was never found.",
            "Tonight, you rented the ground-floor apartment.\n\nYou're going to wait for him.",
        };

        public const string TrueEndingTitle = "THE LIGHTHOUSE";
        public const string TrueEndingText =
            "He slept on the couch with his head on your shoulder until the sun was all the way up.\n" +
            "Nobody in Hollow Creek could explain it. Nobody tried.\nYou kept your promise. You opened the door.";
        public const string GoAwayTitle = "GO AWAY";
        public const string GoAwayText =
            "In the morning there were small wet footprints on the porch.\nThey led down the steps, and then they just... stopped.\nYou told him to go away. Twice.";
        public const string AloneTitle = "ALONE";
        public const string AloneText =
            "It wasn't him. You knew it wasn't him.\nThe house stayed quiet after that, and you sat by the door until noon,\nwaiting for a knock that never came.";
        public const string ReplacedTitle = "WELCOME HOME";
        public const string ReplacedText =
            "It wore him perfectly. Almost.\nAt dawn, Mr. Voss knocked, and someone who looked exactly like you opened the door and smiled.";
    }
}
