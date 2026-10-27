using System.Collections.Generic;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>
    /// Everything that's randomized for one night: who is really who, which tells each mimic shows,
    /// and what the porch light is doing. A new plan is rolled every time you start the night.
    /// </summary>
    public class NightPlan
    {
        public int seed;
        public bool officerMimic, tomMimic, dadMimic;
        public bool homeCallAnswered;     // rule 6 broken: the final mimic knows your knock
        public bool breathLearned;        // after 4 AM they fake breath too

        readonly Dictionary<string, Tells> tells = new Dictionary<string, Tells>();
        readonly Dictionary<string, PorchLightMode> lights = new Dictionary<string, PorchLightMode>();
        System.Random rng;

        public static NightPlan Create(int seed)
        {
            var p = new NightPlan { seed = seed, rng = new System.Random(seed) };
            p.officerMimic = p.rng.NextDouble() < 0.5;
            p.tomMimic = p.rng.NextDouble() < 0.4;
            p.dadMimic = p.rng.NextDouble() < 0.5;
            // Keep the night honest: at least one of the three is real, and at least one is not.
            if (p.officerMimic && p.tomMimic && p.dadMimic) p.tomMimic = false;
            if (!p.officerMimic && !p.tomMimic && !p.dadMimic) p.dadMimic = true;
            return p;
        }

        public string Resolve(string scheduleId)
        {
            switch (scheduleId)
            {
                case "officer": return officerMimic ? "officer_m" : "officer_h";
                case "tom": return tomMimic ? "tom_m" : "tom_h";
                case "dad": return dadMimic ? "dad_m" : "dad_h";
                default: return scheduleId;
            }
        }

        float Roll() => (float)rng.NextDouble();

        public PorchLightMode LightFor(VisitorDef v, int minute)
        {
            if (lights.TryGetValue(v.id, out var m)) return m;
            float r = Roll();
            if (v.id == "marco" || v.id == "masks") m = PorchLightMode.On;            // teach the rules first
            else if (v.id == "leo_real" || v.id == "leo_x") m = PorchLightMode.Flicker;
            else m = r < 0.58f ? PorchLightMode.On : r < 0.85f ? PorchLightMode.Flicker : PorchLightMode.Off;
            lights[v.id] = m;
            return m;
        }

        /// <summary>Which giveaways this visitor shows tonight. People show none.</summary>
        public Tells TellsFor(VisitorDef v, int minute)
        {
            if (!v.mimic) return Tells.None;
            if (tells.TryGetValue(v.id, out var t)) return t;

            bool learnedShadows = minute >= 120;
            bool learnedBreath = minute >= 240;
            t = Tells.None;
            t |= learnedShadows ? (Roll() < 0.75f ? Tells.WrongShadow : Tells.None) : Tells.NoShadow;
            if (!learnedBreath) t |= Tells.NoBreath;

            var extras = new List<Tells> { Tells.EvenKnock, Tells.SilentSteps, Tells.Eyeshine };
            int extraCount = v.id == "masks" ? 3 : (v.id == "voss_f" || v.id == "leo_x") ? 1 : (Roll() < 0.5f ? 1 : 2);
            if (minute >= 240 && v.id != "masks") extraCount = 1;   // the later it gets, the better they are at it
            for (int i = 0; i < extraCount && extras.Count > 0; i++)
            {
                int k = rng.Next(extras.Count);
                t |= extras[k];
                extras.RemoveAt(k);
            }

            // With the porch light dead, make sure there's still *something* to notice.
            var light = LightFor(v, minute);
            if (light == PorchLightMode.Off && (t & (Tells.EvenKnock | Tells.SilentSteps | Tells.Eyeshine)) == 0)
                t |= Tells.Eyeshine;

            // Mrs. Hale is just wrong in every way.
            if (v.id == "hale") t |= Tells.WrongShadow | Tells.SilentSteps;
            tells[v.id] = t;
            return t;
        }

        public static string Describe(Tells t)
        {
            var parts = new List<string>();
            if ((t & Tells.NoShadow) != 0) parts.Add("it cast no shadow under the porch light");
            if ((t & Tells.WrongShadow) != 0) parts.Add("its shadow moved on its own");
            if ((t & Tells.NoBreath) != 0) parts.Add("it didn't breathe smoke in the cold");
            if ((t & Tells.EvenKnock) != 0) parts.Add("it knocked like a clock - perfectly even");
            if ((t & Tells.SilentSteps) != 0) parts.Add("it made no sound coming up the porch steps");
            if ((t & Tells.Eyeshine) != 0) parts.Add("its eyes caught the light like an animal's");
            return parts.Count == 0 ? "" : "You could have noticed: " + string.Join(", ", parts) + ".";
        }
    }
}
