using UnityEngine;

namespace DontAnswer
{
    /// <summary>Everything that dies when the power goes out at 3:03 AM.</summary>
    public class PowerGrid : MonoBehaviour
    {
        public Light[] lights;
        public Renderer[] glowing;
        public PorchLight porch;
        public TVSet tv;
        public Renderer intercomLed;

        public bool On { get; private set; } = true;

        float[] baseIntensity;
        Material[] glowMats;
        Color[] glowBase, glowEmission;
        Material ledMat;
        Color ledBase;

        void Awake()
        {
            baseIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;
            glowMats = new Material[glowing.Length];
            glowBase = new Color[glowing.Length];
            glowEmission = new Color[glowing.Length];
            for (int i = 0; i < glowing.Length; i++)
            {
                if (glowing[i] == null) continue;
                glowMats[i] = glowing[i].material;
                glowBase[i] = glowMats[i].HasProperty("_BaseColor") ? glowMats[i].GetColor("_BaseColor") : Color.white;
                glowEmission[i] = glowMats[i].HasProperty("_EmissionColor") ? glowMats[i].GetColor("_EmissionColor") : Color.black;
            }
            if (intercomLed != null)
            {
                ledMat = intercomLed.material;
                ledBase = ledMat.GetColor("_BaseColor");
            }
        }

        public void SetPower(bool on)
        {
            On = on;
            foreach (var l in lights)
            {
                if (l == null) continue;
                var f = l.GetComponent<FlickerLight>();
                if (f != null) f.ForcedOff = !on;
                else l.enabled = on;
            }
            for (int i = 0; i < glowMats.Length; i++)
            {
                var m = glowMats[i];
                if (m == null) continue;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", on ? glowEmission[i] : Color.black);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", on ? glowBase[i] : glowBase[i] * 0.15f);
            }
            if (ledMat != null) ledMat.SetColor("_BaseColor", on ? ledBase : ledBase * 0.05f);
            if (porch != null) porch.SetPowered(on);
            if (tv != null && !on) tv.SetMode(TVSet.TVMode.Off);
        }
    }
}
