using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DontAnswer
{
    /// <summary>One built body (a visitor, one of a group, or a mimic's shadow twin) and its moving parts.</summary>
    public class VisitorBody
    {
        public Transform root, hips, torso, head, armL, armR, legL, legR;
        public Animator animator;
        public readonly List<Renderer> renderers = new List<Renderer>();
        public readonly List<Renderer> eyes = new List<Renderer>();
        public readonly List<Transform> mouth = new List<Transform>();
        public Quaternion armBaseL = Quaternion.identity, armBaseR = Quaternion.identity;
        public float headSize = 0.23f, legLength = 0.8f;
        public float faceCY, rx = 0.1f, ry = 0.12f, rz = 0.11f;   // face ellipsoid in head space
        public bool ghost;

        public Vector3 OnFace(float x, float y, float inset)
        {
            float k = 1f - (x * x) / (rx * rx) - ((y - faceCY) * (y - faceCY)) / (ry * ry);
            float z = rz * Mathf.Sqrt(Mathf.Max(0.02f, k));
            return new Vector3(x, y, z - inset);
        }

        public Vector3 FaceNormal(Vector3 p) =>
            new Vector3(p.x / (rx * rx), (p.y - faceCY) / (ry * ry), p.z / (rz * rz)).normalized;

        public Vector3 MouthWorld => head != null ? head.TransformPoint(OnFace(0f, faceCY - headSize * 0.27f, -0.02f)) : root.position + Vector3.up * 1.55f;
        public Vector3 HeadWorld => head != null ? head.TransformPoint(new Vector3(0f, faceCY, 0f)) : root.position + Vector3.up * 1.6f;

        public void SetShadowCasting(ShadowCastingMode mode)
        {
            foreach (var r in renderers) if (r != null) r.shadowCastingMode = mode;
        }
    }

    /// <summary>Builds low-poly bodies out of primitives. Used until (and wherever) no rigged model exists.</summary>
    public class PrimitiveBodyBuilder
    {
        readonly Transform owner;
        readonly Material lit, unlit;
        readonly Dictionary<string, Material> cache;
        readonly int layer;
        ShadowCastingMode shadow;

        static readonly Color Black = new Color(0.01f, 0.01f, 0.01f);
        static readonly Color EyeWhite = new Color(0.88f, 0.87f, 0.83f);
        static readonly Color Gold = new Color(0.85f, 0.65f, 0.15f);

        public PrimitiveBodyBuilder(Transform owner, Material lit, Material unlit, Dictionary<string, Material> cache, int layer)
        {
            this.owner = owner;
            this.lit = lit;
            this.unlit = unlit;
            this.cache = cache;
            this.layer = layer;
        }

        public VisitorBody Build(VisitorLook l, Vector3 offset, float scale, ShadowCastingMode shadowMode, string name)
        {
            shadow = shadowMode;
            var b = new VisitorBody();
            b.root = Node(name, owner, offset);
            b.root.localScale = Vector3.one * scale;
            if (l.body == BodyShape.Ghost) BuildGhost(b, l);
            else BuildPerson(b, l);
            return b;
        }

        // ------------------------------------------------------------------ people

        void BuildPerson(VisitorBody b, VisitorLook l)
        {
            float H = l.height, W = l.width;
            bool tall = l.body == BodyShape.Tall;
            b.legLength = H * 0.47f;
            float torsoH = H * (tall ? 0.29f : 0.31f);
            float neckH = H * (tall ? 0.09f : 0.03f);
            b.headSize = Mathf.Clamp(H * 0.13f, 0.17f, 0.25f);
            float torsoW = 0.36f * W, torsoD = 0.22f * W;
            float armLen = H * (tall ? 0.42f : 0.34f);
            Color shoe = l.orangeShoes ? new Color(1f, 0.45f, 0.05f) : new Color(0.08f, 0.07f, 0.07f);

            b.hips = Node("Hips", b.root, new Vector3(0f, b.legLength, 0f));
            b.legL = Node("LegL", b.hips, new Vector3(-0.095f * W, 0f, 0f));
            b.legR = Node("LegR", b.hips, new Vector3(0.095f * W, 0f, 0f));
            foreach (var leg in new[] { b.legL, b.legR })
            {
                Part(b, PrimitiveType.Capsule, leg, new Vector3(0f, -b.legLength * 0.5f, 0f), new Vector3(0.14f * W, b.legLength * 0.5f, 0.14f * W), l.pants);
                Part(b, PrimitiveType.Cube, leg, new Vector3(0f, -b.legLength + 0.04f, 0.05f), new Vector3(0.12f, 0.08f, 0.25f), shoe);
            }

            b.torso = Node("Torso", b.hips, Vector3.zero);
            Part(b, PrimitiveType.Capsule, b.torso, new Vector3(0f, torsoH * 0.5f, 0f), new Vector3(torsoW, torsoH * 0.5f + 0.07f, torsoD), l.shirt);
            if (l.coat)
            {
                float coatLen = H * 0.24f;
                Part(b, PrimitiveType.Cylinder, b.torso, new Vector3(0f, -coatLen * 0.5f + 0.05f, 0f), new Vector3(torsoW * 1.12f, coatLen * 0.5f, torsoD * 1.35f), l.shirt);
            }

            float shoulderX = torsoW * 0.5f + 0.045f;
            b.armL = Node("ArmL", b.torso, new Vector3(-shoulderX, torsoH + 0.02f, 0f));
            b.armR = Node("ArmR", b.torso, new Vector3(shoulderX, torsoH + 0.02f, 0f));
            foreach (var arm in new[] { b.armL, b.armR })
            {
                Part(b, PrimitiveType.Capsule, arm, new Vector3(0f, -armLen * 0.5f, 0f), new Vector3(0.095f * W, armLen * 0.5f, 0.095f * W), l.shirt);
                Part(b, PrimitiveType.Sphere, arm, new Vector3(0f, -armLen + 0.02f, 0f), Vector3.one * 0.085f, l.skin);
            }
            b.armBaseL = Quaternion.Euler(0f, 0f, -5f);
            b.armBaseR = Quaternion.Euler(0f, 0f, 5f);

            var neck = Node("Neck", b.torso, new Vector3(0f, torsoH + 0.04f, 0f));
            Part(b, PrimitiveType.Cylinder, neck, new Vector3(0f, neckH * 0.5f, 0f), new Vector3(0.085f, neckH * 0.5f + 0.03f, 0.085f), l.skin);
            b.head = Node("Head", neck, new Vector3(0f, neckH, 0f));

            float s = b.headSize;
            b.faceCY = s * 0.5f; b.rx = s * 0.45f; b.ry = s * 0.5f; b.rz = s * 0.475f;
            Part(b, PrimitiveType.Sphere, b.head, new Vector3(0f, b.faceCY, 0f), new Vector3(b.rx * 2f, b.ry * 2f, b.rz * 2f), l.skin);
            BuildFace(b, l, s);
            BuildHat(b, l, s);

            if (l.pizzaBox)
            {
                b.armBaseL = Quaternion.Euler(-50f, 0f, -4f);
                b.armBaseR = Quaternion.Euler(-50f, 0f, 4f);
                var box = Node("PizzaBox", b.torso, new Vector3(0f, torsoH * 0.3f, torsoD * 0.5f + 0.2f));
                Part(b, PrimitiveType.Cube, box, Vector3.zero, new Vector3(0.42f, 0.055f, 0.42f), new Color(0.82f, 0.78f, 0.68f));
                Part(b, PrimitiveType.Cube, box, new Vector3(0f, 0.029f, 0f), new Vector3(0.18f, 0.004f, 0.18f), new Color(0.75f, 0.1f, 0.08f));
            }
            if (l.badge)
                Part(b, PrimitiveType.Cube, b.torso, new Vector3(-torsoW * 0.22f, torsoH * 0.72f, torsoD * 0.5f), new Vector3(0.05f, 0.065f, 0.02f), Gold);
            if (l.bloodStains)
            {
                var blood = new Color(0.33f, 0.02f, 0.02f);
                Part(b, PrimitiveType.Sphere, b.torso, new Vector3(torsoW * 0.18f, torsoH * 0.45f, torsoD * 0.42f), new Vector3(0.15f, 0.2f, 0.06f), blood);
                Part(b, PrimitiveType.Sphere, b.torso, new Vector3(-torsoW * 0.2f, torsoH * 0.12f, torsoD * 0.42f), new Vector3(0.09f, 0.11f, 0.05f), blood);
                Part(b, PrimitiveType.Capsule, b.armR, new Vector3(0f, -armLen * 0.62f, 0f), new Vector3(0.105f, armLen * 0.22f, 0.105f), blood);
            }
            if (l.skeletonCostume)
            {
                var bone = new Color(0.9f, 0.9f, 0.84f);
                for (int i = 0; i < 4; i++)
                    Part(b, PrimitiveType.Cube, b.torso, new Vector3(0f, torsoH * (0.3f + 0.15f * i), torsoD * 0.48f),
                        new Vector3(torsoW * (0.75f - 0.06f * Mathf.Abs(i - 1.5f)), 0.028f, 0.02f), bone);
                Part(b, PrimitiveType.Cube, b.torso, new Vector3(0f, torsoH * 0.5f, torsoD * 0.5f), new Vector3(0.035f, torsoH * 0.85f, 0.02f), bone);
                foreach (var arm in new[] { b.armL, b.armR })
                    Part(b, PrimitiveType.Cube, arm, new Vector3(0f, -armLen * 0.5f, 0.047f), new Vector3(0.025f, armLen * 0.8f, 0.012f), bone);
                foreach (var leg in new[] { b.legL, b.legR })
                    Part(b, PrimitiveType.Cube, leg, new Vector3(0f, -b.legLength * 0.5f, 0.07f), new Vector3(0.03f, b.legLength * 0.8f, 0.012f), bone);
            }
            b.armL.localRotation = b.armBaseL;
            b.armR.localRotation = b.armBaseR;
        }

        void BuildGhost(VisitorBody b, VisitorLook l)
        {
            float H = l.height, W = l.width;
            float r = 0.21f * W;
            b.ghost = true;
            b.legLength = 0f;
            b.hips = Node("Hips", b.root, Vector3.zero);
            b.torso = Node("Torso", b.hips, Vector3.zero);
            Part(b, PrimitiveType.Capsule, b.torso, new Vector3(0f, H * 0.5f, 0f), new Vector3(r * 2f, H * 0.5f, r * 2f), l.shirt);
            Part(b, PrimitiveType.Cylinder, b.torso, new Vector3(0f, 0.09f, 0f), new Vector3(r * 2.5f, 0.09f, r * 2.45f), l.shirt);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                Part(b, PrimitiveType.Sphere, b.torso, new Vector3(Mathf.Cos(a) * r * 1.2f, 0.03f, Mathf.Sin(a) * r * 1.2f), new Vector3(0.1f, 0.06f, 0.1f), l.shirt);
            }
            Color shoe = l.orangeShoes ? new Color(1f, 0.45f, 0.05f) : new Color(0.85f, 0.85f, 0.82f);
            Part(b, PrimitiveType.Cube, b.torso, new Vector3(-0.07f, 0.03f, r * 1.15f), new Vector3(0.08f, 0.06f, 0.12f), shoe);
            Part(b, PrimitiveType.Cube, b.torso, new Vector3(0.07f, 0.03f, r * 1.15f), new Vector3(0.08f, 0.06f, 0.12f), shoe);

            b.headSize = r * 2f;
            b.head = Node("Head", b.torso, new Vector3(0f, H * 0.75f, 0f));
            b.faceCY = 0f; b.rx = r; b.rz = r; b.ry = H * 0.25f;
            BuildFace(b, l, b.headSize);
        }

        void BuildFace(VisitorBody b, VisitorLook l, float s)
        {
            bool ghost = l.body == BodyShape.Ghost;
            float ex = ghost ? b.rx * 0.36f : s * 0.19f;
            float ey = b.faceCY + (ghost ? b.ry * 0.18f : s * 0.06f);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * ex;
                switch (l.eyes)
                {
                    case EyeStyle.Normal:
                        Part(b, PrimitiveType.Sphere, b.head, b.OnFace(x, ey, s * 0.04f), new Vector3(s * 0.17f, s * 0.12f, s * 0.1f), EyeWhite);
                        b.eyes.Add(Part(b, PrimitiveType.Sphere, b.head, b.OnFace(x, ey, -s * 0.01f), new Vector3(s * 0.075f, s * 0.075f, s * 0.04f), Black).GetComponent<Renderer>());
                        break;
                    case EyeStyle.Black:
                        b.eyes.Add(Part(b, PrimitiveType.Sphere, b.head, b.OnFace(x, ey, s * 0.03f), new Vector3(s * 0.2f, s * 0.15f, s * 0.1f), Black).GetComponent<Renderer>());
                        break;
                    case EyeStyle.Glowing:
                        b.eyes.Add(Part(b, PrimitiveType.Sphere, b.head, b.OnFace(x, ey, s * 0.03f), new Vector3(s * 0.15f, s * 0.12f, s * 0.1f), new Color(2.5f, 0.3f, 0.15f), true).GetComponent<Renderer>());
                        break;
                    case EyeStyle.Hollow:
                        var size = ghost ? new Vector3(b.rx * 0.42f, b.ry * 0.32f, b.rz * 0.3f) : new Vector3(s * 0.22f, s * 0.27f, s * 0.1f);
                        b.eyes.Add(Part(b, PrimitiveType.Sphere, b.head, b.OnFace(x * 1.05f, ey, ghost ? b.rz * 0.1f : s * 0.035f), size, Black).GetComponent<Renderer>());
                        break;
                }
            }
            if (ghost) return;

            if (l.skeletonCostume)
                Part(b, PrimitiveType.Cube, b.head, b.OnFace(0f, b.faceCY - s * 0.09f, s * 0.01f), new Vector3(s * 0.07f, s * 0.09f, s * 0.04f), Black);
            else
                Part(b, PrimitiveType.Sphere, b.head, b.OnFace(0f, b.faceCY - s * 0.06f, s * 0.03f), new Vector3(s * 0.11f, s * 0.16f, s * 0.14f), l.skin * 0.92f);

            float mouthY = b.faceCY - s * 0.27f;
            switch (l.mouth)
            {
                case MouthStyle.Normal:
                    BuildMouth(b, s * 0.26f, s * 0.035f, s * 0.01f, new Color(0.3f, 0.09f, 0.09f), 3, mouthY, s * 0.02f);
                    break;
                case MouthStyle.Wide:
                    BuildMouth(b, s * 0.66f, s * 0.08f, s * 0.08f, Black, 9, mouthY, s * 0.025f);
                    BuildMouth(b, s * 0.58f, s * 0.022f, s * 0.075f, new Color(0.85f, 0.83f, 0.72f), 9, mouthY + s * 0.018f, s * 0.012f);
                    break;
            }

            if (l.glasses)
            {
                var frame = new Color(0.04f, 0.04f, 0.04f);
                for (int side = -1; side <= 1; side += 2)
                {
                    var p = b.OnFace(side * ex, ey, -s * 0.035f);
                    Part(b, PrimitiveType.Cube, b.head, p + new Vector3(0f, s * 0.085f, 0f), new Vector3(s * 0.26f, s * 0.03f, s * 0.02f), frame);
                    Part(b, PrimitiveType.Cube, b.head, p - new Vector3(0f, s * 0.08f, 0f), new Vector3(s * 0.26f, s * 0.02f, s * 0.02f), frame);
                    Part(b, PrimitiveType.Cube, b.head, p + new Vector3(side * s * 0.13f, 0f, -s * 0.02f), new Vector3(s * 0.025f, s * 0.18f, s * 0.02f), frame);
                }
                Part(b, PrimitiveType.Cube, b.head, b.OnFace(0f, ey + s * 0.02f, -s * 0.04f), new Vector3(s * 0.12f, s * 0.025f, s * 0.02f), frame);
            }
        }

        void BuildMouth(VisitorBody b, float width, float height, float curve, Color color, int segments, float y0, float inset)
        {
            for (int i = 0; i < segments; i++)
            {
                float u = (i + 0.5f) / segments * 2f - 1f;
                float x = u * width * 0.5f;
                float y = y0 + curve * u * u;
                var p = b.OnFace(x, y, inset);
                var go = Part(b, PrimitiveType.Cube, b.head, p, new Vector3(width / segments * 1.2f, height, b.headSize * 0.06f), color);
                go.transform.localRotation = Quaternion.LookRotation(b.FaceNormal(p), Vector3.up);
                b.mouth.Add(go.transform);
            }
        }

        void BuildHat(VisitorBody b, VisitorLook l, float s)
        {
            var c = l.hatColor;
            switch (l.hat)
            {
                case HatStyle.Cap:
                    Part(b, PrimitiveType.Sphere, b.head, new Vector3(0f, s * 0.8f, -s * 0.02f), new Vector3(s * 0.98f, s * 0.55f, s * 1.02f), c);
                    Part(b, PrimitiveType.Cube, b.head, new Vector3(0f, s * 0.74f, s * 0.5f), new Vector3(s * 0.66f, s * 0.04f, s * 0.42f), c * 0.8f);
                    break;
                case HatStyle.PoliceCap:
                    Part(b, PrimitiveType.Cylinder, b.head, new Vector3(0f, s * 0.97f, 0f), new Vector3(s * 1.08f, s * 0.13f, s * 1.08f), c);
                    Part(b, PrimitiveType.Cylinder, b.head, new Vector3(0f, s * 0.84f, 0f), new Vector3(s * 0.96f, s * 0.05f, s * 0.96f), Black);
                    Part(b, PrimitiveType.Cube, b.head, new Vector3(0f, s * 0.8f, s * 0.5f), new Vector3(s * 0.7f, s * 0.04f, s * 0.4f), Black);
                    Part(b, PrimitiveType.Cube, b.head, new Vector3(0f, s * 0.97f, s * 0.54f), new Vector3(s * 0.14f, s * 0.15f, s * 0.03f), Gold);
                    break;
                case HatStyle.Hood:
                    Part(b, PrimitiveType.Sphere, b.head, new Vector3(0f, s * 0.56f, -s * 0.21f), new Vector3(s * 1.22f, s * 1.25f, s * 1.15f), c);
                    break;
                case HatStyle.Hair:
                    Part(b, PrimitiveType.Sphere, b.head, new Vector3(0f, s * 0.66f, -s * 0.06f), new Vector3(s * 0.98f, s * 0.75f, s * 1.0f), c);
                    break;
                case HatStyle.Bun:
                    Part(b, PrimitiveType.Sphere, b.head, new Vector3(0f, s * 0.66f, -s * 0.06f), new Vector3(s * 0.98f, s * 0.75f, s * 1.0f), c);
                    Part(b, PrimitiveType.Sphere, b.head, new Vector3(0f, s * 0.98f, -s * 0.3f), Vector3.one * s * 0.42f, c);
                    break;
            }
        }

        // ------------------------------------------------------------------ helpers

        public Transform Node(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        public GameObject Part(VisitorBody b, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color, bool unlitMat = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            go.layer = layer;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
                Object.Destroy(col);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Mat(color, unlitMat);
            r.shadowCastingMode = unlitMat ? ShadowCastingMode.Off : shadow;
            r.receiveShadows = true;
            b.renderers.Add(r);
            return go;
        }

        public Material Mat(Color c, bool unlitMat)
        {
            string key = (unlitMat ? "u" : "l") + c.r.ToString("F3") + "," + c.g.ToString("F3") + "," + c.b.ToString("F3");
            if (cache.TryGetValue(key, out var m)) return m;
            var src = unlitMat ? unlit : lit;
            m = src != null ? new Material(src) : new Material(Shader.Find(unlitMat ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            cache[key] = m;
            return m;
        }
    }
}
