using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DontAnswer
{
    /// <summary>
    /// A visitor standing on the porch (or a guest on your couch): builds the body - a rigged character from the
    /// <see cref="CharacterLibrary"/> when the role is cast, primitives otherwise (Leo's sheet) - shows the tells
    /// (breath vapor, a shadow that moves on its own, eyeshine) and plays every behaviour the director asks for.
    /// </summary>
    public class VisitorAvatar : MonoBehaviour
    {
        public Material litMaterial;
        public Material unlitMaterial;
        public Material vaporMaterial;
        public CharacterLibrary characters;

        enum Mode { Hidden, Walking, Idle, Leaving, Monster, Sitting, Dragged }
        Mode mode = Mode.Hidden;

        public bool Visible => mode != Mode.Hidden;
        public bool IsSitting => mode == Mode.Sitting;
        public bool IsWalking => mode == Mode.Walking;
        public Vector3 HeadPosition => rigs.Count > 0 ? rigs[0].HeadWorld : bodies.Count > 0 ? bodies[0].HeadWorld : transform.position + Vector3.up * 1.6f;

        // primitive bodies (Leo, or anything without a rigged model)
        readonly List<VisitorBody> bodies = new List<VisitorBody>();
        VisitorBody twin;
        // rigged bodies
        readonly List<RiggedBody> rigs = new List<RiggedBody>();
        RiggedBody rigTwin;

        readonly List<Vector3> baseOffsets = new List<Vector3>();
        readonly List<ParticleSystem> vapor = new List<ParticleSystem>();
        readonly List<float> breathTimers = new List<float>();
        readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        PrimitiveBodyBuilder builder;

        VisitorLook look;
        bool isMimic;
        Tells tells;
        float t, seed, headYaw, headPitch, glanceTimer, glanceDir, nextGlance;
        float twitchTimer, twitchHold, twitchTilt, knockTime, talkTime, breathRate = 3.2f;
        float approach, maxApproach;
        bool watched, talking, panicked, sitFloor, stareOnly;
        Vector3 watcherPos, sitOffset;
        // Fists on the door; palms flat on a window; and the head that keeps tilting while you watch it.
        float pound, palms, palmsTarget, creep, creepSide = 1f;
        Vector3 palmPoint;

        // pose for the rigged bodies, worked out in Update and applied after animation in LateUpdate
        float poseYaw, posePitch, poseRoll, shiver, reveal, stretch, reach, lookBack;
        float revealRoll = 95f;   // how far the head rolls over in the reveal

        static readonly Color[] KidShirts = { new Color(0.12f, 0.1f, 0.18f), new Color(0.18f, 0.08f, 0.08f), new Color(0.08f, 0.12f, 0.12f) };
        static readonly float[] KidHeights = { 0.92f, 1.04f, 0.97f };
        const float MimicNeck = 0.035f;   // "never perfectly": their necks are a little too long

        public void CopyMaterialsFrom(VisitorAvatar other)
        {
            litMaterial = other.litMaterial;
            unlitMaterial = other.unlitMaterial;
            vaporMaterial = other.vaporMaterial;
            characters = other.characters;
        }

        // ================================================================== building

        public void Build(VisitorLook l, bool mimic, Tells mimicTells)
        {
            Clear();
            look = l;
            isMimic = mimic;
            tells = mimicTells;
            seed = Random.value * 100f;
            t = 0f;
            approach = 0f;
            headYaw = headPitch = 0f;
            poseYaw = posePitch = poseRoll = shiver = reveal = stretch = reach = 0f;
            glanceTimer = 0f;
            nextGlance = Random.Range(2f, 5f);
            twitchTimer = Random.Range(1f, 3f);
            maxApproach = Mathf.Max(0f, l.distanceFromDoor - 0.38f);
            panicked = false;
            breathRate = 3.2f;

            builder = new PrimitiveBodyBuilder(transform, litMaterial, unlitMaterial, mats, gameObject.layer);
            bool hideShadow = (tells & (Tells.NoShadow | Tells.WrongShadow)) != 0;
            var shadow = hideShadow ? ShadowCastingMode.Off : ShadowCastingMode.On;

            var role = characters != null && l.body != BodyShape.Ghost ? Casting.Find(l.modelKey) : null;
            if (role != null && BuildRigged(l, role, shadow))
            {
                mode = Mode.Idle;
                return;
            }

            int count = Mathf.Max(1, l.groupCount);
            for (int i = 0; i < count; i++)
            {
                var li = l;
                Vector3 offset = Vector3.zero;
                if (count > 1)
                {
                    li = Clone(l);
                    li.skin = Casting.MaskColors[i % Casting.MaskColors.Length];
                    li.shirt = KidShirts[i % KidShirts.Length];
                    offset = new Vector3((i - (count - 1) * 0.5f) * 0.46f, 0f, i == 1 ? 0.12f : 0f);
                }
                var b = builder.Build(li, offset, 1f, shadow, "Body" + i);
                bodies.Add(b);
                baseOffsets.Add(offset);
                if ((tells & Tells.NoBreath) == 0) AddVapor();
                if ((tells & Tells.Eyeshine) != 0) AddEyeshine(b);
            }

            if ((tells & Tells.WrongShadow) != 0)
            {
                twin = builder.Build(l, bodies[0].root.localPosition, 1f, ShadowCastingMode.ShadowsOnly, "ShadowTwin");
                twin.SetShadowCasting(ShadowCastingMode.ShadowsOnly);
            }
            mode = Mode.Idle;
        }

        static VisitorLook Clone(VisitorLook l) => JsonUtility.FromJson<VisitorLook>(JsonUtility.ToJson(l));

        bool BuildRigged(VisitorLook l, Role role, ShadowCastingMode shadow)
        {
            int count = Mathf.Max(1, l.groupCount);
            for (int i = 0; i < count; i++)
                if (characters.Prefab(role.Model(i)) == null) return false;

            for (int i = 0; i < count; i++)
            {
                Vector3 offset = count > 1 ? new Vector3((i - (count - 1) * 0.5f) * 0.5f, 0f, i == 1 ? 0.14f : 0f) : Vector3.zero;
                float height = l.height * (count > 1 ? KidHeights[i % KidHeights.Length] : 1f);
                var rb = RiggedBody.Create(characters, characters.Prefab(role.Model(i)), transform, "Body" + i, offset, height, gameObject.layer);
                Dress(rb, role, l, i, shadow);
                if (rb.groundDrop > 0f)
                {
                    offset += Vector3.down * rb.groundDrop;
                    rb.root.localPosition = offset;
                }
                rigs.Add(rb);
                baseOffsets.Add(offset);
                if ((tells & Tells.NoBreath) == 0) AddVapor();
                if ((tells & Tells.Eyeshine) != 0) AddEyeshine(rb);
            }

            if ((tells & Tells.WrongShadow) != 0)
            {
                // The shadow on the porch boards belongs to something else: it reaches for the door while the body stands still.
                rigTwin = RiggedBody.Create(characters, characters.Prefab(role.Model(0)), transform, "ShadowTwin", baseOffsets[0], l.height, gameObject.layer);
                rigTwin.SetShadowCasting(ShadowCastingMode.ShadowsOnly);
                rigTwin.Play(CharAnim.Knock, 0f, 0.35f);
            }
            return true;
        }

        // ------------------------------------------------------------------ dressing the rigged bodies

        void Dress(RiggedBody rb, Role role, VisitorLook l, int member, ShadowCastingMode shadow)
        {
            foreach (var r in rb.renderers)
            {
                if (r == null) continue;
                var shared = r.sharedMaterials;
                bool changed = false;
                for (int s = 0; s < shared.Length; s++)
                {
                    if (shared[s] == null) continue;
                    string matName = BaseName(shared[s].name);
                    var c = role.tint(l, matName, member);
                    if (isMimic && matName.StartsWith("Skin"))
                        c = Color.Lerp(c ?? shared[s].GetColor("_BaseColor"), new Color(0.6f, 0.59f, 0.57f), 0.28f);   // a little ashen
                    if (c == null) continue;
                    shared[s] = builder.Mat(c.Value, false);
                    changed = true;
                }
                if (changed) r.sharedMaterials = shared;
                r.shadowCastingMode = shadow;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            foreach (var p in role.props) AddProp(rb, p, l, member, shadow);
        }

        /// <summary>
        /// The top of the head, hair and all, as the body stands right now, in model space: its highest point, and the
        /// middle and half-size of the band just under it, where a cap's crown sits. Measured off the skinned mesh
        /// itself, so a cap fits whatever haircut the model came with.
        /// </summary>
        static (Vector3 center, float y, float halfWidth, float halfDepth) HeadTop(RiggedBody rb)
        {
            var pts = new List<Vector3>();
            var baked = new Mesh();
            float top = float.MinValue;
            foreach (var smr in rb.model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.BakeMesh(baked, true);   // with scale: this plus the renderer's matrix lands in world space
                var toWorld = smr.transform.localToWorldMatrix;
                foreach (var v in baked.vertices)
                {
                    var p = rb.model.InverseTransformPoint(toWorld.MultiplyPoint3x4(v));
                    pts.Add(p);
                    if (p.y > top) top = p.y;
                }
            }
            Destroy(baked);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in pts)
            {
                if (p.y < top - 0.09f || p.y > top - 0.02f) continue;
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
            }
            if (minX > maxX) return (new Vector3(0f, 0f, 0.03f), 1.8f, 0.12f, 0.13f);
            return (new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f), top, (maxX - minX) * 0.5f, (maxZ - minZ) * 0.5f);
        }

        /// <summary>Character material assets are named "glTFName_RRGGBB".</summary>
        static string BaseName(string n)
        {
            int k = n.IndexOf(" (Instance)", System.StringComparison.Ordinal);
            if (k >= 0) n = n.Substring(0, k);
            int u = n.LastIndexOf('_');
            if (u > 0 && n.Length - u == 7) n = n.Substring(0, u);
            return n;
        }

        void AddProp(RiggedBody rb, PropKind kind, VisitorLook l, int member, ShadowCastingMode shadow)
        {
            Material Lit(Color c) => builder.Mat(c, false);
            var black = Lit(new Color(0.012f, 0.01f, 0.01f));
            var gold = Lit(new Color(0.85f, 0.65f, 0.15f));
            var bone = Lit(new Color(0.9f, 0.89f, 0.83f));
            var head = rb.head;
            var none = ShadowCastingMode.Off;

            switch (kind)
            {
                case PropKind.Cap:
                {
                    // A baseball cap, fitted to this head: the crown covers the hair, the peak sits on the brow.
                    var top = HeadTop(rb);
                    var crown = new Vector3(top.center.x, top.y - 0.06f, top.center.z - 0.005f);
                    rb.Attach(head, PrimitiveType.Sphere, crown, new Vector3(top.halfWidth * 2.26f, 0.15f, top.halfDepth * 2.2f), Lit(l.hatColor), shadow);
                    float front = crown.z + top.halfDepth * 1.1f;
                    rb.Attach(head, PrimitiveType.Cube, new Vector3(crown.x, crown.y - 0.024f, front + 0.04f), new Vector3(0.19f, 0.014f, 0.11f), Lit(l.hatColor * 0.8f), shadow, new Vector3(11f, 0f, 0f));
                    break;
                }

                case PropKind.PoliceCap:
                {
                    // A peaked cap: a black band round the head, a wide flat crown over it, the visor and the badge in front.
                    var top = HeadTop(rb);
                    float cx = top.center.x, cz = top.center.z;
                    float band = top.y - 0.068f;
                    rb.Attach(head, PrimitiveType.Cylinder, new Vector3(cx, band, cz), new Vector3(top.halfWidth * 2.18f, 0.019f, top.halfDepth * 2.14f), black, shadow);
                    rb.Attach(head, PrimitiveType.Cylinder, new Vector3(cx, top.y - 0.022f, cz + 0.008f), new Vector3(top.halfWidth * 2.5f, 0.026f, top.halfDepth * 2.4f), Lit(l.hatColor), shadow);
                    float front = cz + top.halfDepth * 1.07f;
                    rb.Attach(head, PrimitiveType.Cube, new Vector3(cx, band - 0.016f, front + 0.035f), new Vector3(0.19f, 0.012f, 0.09f), black, shadow, new Vector3(14f, 0f, 0f));
                    rb.Attach(head, PrimitiveType.Cube, new Vector3(cx, top.y - 0.028f, cz + 0.008f + top.halfDepth * 1.2f), new Vector3(0.042f, 0.046f, 0.01f), gold, none);
                    break;
                }

                case PropKind.Badge:
                    rb.Attach(rb.chest, PrimitiveType.Cube, new Vector3(-0.085f, 1.39f, 0.195f), new Vector3(0.035f, 0.045f, 0.012f), gold, none);
                    break;

                case PropKind.PizzaBox:
                {
                    // Held flat on the left palm the way delivery drivers do; the right hand stays free to knock.
                    var holder = new GameObject("Pizza Box").transform;
                    holder.gameObject.layer = gameObject.layer;
                    holder.SetParent(rb.root, false);
                    holder.localScale = Vector3.one * rb.scale;
                    void Piece(Vector3 pos, Vector3 size, Color c)
                    {
                        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Destroy(go.GetComponent<Collider>());
                        go.layer = gameObject.layer;
                        go.transform.SetParent(holder, false);
                        go.transform.localPosition = pos;
                        go.transform.localScale = size;
                        var r = go.GetComponent<MeshRenderer>();
                        r.sharedMaterial = Lit(c);
                        r.shadowCastingMode = shadow;
                        rb.renderers.Add(r);
                    }
                    Piece(Vector3.zero, new Vector3(0.4f, 0.055f, 0.4f), new Color(0.8f, 0.74f, 0.62f));
                    Piece(new Vector3(0f, 0.029f, 0.02f), new Vector3(0.17f, 0.004f, 0.17f), new Color(0.72f, 0.09f, 0.07f));
                    Piece(new Vector3(0f, 0.029f, -0.12f), new Vector3(0.26f, 0.004f, 0.03f), new Color(0.12f, 0.1f, 0.08f));
                    rb.carried = holder;
                    rb.carrySide = -1;
                    break;
                }

                case PropKind.Blood:
                {
                    var blood = Lit(new Color(0.28f, 0.012f, 0.012f));
                    rb.Attach(rb.chest, PrimitiveType.Sphere, new Vector3(0.06f, 1.28f, 0.175f), new Vector3(0.13f, 0.17f, 0.04f), blood, none);
                    rb.Attach(rb.chest, PrimitiveType.Sphere, new Vector3(-0.07f, 1.1f, 0.165f), new Vector3(0.08f, 0.1f, 0.03f), blood, none);
                    if (rb.foreArmR != null && rb.wristR != null)
                    {
                        var a = rb.model.InverseTransformPoint(rb.foreArmR.position);
                        var w = rb.model.InverseTransformPoint(rb.wristR.position);
                        rb.Attach(rb.foreArmR, PrimitiveType.Sphere, Vector3.Lerp(a, w, 0.55f), Vector3.one * 0.085f, blood, none);
                        rb.Attach(rb.wristR, PrimitiveType.Sphere, w, Vector3.one * 0.06f, blood, none);
                    }
                    break;
                }

                case PropKind.SkullMask:
                    FaceMask(rb, bone, black, true, shadow);
                    break;

                case PropKind.Mask:
                    FaceMask(rb, Lit(Casting.MaskColors[member % Casting.MaskColors.Length]), black, false, shadow);
                    if (member == 0)
                        rb.Attach(head, PrimitiveType.Cylinder, new Vector3(0f, 1.8f, 0.12f), new Vector3(0.03f, 0.03f, 0.03f), Lit(new Color(0.2f, 0.25f, 0.1f)), none);
                    break;

                case PropKind.Glasses:
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float x = side * 0.042f;
                        rb.Attach(head, PrimitiveType.Cube, new Vector3(x, 1.675f, 0.226f), new Vector3(0.06f, 0.008f, 0.008f), black, none);
                        rb.Attach(head, PrimitiveType.Cube, new Vector3(x, 1.637f, 0.224f), new Vector3(0.056f, 0.006f, 0.008f), black, none);
                        rb.Attach(head, PrimitiveType.Cube, new Vector3(side * 0.072f, 1.656f, 0.22f), new Vector3(0.006f, 0.042f, 0.008f), black, none);
                    }
                    rb.Attach(head, PrimitiveType.Cube, new Vector3(0f, 1.664f, 0.228f), new Vector3(0.026f, 0.006f, 0.008f), black, none);
                    break;

                case PropKind.Ribs:
                    for (int i = 0; i < 4; i++)
                        rb.Attach(rb.chest, PrimitiveType.Cube, new Vector3(0f, 1.24f + 0.055f * i, 0.185f),
                            new Vector3(0.2f - 0.025f * Mathf.Abs(i - 1.5f), 0.018f, 0.01f), bone, none);
                    rb.Attach(rb.chest, PrimitiveType.Cube, new Vector3(0f, 1.33f, 0.19f), new Vector3(0.022f, 0.21f, 0.01f), bone, none);
                    break;

                case PropKind.LostBoy:
                {
                    // A little boy in his trick-or-treat tuxedo: a bow tie at the collar, a pumpkin bucket in his hand.
                    // A year out in the rain shows only in the face - soft bruised hollows under the eyes, a small grey
                    // mouth. Everything goes on in the rest pose first; then a child's proportions (a big head, narrow
                    // shoulders), which carry the props along with them.
                    var hollow = Lit(new Color(0.3f, 0.22f, 0.27f));
                    foreach (var e in new[] { RiggedBody.EyeL, RiggedBody.EyeR })
                        rb.AttachHead(PrimitiveType.Sphere, e + new Vector3(0f, -0.022f, -0.003f), new Vector3(0.042f, 0.013f, 0.01f), hollow, none);
                    rb.AttachHead(PrimitiveType.Sphere, RiggedBody.Mouth + new Vector3(0f, 0.002f, 0.001f), new Vector3(0.026f, 0.006f, 0.008f), Lit(new Color(0.32f, 0.17f, 0.18f)), none);

                    var tie = Lit(new Color(0.02f, 0.02f, 0.025f));
                    for (int side = -1; side <= 1; side += 2)
                        rb.Attach(rb.chest, PrimitiveType.Cube, new Vector3(side * 0.032f, 1.47f, 0.168f), new Vector3(0.05f, 0.038f, 0.014f), tie, none, new Vector3(0f, 0f, side * 18f));
                    rb.Attach(rb.chest, PrimitiveType.Cube, new Vector3(0f, 1.47f, 0.173f), new Vector3(0.018f, 0.022f, 0.016f), tie, none);

                    if (rb.wristR != null)
                    {
                        // The pumpkin bucket hangs from his right hand (placed under the hand every frame, so it swings straight down).
                        var holder = new GameObject("Pumpkin Bucket").transform;
                        holder.gameObject.layer = gameObject.layer;
                        holder.SetParent(rb.root, false);
                        holder.localScale = Vector3.one * rb.scale;
                        var orange = Lit(new Color(0.95f, 0.45f, 0.06f));
                        void Piece(PrimitiveType type, Vector3 pos, Vector3 size, Material m, float roll = 0f)
                        {
                            var go = GameObject.CreatePrimitive(type);
                            Destroy(go.GetComponent<Collider>());
                            go.layer = gameObject.layer;
                            go.transform.SetParent(holder, false);
                            go.transform.localPosition = pos;
                            go.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
                            go.transform.localScale = size;
                            var r = go.GetComponent<MeshRenderer>();
                            r.sharedMaterial = m;
                            r.shadowCastingMode = m == orange ? shadow : none;
                            rb.renderers.Add(r);
                        }
                        Piece(PrimitiveType.Cube, new Vector3(0f, -0.06f, 0f), new Vector3(0.008f, 0.12f, 0.008f), black);   // the handle
                        Piece(PrimitiveType.Sphere, new Vector3(0f, -0.2f, 0f), new Vector3(0.2f, 0.16f, 0.2f), orange);
                        Piece(PrimitiveType.Cylinder, new Vector3(0f, -0.15f, 0f), new Vector3(0.17f, 0.01f, 0.17f), Lit(new Color(0.2f, 0.08f, 0.02f)));   // the open top
                        for (int side = -1; side <= 1; side += 2)
                            Piece(PrimitiveType.Cube, new Vector3(side * 0.04f, -0.19f, 0.094f), new Vector3(0.032f, 0.032f, 0.01f), black, 45f);
                        Piece(PrimitiveType.Cube, new Vector3(0f, -0.235f, 0.09f), new Vector3(0.08f, 0.016f, 0.01f), black);
                        rb.hanging = holder;
                    }

                    if (rb.chest != null) rb.chest.localScale *= 0.9f;   // a child's shoulders
                    if (head != null) head.localScale *= 1.42f;          // and a child's big head (net about 1.28x)
                    // A child's short legs. The hips stay where the clips put them, so lower the whole body by however
                    // far the ankles came up.
                    if (rb.legL != null && rb.legR != null && rb.legEndL != null)
                    {
                        float before = rb.legEndL.position.y;
                        rb.legL.localScale *= 0.8f;
                        rb.legR.localScale *= 0.8f;
                        rb.groundDrop = (rb.legEndL.position.y - before) * 1.08f;
                    }
                    break;
                }
            }
        }

        /// <summary>A plate over the face with holes for eyes and a grin: Tom's skull, the children's masks.</summary>
        void FaceMask(RiggedBody rb, Material plate, Material black, bool skull, ShadowCastingMode shadow)
        {
            var c = new Vector3(0f, 1.655f, 0.18f);
            var half = new Vector3(0.108f, 0.13f, 0.06f);
            float Front(float x, float y)
            {
                float k = 1f - (x * x) / (half.x * half.x) - ((y - c.y) * (y - c.y)) / (half.y * half.y);
                return c.z + half.z * Mathf.Sqrt(Mathf.Max(0.02f, k));
            }
            rb.Attach(rb.head, PrimitiveType.Sphere, c, half * 2f, plate, shadow);
            var none = ShadowCastingMode.Off;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.043f, y = 1.672f;
                rb.Attach(rb.head, PrimitiveType.Sphere, new Vector3(x, y, Front(x, y) - 0.008f), skull ? new Vector3(0.062f, 0.066f, 0.03f) : new Vector3(0.056f, 0.044f, 0.03f), black, none);
            }
            float my = skull ? 1.585f : 1.6f;
            rb.Attach(rb.head, PrimitiveType.Cube, new Vector3(0f, my, Front(0f, my) - 0.004f), skull ? new Vector3(0.1f, 0.008f, 0.012f) : new Vector3(0.12f, 0.03f, 0.012f), black, none);
            if (!skull) return;
            rb.Attach(rb.head, PrimitiveType.Sphere, new Vector3(0f, 1.625f, Front(0f, 1.625f) - 0.004f), new Vector3(0.024f, 0.03f, 0.02f), black, none);
            for (int i = -2; i <= 2; i++)
            {
                float x = i * 0.02f;
                rb.Attach(rb.head, PrimitiveType.Cube, new Vector3(x, my, Front(x, my) - 0.004f), new Vector3(0.004f, 0.03f, 0.012f), black, none);
            }
        }

        void AddVapor()
        {
            if (vaporMaterial == null) return;
            var go = new GameObject("Breath");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 1.9f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
            main.startColor = new Color(0.86f, 0.9f, 0.95f, 0.4f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 240;
            main.gravityModifier = -0.015f;
            var em = ps.emission;
            em.enabled = false;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 3f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.7f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = vaporMaterial;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            vapor.Add(ps);
            breathTimers.Add(Random.Range(0.5f, 2.5f));
        }

        void AddEyeshine(VisitorBody b)
        {
            float s = b.headSize;
            float ex = b.ghost ? b.rx * 0.36f : s * 0.19f;
            float ey = b.faceCY + (b.ghost ? b.ry * 0.18f : s * 0.06f);
            var shine = new Color(1.5f, 1.45f, 0.75f);
            for (int side = -1; side <= 1; side += 2)
            {
                var go = builder.Part(b, PrimitiveType.Sphere, b.head, b.OnFace(side * ex, ey, -s * 0.02f), Vector3.one * s * 0.07f, shine, true);
                go.name = "Eyeshine";
            }
        }

        void AddEyeshine(RiggedBody rb)
        {
            var shine = builder.Mat(new Color(1.5f, 1.45f, 0.75f), true);
            foreach (var e in new[] { RiggedBody.EyeL, RiggedBody.EyeR })
                rb.AttachHead(PrimitiveType.Sphere, e + new Vector3(0f, 0f, 0.012f), Vector3.one * 0.03f, shine, ShadowCastingMode.Off).name = "Eyeshine";
        }

        public void Hide()
        {
            Clear();
            mode = Mode.Hidden;
        }

        void Clear()
        {
            StopAllCoroutines();
            foreach (var b in bodies) if (b.root != null) Destroy(b.root.gameObject);
            bodies.Clear();
            if (twin != null && twin.root != null) Destroy(twin.root.gameObject);
            twin = null;
            foreach (var rb in rigs) rb.Destroy();
            rigs.Clear();
            rigTwin?.Destroy();
            rigTwin = null;
            baseOffsets.Clear();
            foreach (var ps in vapor) if (ps != null) Destroy(ps.gameObject);
            vapor.Clear();
            breathTimers.Clear();
            foreach (var m in mats.Values) if (m != null) Destroy(m);
            mats.Clear();
            watched = talking = stareOnly = false;
            knockTime = talkTime = 0f;
            pound = palms = palmsTarget = creep = 0f;
            creepSide = Random.value < 0.5f ? -1f : 1f;
        }

        void OnDestroy()
        {
            foreach (var rb in rigs) rb.Destroy();
            rigTwin?.Destroy();
            foreach (var m in mats.Values) if (m != null) Destroy(m);
        }

        // ================================================================== commands

        public void SetWatched(bool isWatched, Vector3 watcherPosition)
        {
            watched = isWatched;
            watcherPos = watcherPosition;
        }

        /// <summary>Turns its head to follow someone, without creeping any closer (a face at the glass).</summary>
        public void StareAt(Vector3 target)
        {
            watched = true;
            stareOnly = true;
            watcherPos = target;
        }

        /// <summary>Breath vapor is for the cold outside; indoors it's switched off.</summary>
        public void SetBreathVisible(bool on)
        {
            foreach (var ps in vapor) if (ps != null) ps.gameObject.SetActive(on);
        }

        public void SetTalking(bool on) => talking = on;
        public void SetPanicked(bool on) { panicked = on; breathRate = on ? 1.4f : 3.2f; }
        public void PlayKnock(float duration) => knockTime = Mathf.Max(knockTime, duration);

        /// <summary>Hammering on the door with a fist, not knocking.</summary>
        public void PlayPound(float duration)
        {
            pound = Mathf.Max(pound, duration);
            knockTime = Mathf.Max(knockTime, duration);   // the primitive bodies only know how to knock
        }

        /// <summary>Both palms flat on the glass in front of it (any point on the pane), or taken away.</summary>
        public void SetPalmsOnGlass(bool on, Vector3 pointOnGlass)
        {
            palmsTarget = on ? 1f : 0f;
            if (on) palmPoint = pointOnGlass;
        }

        /// <summary>Its eyes catch the light like an animal's, from now on (staring into the peephole, for one).</summary>
        public void GlowEyes()
        {
            if ((tells & Tells.Eyeshine) != 0) return;
            tells |= Tells.Eyeshine;
            foreach (var rb in rigs) AddEyeshine(rb);
            foreach (var b in bodies) AddEyeshine(b);
        }

        /// <summary>Moves the whole body so its eyes end up at a point (a face at a window, an eye at the peephole).</summary>
        public void PutEyesAt(Vector3 eyes, float follow = 1f)
        {
            if (!Visible) return;
            transform.position += (eyes - HeadPosition) * Mathf.Clamp01(follow);
        }

        public void Arrive(Vector3 from, Vector3 standPos, Quaternion standRot, float duration)
        {
            StopAllCoroutines();
            StartCoroutine(ArriveRoutine(from, standPos, standRot, duration));
        }

        IEnumerator ArriveRoutine(Vector3 from, Vector3 standPos, Quaternion standRot, float duration)
        {
            mode = Mode.Walking;
            var dir = standPos - from; dir.y = 0f;
            transform.SetPositionAndRotation(from, Quaternion.LookRotation(dir.normalized, Vector3.up));
            bool glide = (tells & Tells.SilentSteps) != 0 && isMimic;   // it doesn't walk. It arrives.
            float metresPerSecond = Vector3.Distance(from, standPos) / Mathf.Max(0.01f, duration);
            foreach (var rb in rigs) rb.Play(glide ? CharAnim.Idle : CharAnim.Walk, 0.15f, glide ? 0f : rb.WalkRate(metresPerSecond));
            float k = 0f, walked = 0f, step = 0f;
            Vector3 prev = from;
            while (k < 1f)
            {
                k += Time.deltaTime / Mathf.Max(0.01f, duration);
                var p = Vector3.Lerp(from, standPos, k);
                transform.position = p;
                walked += Time.deltaTime;
                if (glide)
                {
                    for (int i = 0; i < bodies.Count; i++) bodies[i].root.localPosition = baseOffsets[i];
                }
                else
                {
                    if (rigs.Count == 0) WalkPose(walked);
                    step += Vector3.Distance(p, prev);
                    if (step > 0.62f && AudioManager.Instance != null)
                    {
                        step = 0f;
                        AudioManager.Instance.Footstep(p, 0.6f, 0.9f);
                    }
                }
                prev = p;
                if (k > 0.7f) transform.rotation = Quaternion.Slerp(transform.rotation, standRot, Time.deltaTime * 6f);
                if (k > 0.85f) foreach (var rb in rigs) rb.Play(CharAnim.Idle, 0.35f);
                yield return null;
            }
            transform.SetPositionAndRotation(standPos, standRot);
            ResetPose();
            mode = Mode.Idle;
        }

        public void Leave(Vector3 exitPoint, float duration)
        {
            StopAllCoroutines();
            StartCoroutine(LeaveRoutine(exitPoint, duration));
        }

        IEnumerator LeaveRoutine(Vector3 exit, float duration)
        {
            mode = Mode.Leaving;
            ResetPose();
            lookBack = 0f;
            Vector3 start = transform.position;
            var away = exit - start; away.y = 0f;
            Quaternion faceAway = Quaternion.LookRotation(away.normalized, Vector3.up);
            Quaternion faceHouse = transform.rotation;
            float glanceSide = Random.value < 0.5f ? -1f : 1f;

            foreach (var rb in rigs) rb.Play(CharAnim.Walk, 0.25f, 0.55f);
            yield return TurnTo(faceAway, 0.55f);

            float dist = Vector3.Distance(start, exit);
            float speed = dist / Mathf.Max(0.01f, duration);
            float k = 0f, walked = 0f, step = 0f;
            bool stared = false;
            Vector3 prev = start;
            while (k < 1f)
            {
                k += Time.deltaTime / Mathf.Max(0.01f, duration);
                var p = Vector3.Lerp(start, exit, k);
                transform.position = p;
                walked += Time.deltaTime;
                foreach (var rb in rigs) rb.Play(CharAnim.Walk, 0.3f, rb.WalkRate(speed));
                if (rigs.Count == 0) WalkPose(walked);
                // People glance back once over their shoulder. It never does.
                lookBack = !isMimic && k > 0.2f && k < 0.45f ? Mathf.Sin((k - 0.2f) / 0.25f * Mathf.PI) * 65f * glanceSide : 0f;
                step += Vector3.Distance(p, prev);
                if (step > 0.62f && AudioManager.Instance != null && !(isMimic && (tells & Tells.SilentSteps) != 0))
                {
                    step = 0f;
                    AudioManager.Instance.Footstep(p, 0.45f, 0.9f);
                }
                prev = p;
                if (isMimic && !stared && k >= 0.5f)
                {
                    // It stops at the edge of the light, turns round, and looks at your door for a while.
                    stared = true;
                    foreach (var rb in rigs) rb.Play(CharAnim.Idle, 0.3f, 0.1f);
                    yield return TurnTo(faceHouse, 0.9f);
                    yield return new WaitForSeconds(2.2f);
                    foreach (var rb in rigs) rb.Play(CharAnim.Walk, 0.25f, 0.5f);
                    yield return TurnTo(faceAway, 0.6f);
                }
                yield return null;
            }
            Hide();
        }

        IEnumerator TurnTo(Quaternion target, float time)
        {
            Quaternion from = transform.rotation;
            for (float a = 0f; a < 1f; a += Time.deltaTime / Mathf.Max(0.01f, time))
            {
                transform.rotation = Quaternion.Slerp(from, target, Mathf.SmoothStep(0f, 1f, a));
                yield return null;
            }
            transform.rotation = target;
        }

        /// <summary>Stops whatever it was doing (walking, leaving) where it stands.</summary>
        public void Halt()
        {
            StopAllCoroutines();
            if (mode == Mode.Walking || mode == Mode.Leaving) mode = Mode.Idle;
            foreach (var rb in rigs) rb.Play(CharAnim.Idle, 0.25f);
        }

        /// <summary>Gets up off its seat and stands at a point on the floor (a guest getting to their feet).</summary>
        public void StandUp(Vector3 standAt)
        {
            StopAllCoroutines();
            ResetPose();
            transform.position = standAt;
            mode = Mode.Idle;
            foreach (var rb in rigs) rb.Play(CharAnim.Idle, 0.2f);
        }

        /// <summary>Walks a route through the house at a walking pace, turning along it.</summary>
        public IEnumerator WalkPath(IList<Vector3> points, float speed)
        {
            mode = Mode.Walking;
            lookBack = 0f;
            foreach (var rb in rigs) rb.Play(CharAnim.Walk, 0.2f, rb.WalkRate(speed));
            float step = 0f;
            foreach (var target in points)
            {
                while (true)
                {
                    var to = target - transform.position;
                    to.y = 0f;
                    float dist = to.magnitude;
                    if (dist < 0.04f) break;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to / dist, Vector3.up), Time.deltaTime * 7f);
                    float move = Mathf.Min(dist, speed * Time.deltaTime);
                    transform.position += to / dist * move;
                    if (rigs.Count == 0) WalkPose(t * 2f);
                    step += move;
                    if (step > 0.62f && AudioManager.Instance != null)
                    {
                        step = 0f;
                        AudioManager.Instance.Footstep(transform.position, 0.5f, 1f);
                    }
                    yield return null;
                }
            }
            mode = Mode.Idle;
            foreach (var rb in rigs) rb.Play(CharAnim.Idle, 0.25f);
        }

        /// <summary>A full sprint to a point, facing where it runs. Used when something sees an open door.</summary>
        public IEnumerator RunTo(Vector3 target, float speed)
        {
            mode = Mode.Walking;
            lookBack = 0f;
            foreach (var rb in rigs) rb.Play(CharAnim.Run, 0.12f, speed / (3.6f * rb.scale));
            float step = 0f;
            while (true)
            {
                var to = target - transform.position;
                float dist = to.magnitude;
                if (dist < 0.05f) break;
                var flat = new Vector3(to.x, 0f, to.z);
                if (flat.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat.normalized, Vector3.up), Time.deltaTime * 12f);
                float move = Mathf.Min(dist, speed * Time.deltaTime);
                transform.position += to / dist * move;
                if (rigs.Count == 0) WalkPose(t * 2f);
                step += move;
                if (step > 0.9f && AudioManager.Instance != null)
                {
                    step = 0f;
                    AudioManager.Instance.Footstep(transform.position, 0.9f, 0.85f);
                }
                yield return null;
            }
            transform.position = target;
        }

        /// <summary>Something pulls them off the porch into the dark.</summary>
        public IEnumerator DraggedAway(Vector3 direction, float distance, float duration)
        {
            mode = Mode.Dragged;
            foreach (var rb in rigs) rb.Play(CharAnim.Death, 0.08f, 1.5f, true);
            Vector3 start = transform.position;
            for (float k = 0f; k < 1f; k += Time.deltaTime / duration)
            {
                float e = k * k;
                transform.position = start + direction.normalized * distance * e + Vector3.down * 0.4f * e;
                foreach (var b in bodies)
                {
                    b.root.localRotation = Quaternion.Euler(-35f * Mathf.Min(1f, k * 3f), 0f, Mathf.Sin(t * 30f) * 8f);
                    if (b.armL != null)
                    {
                        b.armL.localRotation = Quaternion.Euler(-150f + Mathf.Sin(t * 25f) * 30f, 0f, -20f);
                        b.armR.localRotation = Quaternion.Euler(-140f + Mathf.Cos(t * 23f) * 30f, 0f, 20f);
                    }
                }
                yield return null;
            }
            Hide();
        }

        void WalkPose(float time)
        {
            float s = Mathf.Sin(time * 7f);
            for (int i = 0; i < bodies.Count; i++)
            {
                var b = bodies[i];
                b.root.localPosition = baseOffsets[i] + new Vector3(0f, Mathf.Abs(s) * 0.03f, 0f);
                if (b.legL != null)
                {
                    b.legL.localRotation = Quaternion.Euler(s * 22f, 0f, 0f);
                    b.legR.localRotation = Quaternion.Euler(-s * 22f, 0f, 0f);
                }
                if (b.armL != null && !look.pizzaBox)
                {
                    b.armL.localRotation = b.armBaseL * Quaternion.Euler(-s * 18f, 0f, 0f);
                    b.armR.localRotation = b.armBaseR * Quaternion.Euler(s * 18f, 0f, 0f);
                }
            }
        }

        void ResetPose()
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                var b = bodies[i];
                b.root.localPosition = baseOffsets[i];
                b.root.localRotation = Quaternion.identity;
                if (b.legL != null) { b.legL.localRotation = Quaternion.identity; b.legR.localRotation = Quaternion.identity; }
                if (b.armL != null) { b.armL.localRotation = b.armBaseL; b.armR.localRotation = b.armBaseR; }
            }
            for (int i = 0; i < rigs.Count; i++)
            {
                rigs[i].root.localPosition = baseOffsets[i];
                rigs[i].root.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// The reveal, played by the death sequence: the head rolls over (by <paramref name="roll"/> degrees), the neck
        /// stretches, the eyes burn and the mouth opens.
        /// </summary>
        public IEnumerator Reveal(float duration, float roll = 95f)
        {
            revealRoll = roll;
            if (rigs.Count > 0) { yield return RevealRigged(duration); yield break; }
            if (bodies.Count == 0) yield break;
            mode = Mode.Monster;
            var b = bodies[0];
            var glow = builder.Mat(new Color(4f, 0.12f, 0.06f), true);
            foreach (var r in b.eyes) { r.sharedMaterial = glow; r.transform.localScale *= 1.4f; }
            foreach (var m in b.mouth) m.gameObject.SetActive(false);

            float s = b.headSize;
            var maw = builder.Part(b, PrimitiveType.Sphere, b.head, b.OnFace(0f, b.faceCY - (b.ghost ? b.ry * 0.35f : s * 0.26f), s * 0.06f),
                new Vector3(s * 0.2f, s * 0.1f, s * 0.15f), new Color(0.01f, 0f, 0f));
            Transform neck = b.head.parent;
            GameObject neckExt = null;
            if (!b.ghost && neck != null)
            {
                neckExt = builder.Part(b, PrimitiveType.Cylinder, neck, new Vector3(0f, 0.05f, 0f), new Vector3(0.08f, 0.01f, 0.08f), look.skin * 0.85f);
            }
            Vector3 headStart = b.head.localPosition;
            for (float k = 0f; k < 1f; k += Time.deltaTime / duration)
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                b.head.localRotation = Quaternion.Euler(-10f * e, Mathf.Sin(k * 40f) * 4f * e, roll * e);
                float st = b.ghost ? 0f : 0.32f * e;
                b.head.localPosition = headStart + Vector3.up * st;
                if (neckExt != null)
                {
                    neckExt.transform.localScale = new Vector3(0.08f, Mathf.Max(0.01f, st * 0.5f + 0.02f), 0.08f);
                    neckExt.transform.localPosition = new Vector3(0f, headStart.y + st * 0.5f, 0f);
                }
                maw.transform.localScale = Vector3.Lerp(new Vector3(s * 0.2f, s * 0.1f, s * 0.15f), new Vector3(s * 0.62f, s * 0.75f, s * 0.24f), e);
                if (b.armL != null)
                {
                    b.armL.localRotation = Quaternion.Slerp(b.armBaseL, Quaternion.Euler(-100f, 0f, -14f), e);
                    b.armR.localRotation = Quaternion.Slerp(b.armBaseR, Quaternion.Euler(-100f, 0f, 14f), e);
                }
                yield return null;
            }
        }

        IEnumerator RevealRigged(float duration)
        {
            mode = Mode.Monster;
            var rb = rigs[0];
            rb.Play(CharAnim.Idle, 0.12f, 0f);
            var glow = builder.Mat(new Color(4f, 0.12f, 0.06f), true);
            var black = builder.Mat(Color.black, true);   // unlit: a hole, not a surface
            foreach (var e in new[] { RiggedBody.EyeL, RiggedBody.EyeR })
                rb.AttachHead(PrimitiveType.Sphere, e + new Vector3(0f, 0f, 0.012f), Vector3.one * 0.046f, glow, ShadowCastingMode.Off);
            var maw = rb.AttachHead(PrimitiveType.Sphere, RiggedBody.Mouth + new Vector3(0f, -0.012f, 0.004f), new Vector3(0.05f, 0.02f, 0.035f), black, ShadowCastingMode.Off);
            var mawScale = maw.transform.localScale;
            for (float k = 0f; k < 1f; k += Time.deltaTime / duration)
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                reveal = e;
                stretch = 0.3f * e;
                reach = e;
                maw.transform.localScale = Vector3.Scale(mawScale, Vector3.Lerp(Vector3.one, new Vector3(2.3f, 6.5f, 1.5f), e));
                yield return null;
            }
            reveal = reach = 1f;
            stretch = 0.3f;
            maw.transform.localScale = Vector3.Scale(mawScale, new Vector3(2.3f, 6.5f, 1.5f));   // all the way open, however short the reveal
        }

        public void Sit(Transform seat)
        {
            StopAllCoroutines();
            mode = Mode.Sitting;
            transform.SetPositionAndRotation(seat.position, seat.rotation);
            foreach (var ps in vapor) if (ps != null) ps.gameObject.SetActive(false);   // it's warm inside
            if (rigs.Count > 0)
            {
                var rb = rigs[0];
                sitFloor = seat.position.y < 0.25f;
                // Seat markers sit at the frame of the seat; the cushion is higher, so hips ride a little above it.
                rb.root.localPosition = new Vector3(0f, -rb.hipHeight + (sitFloor ? 0.1f : 0.16f) * rb.scale, -0.12f * rb.scale);
                rb.Play(CharAnim.Idle, 0f, 0.7f);
                return;
            }
            if (bodies.Count == 0) return;
            var b = bodies[0];
            if (b.legL != null)
            {
                sitOffset = new Vector3(0f, -b.legLength + 0.05f, 0f);
                b.legL.localRotation = Quaternion.Euler(-85f, 0f, -4f);
                b.legR.localRotation = Quaternion.Euler(-85f, 0f, 4f);
            }
            else sitOffset = new Vector3(0f, -0.08f, 0f);
            b.root.localPosition = sitOffset;
        }

        // ================================================================== animation

        void Update()
        {
            if (mode == Mode.Hidden || (bodies.Count == 0 && rigs.Count == 0)) return;
            float dt = Time.deltaTime;
            t += dt;
            bool rigged = rigs.Count > 0;
            switch (mode)
            {
                case Mode.Idle:
                    if (rigged) AnimateIdleRigged(dt); else AnimateIdle(dt);
                    break;
                case Mode.Sitting:
                    if (rigged) AnimateSittingRigged(dt); else AnimateSitting(dt);
                    break;
                case Mode.Monster:
                    if (rigged) rigs[0].root.localPosition = baseOffsets[0] + Random.insideUnitSphere * 0.012f;
                    else bodies[0].root.localPosition = Random.insideUnitSphere * 0.012f;
                    break;
            }
            if (mode != Mode.Sitting) Breathe(dt);
            if (twin != null && twin.root != null) AnimateTwin(dt);
        }

        void LateUpdate()
        {
            if (mode == Mode.Hidden || rigs.Count == 0) return;
            float dt = Time.deltaTime;
            foreach (var rb in rigs) rb.Tick(dt);
            rigTwin?.Tick(dt);
            PoseRigs();
        }

        /// <summary>Bends the rigged bodies after the clips have been applied.</summary>
        void PoseRigs()
        {
            var f = transform;
            for (int i = 0; i < rigs.Count; i++)
            {
                var rb = rigs[i];
                switch (mode)
                {
                    case Mode.Sitting:
                        rb.Sit(f, sitFloor, Mathf.Sin(t * 1.9f) * 0.004f);
                        rb.Look(f, poseYaw, 8f, poseRoll);
                        break;
                    case Mode.Monster:
                        rb.Stretch(f, MimicNeck + stretch);
                        rb.Look(f, Mathf.Sin(t * 40f) * 4f * reveal, -10f * reveal, revealRoll * reveal);
                        rb.Reach(f, reach);
                        break;
                    case Mode.Idle:
                    {
                        float phase = i * 1.7f;
                        if (isMimic) rb.Stretch(f, MimicNeck);
                        rb.Look(f, poseYaw + (rigs.Count > 1 ? Mathf.Sin(t * 0.3f + phase) * 4f : 0f), posePitch, poseRoll);
                        if (shiver > 0f)
                        {
                            RiggedBody.Turn(rb.chest, f.forward, Mathf.Sin(t * 43f + phase) * 1.3f * shiver);
                            RiggedBody.Turn(rb.chest, f.right, Mathf.Sin(t * 31f) * 0.8f * shiver);
                        }
                        if (palms > 0f) rb.PressPalms(f, palmPoint, palms);
                        break;
                    }
                    default:
                        if (isMimic) rb.Stretch(f, MimicNeck);
                        rb.Look(f, lookBack, 0f, 0f);
                        break;
                }
                if (rb.carried != null)
                {
                    bool holding = mode != Mode.Monster && mode != Mode.Dragged && mode != Mode.Sitting;
                    if (rb.carried.gameObject.activeSelf != holding) rb.carried.gameObject.SetActive(holding);
                    if (holding) rb.Carry(f);
                }
                if (rb.hanging != null && rb.wristR != null)
                {
                    bool holding = mode != Mode.Monster && mode != Mode.Dragged && mode != Mode.Sitting;
                    if (rb.hanging.gameObject.activeSelf != holding) rb.hanging.gameObject.SetActive(holding);
                    var ahead = f.forward;
                    ahead.y = 0f;
                    if (holding && ahead.sqrMagnitude > 0.0001f)
                        rb.hanging.SetPositionAndRotation(rb.wristR.position + Vector3.down * 0.045f * rb.scale, Quaternion.LookRotation(ahead.normalized, Vector3.up));
                }
            }
            if (rigTwin != null)
            {
                rigTwin.root.localPosition = rigs[0].root.localPosition;
                rigTwin.root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.9f) * 9f);
                rigTwin.Look(f, Mathf.Sin(t * 0.5f) * 80f, 0f, Mathf.Sin(t * 1.3f) * 25f);
            }
        }

        void Breathe(float dt)
        {
            int count = rigs.Count > 0 ? rigs.Count : bodies.Count;
            for (int i = 0; i < vapor.Count && i < count; i++)
            {
                breathTimers[i] -= dt;
                if (breathTimers[i] > 0f) continue;
                breathTimers[i] = breathRate * Random.Range(0.8f, 1.2f);
                var p = rigs.Count > 0 ? rigs[i].MouthWorld : bodies[i].MouthWorld;
                var fwd = transform.forward;
                for (int n = 0; n < 9; n++)
                {
                    vapor[i].Emit(new ParticleSystem.EmitParams
                    {
                        position = p + Random.insideUnitSphere * 0.02f,
                        velocity = fwd * Random.Range(0.25f, 0.45f) + Random.insideUnitSphere * 0.05f + Vector3.up * 0.04f,
                        applyShapeToPosition = false
                    }, 1);
                }
            }
        }

        void AnimateIdleRigged(float dt)
        {
            Vector3 offset = Vector3.zero, euler = Vector3.zero;
            float tilt = look.headTilt, yawTarget = 0f, pitchTarget = 0f, speed = 1f;
            shiver = 0f;

            var idle = panicked ? IdleStyle.Nervous : look.idle;
            switch (idle)
            {
                case IdleStyle.Breathe:
                    euler.y = Mathf.Sin(t * 0.37f + seed) * 2f;
                    Glance(dt, ref yawTarget, 35f, 4f, 8f);
                    break;
                case IdleStyle.Shiver:
                    speed = 1.2f;
                    shiver = 1f;
                    Glance(dt, ref yawTarget, 50f, 3f, 6f);
                    break;
                case IdleStyle.Sway:
                    speed = 0.8f;
                    euler = new Vector3(Mathf.Sin(t * 0.7f) * 2.5f, 0f, Mathf.Sin(t * 0.9f + seed) * 4f);
                    tilt += Mathf.Sin(t * 0.9f + seed + 1f) * 7f;
                    break;
                case IdleStyle.Nervous:
                    speed = panicked ? 2f : 1.35f;
                    offset.x = Mathf.Sin(t * (panicked ? 2.2f : 0.8f) + seed) * 0.03f;
                    Glance(dt, ref yawTarget, 70f, panicked ? 0.6f : 2.5f, panicked ? 1.6f : 5.5f);
                    break;
                case IdleStyle.Still:
                    speed = 0.15f;   // it barely moves. People never stand this still.
                    break;
                case IdleStyle.Twitch:
                    speed = 0f;
                    twitchTimer -= dt;
                    if (twitchTimer <= 0f)
                    {
                        twitchTilt = Random.Range(-40f, 40f);
                        twitchHold = Random.Range(0.08f, 0.2f);
                        twitchTimer = Random.Range(1.2f, 3.5f);
                    }
                    if (twitchHold > 0f) { twitchHold -= dt; tilt += twitchTilt; }
                    break;
            }

            if (talking)
            {
                talkTime += dt;
                pitchTarget += Mathf.Sin(talkTime * 9f) * 3f;
                if (!isMimic) yawTarget = 0f;
            }

            if (watched && isMimic)
            {
                // It knows you're looking. It stares straight at you and (at the door) creeps closer,
                // and the longer you look, the further its head leans over. Look away and it's upright again.
                yawTarget = YawToward(watcherPos);
                pitchTarget = PitchToward(watcherPos);
                if (!stareOnly) approach = Mathf.MoveTowards(approach, maxApproach, dt * 0.05f);
                creep = Mathf.MoveTowards(creep, 1f, dt * 0.055f);
            }
            else creep = Mathf.MoveTowards(creep, 0f, dt * 0.6f);
            if (isMimic) tilt += creep * creep * 52f * creepSide;

            bool snappy = look.idle == IdleStyle.Still || look.idle == IdleStyle.Twitch;
            headYaw = Mathf.LerpAngle(headYaw, yawTarget, dt * (snappy ? 30f : 5f));
            headPitch = Mathf.Lerp(headPitch, pitchTarget, dt * 3f);
            poseYaw = headYaw;
            posePitch = headPitch;
            poseRoll = tilt;

            for (int i = 0; i < rigs.Count; i++)
            {
                var rb = rigs[i];
                if (i == 0 && pound > 0f) rb.Play(CharAnim.Pound, 0.1f, 1.45f);
                else if (i == 0 && knockTime > 0f) rb.Play(CharAnim.Knock, 0.15f, panicked ? 1.8f : 1.15f);
                else rb.Play(CharAnim.Idle, 0.3f, speed);
                rb.root.localPosition = baseOffsets[i] + (i == 0 ? offset : Vector3.zero) + Vector3.forward * approach;
                rb.root.localRotation = Quaternion.Euler(euler);
            }
            if (knockTime > 0f) knockTime -= dt;
            if (pound > 0f) pound -= dt;
            palms = Mathf.MoveTowards(palms, palmsTarget, dt * 3.5f);
        }

        void AnimateSittingRigged(float dt)
        {
            float yawTarget = 0f;
            Glance(dt, ref yawTarget, 45f, 3f, 7f);
            if (watched) yawTarget = 0f;
            headYaw = Mathf.LerpAngle(headYaw, yawTarget, dt * 4f);
            poseYaw = headYaw;
            poseRoll = look.idle == IdleStyle.Sway ? Mathf.Sin(t) * 6f : 0f;
            rigs[0].Play(CharAnim.Idle, 0.3f, 0.7f);
        }

        void AnimateIdle(float dt)
        {
            float breath = 0f;
            Vector3 offset = Vector3.zero, euler = Vector3.zero;
            float tilt = look.headTilt, yawTarget = 0f, pitchTarget = 0f;

            var idle = look.idle;
            if (panicked) idle = IdleStyle.Nervous;
            switch (idle)
            {
                case IdleStyle.Breathe:
                    breath = Mathf.Sin(t * 2.1f) * 0.012f;
                    euler.y = Mathf.Sin(t * 0.37f + seed) * 2f;
                    Glance(dt, ref yawTarget, 35f, 4f, 8f);
                    break;
                case IdleStyle.Shiver:
                    breath = Mathf.Sin(t * 3.4f) * 0.01f;
                    offset = new Vector3(Mathf.PerlinNoise(t * 18f, seed) - 0.5f, 0f, Mathf.PerlinNoise(seed, t * 18f) - 0.5f) * 0.014f;
                    tilt += Mathf.Sin(t * 21f) * 1.5f;
                    Glance(dt, ref yawTarget, 50f, 3f, 6f);
                    break;
                case IdleStyle.Sway:
                    breath = Mathf.Sin(t * 1.8f) * 0.012f;
                    euler = new Vector3(Mathf.Sin(t * 0.7f) * 2.5f, 0f, Mathf.Sin(t * 0.9f + seed) * 5f);
                    tilt += Mathf.Sin(t * 0.9f + seed + 1f) * 7f;
                    break;
                case IdleStyle.Nervous:
                    breath = Mathf.Sin(t * (panicked ? 6f : 3.3f)) * 0.016f;
                    offset.x = Mathf.Sin(t * (panicked ? 2.2f : 0.8f) + seed) * 0.03f;
                    Glance(dt, ref yawTarget, 85f, panicked ? 0.6f : 2.5f, panicked ? 1.6f : 5.5f);
                    break;
                case IdleStyle.Still:
                    break;
                case IdleStyle.Twitch:
                    twitchTimer -= dt;
                    if (twitchTimer <= 0f)
                    {
                        twitchTilt = Random.Range(-40f, 40f);
                        twitchHold = Random.Range(0.08f, 0.2f);
                        twitchTimer = Random.Range(1.2f, 3.5f);
                    }
                    if (twitchHold > 0f) { twitchHold -= dt; tilt += twitchTilt; }
                    break;
            }

            if (talking)
            {
                talkTime += dt;
                pitchTarget += Mathf.Sin(talkTime * 9f) * 3f;
                if (!isMimic) yawTarget = 0f;
            }

            if (watched && isMimic)
            {
                yawTarget = 0f;
                pitchTarget = PitchToward(watcherPos);
                approach = Mathf.MoveTowards(approach, maxApproach, dt * 0.05f);
            }

            bool snappy = look.idle == IdleStyle.Still || look.idle == IdleStyle.Twitch;
            headYaw = Mathf.LerpAngle(headYaw, yawTarget, dt * (snappy ? 30f : 5f));
            headPitch = Mathf.Lerp(headPitch, pitchTarget, dt * 3f);

            for (int i = 0; i < bodies.Count; i++)
            {
                var b = bodies[i];
                float phase = i * 1.7f;
                b.head.localRotation = Quaternion.Euler(headPitch, headYaw + (bodies.Count > 1 ? Mathf.Sin(t * 0.3f + phase) * 4f : 0f), tilt);
                b.torso.localScale = new Vector3(1f + breath * 0.5f, 1f + breath, 1f + breath * 0.8f);
                b.root.localPosition = baseOffsets[i] + (i == 0 ? offset : Vector3.zero) + Vector3.forward * approach;
                b.root.localRotation = Quaternion.Euler(euler);

                if (b.armR != null && !look.pizzaBox)
                {
                    if (knockTime > 0f)
                    {
                        float knock = Mathf.Abs(Mathf.Sin(t * 11f));
                        b.armR.localRotation = Quaternion.Euler(-70f - knock * 18f, -15f, 10f);
                    }
                    else b.armR.localRotation = Quaternion.Slerp(b.armR.localRotation, b.armBaseR, dt * 6f);
                }
            }
            if (knockTime > 0f) knockTime -= dt;
        }

        void AnimateTwin(float dt)
        {
            // The shadow does its own thing: it turns to look behind itself and slowly reaches for the door.
            var tb = twin;
            var main = bodies[0];
            twin.root.localPosition = main.root.localPosition;
            twin.root.localScale = main.root.localScale;
            float reachAmount = Mathf.Clamp01(Mathf.Sin(t * 0.35f) * 0.8f + 0.5f);
            if (tb.armR != null)
            {
                tb.armR.localRotation = Quaternion.Euler(-95f * reachAmount, 0f, 20f * reachAmount);
                tb.armL.localRotation = Quaternion.Euler(0f, 0f, -15f - 40f * reachAmount);
            }
            tb.head.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.5f) * 80f, Mathf.Sin(t * 1.3f) * 25f);
            tb.root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.9f) * 9f);
        }

        void AnimateSitting(float dt)
        {
            var b = bodies[0];
            float breath = Mathf.Sin(t * 2f) * 0.01f;
            float yawTarget = 0f;
            Glance(dt, ref yawTarget, 45f, 3f, 7f);
            if (watched) yawTarget = 0f;
            headYaw = Mathf.LerpAngle(headYaw, yawTarget, dt * 4f);
            b.head.localRotation = Quaternion.Euler(8f, headYaw, look.idle == IdleStyle.Sway ? Mathf.Sin(t) * 6f : 0f);
            b.torso.localScale = new Vector3(1f, 1f + breath, 1f + breath);
            b.root.localPosition = sitOffset;
        }

        void Glance(float dt, ref float yawTarget, float angle, float minWait, float maxWait)
        {
            if (glanceTimer > 0f)
            {
                glanceTimer -= dt;
                yawTarget = angle * glanceDir;
                return;
            }
            if (t >= nextGlance)
            {
                glanceTimer = Random.Range(0.8f, 1.6f);
                glanceDir = Random.value < 0.5f ? -1f : 1f;
                nextGlance = t + glanceTimer + Random.Range(minWait, maxWait);
            }
        }

        float YawToward(Vector3 target)
        {
            var d = transform.InverseTransformPoint(target);
            return Mathf.Clamp(Mathf.Atan2(d.x, Mathf.Max(0.05f, d.z)) * Mathf.Rad2Deg, -75f, 75f);
        }

        float PitchToward(Vector3 target)
        {
            var d = transform.InverseTransformPoint(target) - transform.InverseTransformPoint(HeadPosition);
            return Mathf.Clamp(-Mathf.Atan2(d.y, Mathf.Max(0.05f, d.z)) * Mathf.Rad2Deg, -40f, 40f);
        }
    }
}
