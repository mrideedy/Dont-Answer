using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DontAnswer.EditorTools
{
    /// <summary>
    /// One-click builder for the whole game: the apartment at 13 Elm Street (hall, living room, kitchen,
    /// bedroom, bathroom, basement), porch, yard, lights, rain, post-processing, systems and all references.
    /// Menu: Don't Answer / Build Game Scene. Re-running it regenerates Assets/_Game/Scenes/Main.unity.
    /// </summary>
    public static class SceneBuilder
    {
        const string Root = "Assets/_Game";
        const string MatDir = Root + "/Materials";
        const string TexDir = Root + "/Textures";
        const string ScenePath = Root + "/Scenes/Main.unity";
        const string RenderDir = Root + "/Rendering";
        const string LinesPath = Root + "/Data/Lines.csv";

        // A little lighter than the darkest things in the yard, so trees, fences and figures read as silhouettes.
        static readonly Color FogColor = new Color(0.035f, 0.043f, 0.062f);   // = the sky's horizon, so the yard fades into it
        const float H = 2.7f;     // ceiling height
        const float T = 0.15f;    // wall thickness

        static int outsideLayer, tvOnlyLayer;
        static Dictionary<string, Material> M;
        static Font font, storyFont;
        static CharacterLibrary characters;

        // ================================================================== menu items

        [MenuItem("Don't Answer/Build Game Scene", priority = 0)]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Don't Answer] Stop Play Mode before building the scene.");
                return;
            }
            var active = SceneManager.GetActiveScene();
            if (active.isDirty && active.path != ScenePath && !string.IsNullOrEmpty(active.path))
                EditorSceneManager.SaveScene(active);

            EnsureFolders();
            outsideLayer = EnsureLayer("Outside");
            tvOnlyLayer = EnsureLayer("TVOnly");
            ConfigureRendering();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            storyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/Fonts/SpecialElite-Regular.ttf");
            M = CreateMaterials();
            characters = CharacterBuilder.Build();
            var globalProfile = CreateProfile(RenderDir + "/GlobalVolume.asset", FillGlobal);
            var peepProfile = CreateProfile(RenderDir + "/PeepholeVolume.asset", FillPeephole);
            var dreadProfile = CreateProfile(RenderDir + "/DreadVolume.asset", FillDread);
            var basementProfile = CreateProfile(RenderDir + "/BasementVolume.asset", FillBasement);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupEnvironment();

            var world = new GameObject("World").transform;
            var house = Group("House", world);
            var exterior = Group("Exterior", world);
            var lightsRoot = Group("Lights", world);
            var markers = Group("Markers", world);

            var r = new Refs();
            BuildLivingRoom(house, r);
            BuildFrontDoor(house, r);
            BuildEntryway(house, r);
            BuildKitchen(house, r);
            BuildBedroom(house, r);
            BuildBathroom(house, r);
            BuildBasement(house, r);
            BuildDust(house);
            BuildPorch(exterior, r);
            BuildYard(exterior, r);
            BuildRain(exterior);
            r.windowFigure = BuildWindowFigure(exterior);
            SetLayerRecursively(exterior.gameObject, outsideLayer);

            r.moon = MakeLight("Moonlight", lightsRoot, LightType.Directional, Vector3.zero,
                Quaternion.LookRotation(new Vector3(-0.55f, -0.42f, -0.72f)), new Color(0.5f, 0.62f, 0.95f), 0.62f, 0f, LightShadows.Soft);
            r.moon.shadowStrength = 0.92f;
            // The flash for the yard only: inside, lightning arrives through the windows, by way of the moon's shadows.
            r.lightning = MakeLight("Lightning", lightsRoot, LightType.Directional, Vector3.zero,
                Quaternion.LookRotation(new Vector3(-0.3f, -0.8f, -0.5f)), new Color(0.8f, 0.85f, 1f), 0f, 0f, LightShadows.None, 1 << outsideLayer);
            r.sun = MakeLight("Sunrise", lightsRoot, LightType.Directional, Vector3.zero,
                Quaternion.LookRotation(new Vector3(-0.85f, -0.22f, -0.45f)), new Color(1f, 0.62f, 0.38f), 0f, 0f, LightShadows.Soft);
            r.sun.enabled = false;
            RenderSettings.sun = r.moon;
            lightsRoot.gameObject.AddComponent<NightSky>();

            // ---------------------------------------------------------------- volumes
            var volumes = Group("Volumes", world);
            var gv = new GameObject("Global Volume").AddComponent<Volume>();
            gv.transform.SetParent(volumes, false);
            gv.isGlobal = true;
            gv.sharedProfile = globalProfile;
            var pv = new GameObject("Peephole Volume").AddComponent<Volume>();
            pv.transform.SetParent(volumes, false);
            pv.isGlobal = true;
            pv.priority = 10;
            pv.weight = 0f;
            pv.sharedProfile = peepProfile;
            var dv = new GameObject("Dread Volume").AddComponent<Volume>();
            dv.transform.SetParent(volumes, false);
            dv.isGlobal = true;
            dv.priority = 5;
            dv.weight = 0f;
            dv.sharedProfile = dreadProfile;
            dv.gameObject.AddComponent<MoodVolume>();
            var bv = new GameObject("Basement Volume").AddComponent<Volume>();
            bv.transform.SetParent(volumes, false);
            bv.transform.position = new Vector3(-4f, -1.5f, -7f);
            bv.isGlobal = false;
            bv.priority = 3;
            bv.blendDistance = 0.8f;
            bv.sharedProfile = basementProfile;
            var bvBox = bv.gameObject.AddComponent<BoxCollider>();
            bvBox.isTrigger = true;
            bvBox.size = new Vector3(6f, 2.6f, 6f);
            bv.gameObject.layer = 2;   // Ignore Raycast: line-of-sight checks must not hit it

            new GameObject("Fog Zones").AddComponent<FogZones>().transform.SetParent(volumes, false);
            var lightning = r.lightning.gameObject.AddComponent<LightningSystem>();
            lightning.moon = r.moon;
            lightning.outsideFlash = r.lightning;

            // ---------------------------------------------------------------- player & markers
            var playerStart = Marker("PlayerStart", markers, new Vector3(0.6f, 0f, -0.6f), 0f);
            var player = BuildPlayer(playerStart, out var flashlight);
            var doorStand = Marker("DoorStand", markers, new Vector3(0.3f, 0f, 1.6f), -10f);
            var prologue = Marker("PrologueView", markers, new Vector3(0.35f, 0f, 5.15f), 180f);
            var visitorSpot = Marker("VisitorSpot", markers, new Vector3(0f, 0f, 3.15f), 180f);
            var visitorApproach = Marker("VisitorApproach", markers, new Vector3(0.4f, -0.45f, 9.5f), 180f);
            var visitorExit = Marker("VisitorExit", markers, new Vector3(3.6f, -0.45f, 9.2f), 0f);
            var upstairs = Marker("Upstairs", markers, new Vector3(0.5f, 3.4f, -1f), 0f);
            var street = Marker("Street", markers, new Vector3(2f, 1.5f, 16f), 0f);

            // ---------------------------------------------------------------- visitors
            var visitor = MakeAvatar("Visitor", world, new Vector3(0f, 0f, 4f));
            var prowler = MakeAvatar("Prowler", world, new Vector3(-8f, -0.45f, -2f));

            // ---------------------------------------------------------------- systems
            var systems = new GameObject("Systems").transform;
            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(systems, false);
            var audio = audioGo.AddComponent<AudioManager>();
            audio.doorPoint = r.doorSound;
            audio.clockPoint = r.clockPoint;
            audio.basementPoint = r.basementPoint;

            var uiGo = new GameObject("UI");
            uiGo.transform.SetParent(systems, false);
            var ui = uiGo.AddComponent<UIManager>();
            ui.storyFont = storyFont;
            ui.digitalFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/Fonts/VT323-Regular.ttf");

            var powerGo = new GameObject("Power");
            powerGo.transform.SetParent(systems, false);
            var power = powerGo.AddComponent<PowerGrid>();
            power.lights = r.poweredLights.ToArray();
            power.glowing = r.glowing.ToArray();
            power.porch = r.porch;
            power.tv = r.tv;
            power.intercomLed = r.intercomLed;

            var gameGo = new GameObject("Game");
            gameGo.transform.SetParent(systems, false);
            var gd = gameGo.AddComponent<GameDirector>();
            var phone = gameGo.AddComponent<PhoneSystem>();

            gd.linesCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(LinesPath);
            gd.ui = ui;
            gd.sfx = audio;
            gd.phone = phone;
            gd.player = player;
            gd.flashlight = flashlight;
            gd.playerStart = playerStart;
            gd.doorStand = doorStand;
            gd.prologueView = prologue;
            gd.door = r.frontDoor;
            gd.porchLight = r.porch;
            gd.peepholeAnchor = r.peepholeAnchor;
            gd.visitor = visitor;
            gd.visitorSpot = visitorSpot;
            gd.visitorApproach = visitorApproach;
            gd.visitorExit = visitorExit;
            gd.mailSlot = r.mailSlot;
            gd.backDoor = r.backDoor;
            gd.windows = r.windows.ToArray();
            gd.prowler = prowler;
            gd.insideSeats = r.seats.ToArray();
            gd.bedroomDoor = r.bedroomDoor;
            gd.basementDoor = r.basementDoor;
            gd.bathroomDoor = r.bathroomDoor;
            gd.memories = r.memories.ToArray();
            gd.landline = r.landline;
            gd.tv = r.tv;
            gd.tvFeedFigure = r.feedFigure;
            gd.power = power;
            gd.lamps = r.lamps.ToArray();
            gd.windowFigure = r.windowFigure;
            gd.upstairsPoint = upstairs;
            gd.streetPoint = street;
            gd.peepholeVolume = pv;
            gd.moonLight = r.moon;
            gd.sunLight = r.sun;
            gd.lightningLight = r.lightning;
            gd.mainCamera = player.cam;
            lightning.mainCamera = player.cam;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "Don't Answer";
            AssetDatabase.SaveAssets();
            Debug.Log("[Don't Answer] Built " + ScenePath);
        }

        class Refs
        {
            public FrontDoor frontDoor;
            public Transform peepholeAnchor, doorSound, clockPoint, basementPoint, feedFigure;
            public PorchLight porch;
            public MailSlot mailSlot;
            public BackDoor backDoor;
            public readonly List<WindowEntry> windows = new List<WindowEntry>();
            public readonly List<Transform> seats = new List<Transform>();
            public readonly List<MemoryItem> memories = new List<MemoryItem>();
            public readonly List<Light> poweredLights = new List<Light>();
            public readonly List<Renderer> glowing = new List<Renderer>();
            public readonly List<FlickerLight> lamps = new List<FlickerLight>();
            public InteriorDoor bedroomDoor, bathroomDoor, basementDoor;
            public Landline landline;
            public TVSet tv;
            public Renderer intercomLed;
            public SwingingLamp basementLamp;
            public WindowFigure windowFigure;
            public Light moon, lightning, sun;
        }

        [MenuItem("Don't Answer/Configure Rendering (URP pixel look)", priority = 21)]
        public static void ConfigureRendering()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" }))
            {
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (a == null) continue;
                var so = new SerializedObject(a);
                SetFloat(so, "m_RenderScale", 1f);   // full resolution: clean edges at any window size
                SetInt(so, "m_UpscalingFilter", (int)UpscalingFilterSelection.Linear);
                SetInt(so, "m_MSAA", 1);
                SetBool(so, "m_SupportsHDR", true);
                SetBool(so, "m_MainLightShadowsSupported", true);
                SetInt(so, "m_MainLightShadowmapResolution", 2048);
                SetInt(so, "m_AdditionalLightsRenderingMode", 1);
                SetInt(so, "m_AdditionalLightsPerObjectLimit", 8);
                SetBool(so, "m_AdditionalLightShadowsSupported", true);
                SetInt(so, "m_AdditionalLightsShadowmapResolution", 2048);
                SetInt(so, "m_AdditionalLightsShadowResolutionTierHigh", 1024);
                SetBool(so, "m_SoftShadowsSupported", true);
                SetFloat(so, "m_ShadowDistance", 30f);
                SetInt(so, "m_ShadowCascadeCount", 2);
                SetInt(so, "m_AdditionalLightsShadowResolutionTierLow", 256);
                SetInt(so, "m_AdditionalLightsShadowResolutionTierMedium", 512);
                SetInt(so, "m_AdditionalLightsShadowResolutionTierHigh", 1024);
                SetInt(so, "m_ColorGradingMode", 1);   // HDR grading under ACES
                SetInt(so, "m_ColorGradingLutSize", 32);
                SetBool(so, "m_SupportsLightCookies", true);
                SetBool(so, "m_RequireDepthTexture", true);   // soft particles (mist)
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(a);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
            {
                var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (rd == null) continue;
                var so = new SerializedObject(rd);
                SetInt(so, "m_RenderingMode", 0);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rd);
                // Ambient occlusion: corners, the gap under the door, the space behind the couch all go properly dark.
                foreach (var f in rd.rendererFeatures)
                {
                    if (f == null || f.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                    var fs = new SerializedObject(f);
                    SetFloat(fs, "m_Settings.Intensity", 1.1f);
                    SetFloat(fs, "m_Settings.Radius", 0.4f);
                    SetFloat(fs, "m_Settings.DirectLightingStrength", 0.15f);
                    SetBool(fs, "m_Settings.Downsample", true);
                    SetFloat(fs, "m_Settings.Falloff", 40f);
                    fs.ApplyModifiedPropertiesWithoutUndo();
                    f.SetActive(true);
                    EditorUtility.SetDirty(f);
                }
            }
            AssetDatabase.SaveAssets();
        }

        // ================================================================== environment

        static void SetupEnvironment()
        {
            RenderSettings.skybox = M["NightSky"];
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.056f, 0.078f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.018f;   // FogZones thickens it outside at runtime
            RenderSettings.fogColor = FogColor;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.3f;

            string lightingPath = RenderDir + "/Lighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(lightingPath);
            if (ls == null)
            {
                ls = new LightingSettings { name = "DontAnswerLighting" };
                AssetDatabase.CreateAsset(ls, lightingPath);
            }
            ls.bakedGI = false;
            ls.realtimeGI = false;
            Lightmapping.lightingSettings = ls;
        }

        // ================================================================== walls with holes

        struct Hole
        {
            public float a, b, lo, hi;
            public Hole(float a, float b, float lo, float hi) { this.a = a; this.b = b; this.lo = lo; this.hi = hi; }
        }

        /// <summary>A wall running along Z at constant X, from z0 to z1, with rectangular openings.</summary>
        static void WallX(string name, Transform parent, float x, float z0, float z1, float y0, float y1, Material m, params Hole[] holes)
        {
            Array.Sort(holes, (p, q) => p.a.CompareTo(q.a));
            float cur = z0;
            foreach (var h in holes)
            {
                if (h.a > cur) Seg(name, parent, true, x, cur, h.a, y0, y1, m);
                if (h.lo > y0) Seg(name + " below", parent, true, x, h.a, h.b, y0, h.lo, m);
                if (h.hi < y1) Seg(name + " above", parent, true, x, h.a, h.b, h.hi, y1, m);
                cur = h.b;
            }
            if (cur < z1) Seg(name, parent, true, x, cur, z1, y0, y1, m);
        }

        /// <summary>A wall running along X at constant Z, from x0 to x1, with rectangular openings.</summary>
        static void WallZ(string name, Transform parent, float z, float x0, float x1, float y0, float y1, Material m, params Hole[] holes)
        {
            Array.Sort(holes, (p, q) => p.a.CompareTo(q.a));
            float cur = x0;
            foreach (var h in holes)
            {
                if (h.a > cur) Seg(name, parent, false, z, cur, h.a, y0, y1, m);
                if (h.lo > y0) Seg(name + " below", parent, false, z, h.a, h.b, y0, h.lo, m);
                if (h.hi < y1) Seg(name + " above", parent, false, z, h.a, h.b, h.hi, y1, m);
                cur = h.b;
            }
            if (cur < x1) Seg(name, parent, false, z, cur, x1, y0, y1, m);
        }

        static void Seg(string name, Transform parent, bool alongZ, float c, float u0, float u1, float y0, float y1, Material m)
        {
            float len = u1 - u0, h = y1 - y0;
            if (len < 0.01f || h < 0.01f) return;
            var pos = alongZ ? new Vector3(c, (y0 + y1) * 0.5f, (u0 + u1) * 0.5f) : new Vector3((u0 + u1) * 0.5f, (y0 + y1) * 0.5f, c);
            var size = alongZ ? new Vector3(T, h, len) : new Vector3(len, h, T);
            var go = Box(name, parent, pos, size, m);
            Tile(go, len, h);
        }

        static void Floor(string name, Transform parent, float x0, float x1, float z0, float z1, float y, Material m, float density = 1f)
        {
            var go = Box(name, parent, new Vector3((x0 + x1) * 0.5f, y - 0.05f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.1f, z1 - z0), m);
            Tile(go, (x1 - x0) * density, (z1 - z0) * density);
        }

        static void Tile(GameObject go, float u, float v)
        {
            var at = go.AddComponent<AutoTile>();
            at.tiling = new Vector2(Mathf.Max(0.1f, u), Mathf.Max(0.1f, v));
        }

        // ================================================================== living room + hall

        static void BuildLivingRoom(Transform parent, Refs r)
        {
            var g = Group("Living Room", parent);
            var wall = M["Wallpaper"];
            Floor("Floor", g, -3f, 3f, -4f, 3f, 0f, M["Floor"], 0.8f);
            Floor("Ceiling", g, -3f, 3f, -4f, 3f, H + 0.1f, M["Ceiling"]);

            WallZ("Wall North", g, 3.075f, -3.15f, 3.15f, 0f, H, wall, new Hole(-0.5f, 0.5f, 0f, 2.15f));
            WallX("Wall East", g, 3.075f, -4.15f, 3.15f, 0f, H, wall, new Hole(-1.6f, -0.4f, 0.9f, 2.0f));
            WallX("Wall West", g, -3.075f, -4.15f, 3.15f, 0f, H, wall, new Hole(-1.0f, 0f, 0f, 2.1f));
            WallZ("Wall South", g, -4.075f, -3.15f, 3.15f, 0f, H, wall, new Hole(-1.7f, -0.75f, 0f, 2.1f), new Hole(1.25f, 2.2f, 0f, 2.1f));

            // Trim around the openings
            TrimX(g, -3.0f, -1.0f, 0f, 2.1f);
            TrimZ(g, -3.99f, -1.7f, -0.75f, 2.1f);
            TrimZ(g, -3.99f, 1.25f, 2.2f, 2.1f);
            Box("Baseboard S", g, new Vector3(0f, 0.06f, -3.99f), new Vector3(6f, 0.12f, 0.02f), M["Trim"], false);
            Box("Baseboard N A", g, new Vector3(-1.8f, 0.06f, 2.99f), new Vector3(2.4f, 0.12f, 0.02f), M["Trim"], false);
            Box("Baseboard N B", g, new Vector3(1.8f, 0.06f, 2.99f), new Vector3(2.4f, 0.12f, 0.02f), M["Trim"], false);

            // East window over the couch
            r.windows.Add(BuildWindow(g, "living room window", new Vector3(3.075f, 0.9f, -1.0f), 1.2f, 1.1f, 90f,
                new Vector3(3.75f, -0.45f, -1.0f), new Vector3(2.4f, 0f, -1.0f)));

            // Furniture
            Box("Rug", g, new Vector3(0.6f, 0.006f, -2.7f), new Vector3(2.9f, 0.012f, 2.1f), M["Rug"], false);
            Box("Couch Base", g, new Vector3(2.35f, 0.25f, -2.9f), new Vector3(0.9f, 0.5f, 2.0f), M["Couch"]);
            Box("Couch Back", g, new Vector3(2.72f, 0.78f, -2.9f), new Vector3(0.18f, 0.62f, 2.0f), M["Couch"]);
            Box("Couch Arm A", g, new Vector3(2.35f, 0.6f, -3.95f + 0.08f), new Vector3(0.9f, 0.32f, 0.16f), M["Couch"]);
            Box("Couch Arm B", g, new Vector3(2.35f, 0.6f, -1.85f), new Vector3(0.9f, 0.32f, 0.16f), M["Couch"]);
            Box("Cushion A", g, new Vector3(2.3f, 0.54f, -3.38f), new Vector3(0.76f, 0.09f, 0.92f), M["Cushion"], false);
            Box("Cushion B", g, new Vector3(2.3f, 0.54f, -2.42f), new Vector3(0.76f, 0.09f, 0.92f), M["Cushion"], false);
            Box("Throw", g, new Vector3(2.55f, 0.95f, -3.2f), new Vector3(0.3f, 0.5f, 0.7f), M["Coat"], false);

            Box("Armchair Base", g, new Vector3(-2.2f, 0.25f, -3.45f), new Vector3(0.85f, 0.5f, 0.8f), M["Couch"]);
            Box("Armchair Back", g, new Vector3(-2.2f, 0.8f, -3.82f), new Vector3(0.85f, 0.65f, 0.15f), M["Couch"]);
            Box("Armchair Arm A", g, new Vector3(-2.6f, 0.6f, -3.45f), new Vector3(0.12f, 0.3f, 0.8f), M["Couch"]);
            Box("Armchair Arm B", g, new Vector3(-1.8f, 0.6f, -3.45f), new Vector3(0.12f, 0.3f, 0.8f), M["Couch"]);

            Box("Chair Seat", g, new Vector3(1.25f, 0.45f, 0.8f), new Vector3(0.46f, 0.05f, 0.46f), M["Wood"]);
            Box("Chair Back", g, new Vector3(1.46f, 0.76f, 0.8f), new Vector3(0.05f, 0.6f, 0.46f), M["Wood"], false);
            foreach (var x in new[] { 1.05f, 1.45f })
            foreach (var z in new[] { 0.6f, 1.0f })
                Box("Chair Leg", g, new Vector3(x, 0.22f, z), new Vector3(0.04f, 0.44f, 0.04f), M["Wood"], false);

            var seats = Group("Guest Seats", g);
            r.seats.Add(Marker("Seat Couch A", seats, new Vector3(2.25f, 0.52f, -3.38f), -90f));
            r.seats.Add(Marker("Seat Couch B", seats, new Vector3(2.25f, 0.52f, -2.42f), -90f));
            r.seats.Add(Marker("Seat Armchair", seats, new Vector3(-2.2f, 0.5f, -3.4f), 20f));
            r.seats.Add(Marker("Seat Chair", seats, new Vector3(1.25f, 0.47f, 0.8f), -90f));
            r.seats.Add(Marker("Seat Rug", seats, new Vector3(0.6f, 0.08f, -2.0f), 180f));

            Box("Coffee Table", g, new Vector3(1.2f, 0.4f, -2.9f), new Vector3(0.6f, 0.05f, 1.1f), M["Wood"]);
            foreach (var x in new[] { 0.96f, 1.44f })
            foreach (var z in new[] { -3.4f, -2.4f })
                Box("Table Leg", g, new Vector3(x, 0.19f, z), new Vector3(0.04f, 0.38f, 0.04f), M["Wood"], false);
            Prim(PrimitiveType.Cylinder, "Mug", g, new Vector3(1.25f, 0.47f, -3.1f), new Vector3(0.07f, 0.045f, 0.07f), M["Paper"], false);

            // TV against the west wall, right beside the kitchen doorway
            Box("TV Stand", g, new Vector3(-2.75f, 0.3f, -2.4f), new Vector3(0.42f, 0.6f, 1.2f), M["Wood"]);
            var tvBody = Box("TV", g, new Vector3(-2.74f, 0.88f, -2.4f), new Vector3(0.34f, 0.56f, 0.76f), M["TV"]);
            var screen = Box("TV Screen", g, new Vector3(-2.566f, 0.9f, -2.4f), new Vector3(0.012f, 0.42f, 0.6f), M["Screen"], false);
            var tvLight = MakeLight("TV Light", g, LightType.Point, new Vector3(-2.2f, 0.95f, -2.4f), Quaternion.identity,
                new Color(0.6f, 0.72f, 1f), 0.8f, 4.5f, LightShadows.None, ~(1 << outsideLayer));
            var tv = tvBody.AddComponent<TVSet>();
            tv.screen = screen.GetComponent<Renderer>();
            tv.screenLight = tvLight;
            tv.newsOverlay = BuildNewsOverlay(g);
            BuildTvFeed(g, tv, r);
            tvBody.AddComponent<TVInteractable>().tv = tv;
            r.tv = tv;

            // Floor lamp, beside the window (not in front of it: the window has to stay clear to see out)
            Prim(PrimitiveType.Cylinder, "Lamp Base", g, new Vector3(2.6f, 0.02f, 0.25f), new Vector3(0.3f, 0.02f, 0.3f), M["Metal"], false);
            Prim(PrimitiveType.Cylinder, "Lamp Pole", g, new Vector3(2.6f, 0.8f, 0.25f), new Vector3(0.035f, 0.8f, 0.035f), M["Metal"]);
            var shade = Prim(PrimitiveType.Cylinder, "Lamp Shade", g, new Vector3(2.6f, 1.62f, 0.25f), new Vector3(0.42f, 0.16f, 0.42f), M["LampShade"], false);
            r.glowing.Add(shade.GetComponent<Renderer>());
            AddLamp(r, MakeLight("Floor Lamp Light", g, LightType.Point, new Vector3(2.55f, 1.45f, 0.15f), Quaternion.identity,
                new Color(1f, 0.6f, 0.34f), 2.1f, 6.5f, LightShadows.Soft, ~(1 << outsideLayer)), true, 0.03f);

            // Wall clock above the TV
            var clock = Prim(PrimitiveType.Cylinder, "Wall Clock", g, new Vector3(-2.985f, 2.0f, -2.4f), new Vector3(0.38f, 0.015f, 0.38f), M["ClockFace"], false);
            clock.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            var rim = Prim(PrimitiveType.Cylinder, "Clock Rim", g, new Vector3(-2.995f, 2.0f, -2.4f), new Vector3(0.42f, 0.012f, 0.42f), M["Wood"], false);
            rim.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            Box("Hour Hand", g, new Vector3(-2.97f, 2.04f, -2.4f), new Vector3(0.006f, 0.09f, 0.012f), M["Black"], false);
            var minute = Box("Minute Hand", g, new Vector3(-2.97f, 2.0f, -2.35f), new Vector3(0.006f, 0.012f, 0.11f), M["Black"], false);
            minute.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            r.clockPoint = clock.transform;

            // Bookshelf between the bedroom and bathroom doors
            Box("Bookshelf", g, new Vector3(0.25f, 0.9f, -3.85f), new Vector3(1.2f, 1.8f, 0.3f), M["Wood"]);
            var bookMats = new[] { "SignRed", "Curtain", "Coat", "Paper", "Couch", "Pumpkin" };
            for (int shelf = 0; shelf < 3; shelf++)
            {
                float x = -0.25f;
                for (int b = 0; x < 0.7f; b++)
                {
                    float w = 0.05f + ((b * 7 + shelf * 3) % 4) * 0.012f;
                    float h = 0.26f + ((b * 5 + shelf) % 3) * 0.04f;
                    Box("Book", g, new Vector3(x, 0.3f + shelf * 0.55f + h * 0.5f, -3.68f), new Vector3(w, h, 0.2f), M[bookMats[(b + shelf) % bookMats.Length]], false);
                    x += w + 0.008f;
                }
            }

            // Sam's moving box with Leo's photo - the first thing you can find
            Box("Moving Box", g, new Vector3(-0.9f, 0.2f, -1.9f), new Vector3(0.55f, 0.4f, 0.45f), M["Cardboard"]);
            Box("Box Flap", g, new Vector3(-0.9f, 0.41f, -1.68f), new Vector3(0.55f, 0.02f, 0.25f), M["Cardboard"], false).transform.rotation = Quaternion.Euler(-35f, 0f, 0f);
            var photo = Box("Photo", g, new Vector3(-0.85f, 0.42f, -1.95f), new Vector3(0.16f, 0.01f, 0.12f), M["Photo"], false);
            photo.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
            AddMemory(r, photo, "MEM_PHOTO", "A photo of Leo",
                "Last Halloween. Leo in his little trick-or-treat tuxedo, grinning, the bow tie you tied for him crooked - like he said.\n\nHe's wearing the bright orange sneakers he begged Mom for.", "Look at", false, new Vector3(0.28f, 0.1f, 0.25f));

            // Frames
            Box("Frame", g, new Vector3(1.6f, 1.7f, -3.985f), new Vector3(0.6f, 0.45f, 0.03f), M["Wood"], false);
            Box("Picture", g, new Vector3(1.6f, 1.7f, -3.968f), new Vector3(0.52f, 0.37f, 0.005f), M["Picture"], false);
        }

        static void BuildFrontDoor(Transform parent, Refs r)
        {
            var rootGo = new GameObject("Front Door");
            rootGo.transform.SetParent(parent, false);
            rootGo.transform.position = new Vector3(0f, 0f, 3.075f);
            var door = rootGo.AddComponent<FrontDoor>();
            r.frontDoor = door;

            var hinge = Group("Hinge", rootGo.transform);
            hinge.localPosition = new Vector3(-0.475f, 0f, 0f);
            door.hinge = hinge;

            var panel = LocalBox("Door Panel", hinge, new Vector3(0.475f, 1.06f, 0f), new Vector3(0.93f, 2.12f, 0.06f), M["Door"], true);
            panel.AddComponent<DoorInteractable>();
            foreach (var y in new[] { 0.55f, 1.5f })
            foreach (var x in new[] { 0.27f, 0.68f })
            {
                LocalBox("Inset", hinge, new Vector3(x, y, -0.033f), new Vector3(0.3f, 0.72f, 0.008f), M["DoorDark"], false);
                LocalBox("Inset Out", hinge, new Vector3(x, y, 0.033f), new Vector3(0.3f, 0.72f, 0.008f), M["DoorDark"], false);
            }

            var handle = Group("Handle", hinge);
            handle.localPosition = new Vector3(0.85f, 1.0f, -0.045f);
            door.handle = handle;
            LocalPrim(PrimitiveType.Cylinder, "Rose", handle, new Vector3(0f, 0f, 0.01f), new Vector3(0.06f, 0.008f, 0.06f), new Vector3(90f, 0f, 0f), M["Brass"]);
            LocalBox("Lever", handle, new Vector3(-0.055f, 0f, -0.02f), new Vector3(0.13f, 0.025f, 0.025f), M["Brass"], false);
            LocalBox("Sign", handle, new Vector3(-0.07f, -0.13f, -0.03f), new Vector3(0.1f, 0.17f, 0.005f), M["SignRed"], false);
            LocalBox("Sign Band", handle, new Vector3(-0.07f, -0.13f, -0.034f), new Vector3(0.08f, 0.03f, 0.002f), M["Paper"], false);
            LocalBox("Lever Out", hinge, new Vector3(0.795f, 1.0f, 0.065f), new Vector3(0.13f, 0.025f, 0.025f), M["Brass"], false);
            LocalBox("Deadbolt", hinge, new Vector3(0.85f, 1.25f, -0.04f), new Vector3(0.05f, 0.07f, 0.02f), M["Brass"], false);
            LocalBox("Chain", hinge, new Vector3(0.72f, 1.42f, -0.035f), new Vector3(0.16f, 0.012f, 0.012f), M["Brass"], false);

            // Peephole
            LocalPrim(PrimitiveType.Cylinder, "Peephole Ring", hinge, new Vector3(0.475f, 1.58f, -0.032f), new Vector3(0.04f, 0.004f, 0.04f), new Vector3(90f, 0f, 0f), M["Brass"]);
            LocalPrim(PrimitiveType.Cylinder, "Peephole Lens", hinge, new Vector3(0.475f, 1.58f, -0.035f), new Vector3(0.018f, 0.002f, 0.018f), new Vector3(90f, 0f, 0f), M["Black"]);
            var peep = new GameObject("Peephole");
            peep.transform.SetParent(hinge, false);
            peep.transform.localPosition = new Vector3(0.475f, 1.58f, -0.05f);
            var trig = peep.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.size = new Vector3(0.24f, 0.2f, 0.03f);
            peep.AddComponent<PeepholeInteractable>();
            r.peepholeAnchor = new GameObject("Peephole Camera Anchor").transform;
            r.peepholeAnchor.SetParent(hinge, false);
            r.peepholeAnchor.localPosition = new Vector3(0.475f, 1.58f, 0.06f);

            // Mail slot: a brass flap on the inside, and something's fingers on the outside.
            var slot = Group("Mail Slot", hinge);
            slot.localPosition = new Vector3(0.475f, 0.92f, 0f);
            LocalBox("Slot Plate", slot, new Vector3(0f, 0f, -0.036f), new Vector3(0.32f, 0.09f, 0.008f), M["Brass"], false);
            var flapPivot = Group("Flap Pivot", slot);
            flapPivot.localPosition = new Vector3(0f, 0.035f, -0.042f);
            LocalBox("Flap", flapPivot, new Vector3(0f, -0.03f, 0f), new Vector3(0.28f, 0.06f, 0.006f), M["Brass"], false);
            var fingers = Group("Fingers", slot);
            fingers.localPosition = new Vector3(0f, 0f, 0.06f);
            for (int i = 0; i < 4; i++)
            {
                var f = LocalPrim(PrimitiveType.Capsule, "Finger", fingers, new Vector3(-0.06f + i * 0.04f, 0f, -0.05f), new Vector3(0.022f, 0.08f, 0.022f), new Vector3(90f, 0f, 0f), M["PaleSkin"]);
                f.transform.localScale = new Vector3(0.022f, 0.07f + (i == 1 || i == 2 ? 0.02f : 0f), 0.022f);
            }
            var mail = slot.gameObject.AddComponent<MailSlot>();
            mail.flap = flapPivot;
            mail.fingers = fingers;
            r.mailSlot = mail;

            r.doorSound = new GameObject("Door Sound").transform;
            r.doorSound.SetParent(rootGo.transform, false);
            r.doorSound.localPosition = new Vector3(0f, 1.45f, 0.2f);
            door.soundPoint = r.doorSound;
            mail.soundPoint = r.doorSound;

            // Frame trim inside and out
            var g = parent;
            Box("Door Post L", g, new Vector3(-0.53f, 1.075f, 2.99f), new Vector3(0.07f, 2.15f, 0.05f), M["Trim"], false);
            Box("Door Post R", g, new Vector3(0.53f, 1.075f, 2.99f), new Vector3(0.07f, 2.15f, 0.05f), M["Trim"], false);
            Box("Door Head", g, new Vector3(0f, 2.19f, 2.99f), new Vector3(1.13f, 0.07f, 0.05f), M["Trim"], false);
            Box("Door Post L Out", g, new Vector3(-0.53f, 1.075f, 3.17f), new Vector3(0.07f, 2.15f, 0.05f), M["Trim"], false);
            Box("Door Post R Out", g, new Vector3(0.53f, 1.075f, 3.17f), new Vector3(0.07f, 2.15f, 0.05f), M["Trim"], false);
            Box("Door Head Out", g, new Vector3(0f, 2.19f, 3.17f), new Vector3(1.13f, 0.07f, 0.05f), M["Trim"], false);
        }

        static void BuildEntryway(Transform parent, Refs r)
        {
            var g = Group("Entryway", parent);
            var intercom = Box("Intercom", g, new Vector3(0.95f, 1.4f, 2.985f), new Vector3(0.17f, 0.26f, 0.03f), M["Intercom"]);
            intercom.AddComponent<IntercomInteractable>();
            Box("Speaker", g, new Vector3(0.95f, 1.46f, 2.968f), new Vector3(0.11f, 0.09f, 0.005f), M["Black"], false);
            for (int i = 0; i < 4; i++)
                Box("Slot", g, new Vector3(0.95f, 1.43f + i * 0.02f, 2.965f), new Vector3(0.09f, 0.006f, 0.002f), M["Metal"], false);
            var button = Box("Talk Button", g, new Vector3(0.95f, 1.33f, 2.964f), new Vector3(0.05f, 0.035f, 0.012f), M["ButtonGlow"], false);
            r.intercomLed = Box("LED", g, new Vector3(1.0f, 1.51f, 2.967f), new Vector3(0.012f, 0.012f, 0.004f), M["LedGreen"], false).GetComponent<Renderer>();
            r.glowing.Add(button.GetComponent<Renderer>());

            var note = Box("Landlord Note", g, new Vector3(-0.88f, 1.5f, 2.992f), new Vector3(0.26f, 0.34f, 0.006f), M["Paper"]);
            note.AddComponent<NoteInteractable>();
            note.transform.rotation = Quaternion.Euler(0f, 0f, 3f);
            for (int i = 0; i < 7; i++)
                Box("Scribble", g, new Vector3(-0.88f + (i % 2) * 0.01f, 1.62f - i * 0.035f, 2.988f), new Vector3(0.18f - (i % 3) * 0.03f, 0.008f, 0.002f), M["Ink"], false);
            Sphere("Pin", g, new Vector3(-0.88f, 1.655f, 2.985f), 0.02f, M["SignRed"]);

            Box("Switch Plate", g, new Vector3(0.95f, 1.15f, 2.99f), new Vector3(0.08f, 0.12f, 0.01f), M["Intercom"], false);
            Box("Door Mat", g, new Vector3(0f, 0.006f, 2.55f), new Vector3(1f, 0.012f, 0.6f), M["Mat"], false);

            // Side table with a jack-o'-lantern. The candle inside stays lit when the power dies.
            Box("Table Top", g, new Vector3(-1.9f, 0.76f, 2.65f), new Vector3(0.85f, 0.04f, 0.45f), M["Wood"]);
            foreach (var x in new[] { -2.27f, -1.53f })
            foreach (var z in new[] { 2.46f, 2.84f })
                Box("Leg", g, new Vector3(x, 0.37f, z), new Vector3(0.04f, 0.74f, 0.04f), M["Wood"], false);
            Sphere("Pumpkin", g, new Vector3(-1.95f, 0.91f, 2.68f), new Vector3(0.32f, 0.25f, 0.32f), M["Pumpkin"]);
            Prim(PrimitiveType.Cylinder, "Stem", g, new Vector3(-1.95f, 1.05f, 2.68f), new Vector3(0.03f, 0.03f, 0.03f), M["Stem"], false);
            Box("Pumpkin Eye L", g, new Vector3(-2.01f, 0.95f, 2.53f), new Vector3(0.045f, 0.045f, 0.02f), M["PumpkinGlow"], false).transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            Box("Pumpkin Eye R", g, new Vector3(-1.89f, 0.95f, 2.53f), new Vector3(0.045f, 0.045f, 0.02f), M["PumpkinGlow"], false).transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            Box("Pumpkin Mouth", g, new Vector3(-1.95f, 0.875f, 2.535f), new Vector3(0.13f, 0.025f, 0.02f), M["PumpkinGlow"], false);
            var pl = MakeLight("Pumpkin Candle", g, LightType.Point, new Vector3(-1.95f, 0.95f, 2.4f), Quaternion.identity,
                new Color(1f, 0.5f, 0.12f), 0.9f, 3.4f, LightShadows.None, ~(1 << outsideLayer));
            var pf = pl.gameObject.AddComponent<FlickerLight>();
            pf.noise = 0.28f;
            pf.speed = 7f;
            Prim(PrimitiveType.Cylinder, "Candy Bowl", g, new Vector3(-1.62f, 0.8f, 2.66f), new Vector3(0.22f, 0.035f, 0.22f), M["Metal"], false);
            var candyColors = new[] { "SignRed", "Pumpkin", "LedGreen", "Curtain", "Paper" };
            for (int i = 0; i < 7; i++)
                Sphere("Candy", g, new Vector3(-1.62f + Mathf.Cos(i * 0.9f) * 0.06f, 0.84f, 2.66f + Mathf.Sin(i * 0.9f) * 0.06f), 0.035f, M[candyColors[i % candyColors.Length]]);

            Prim(PrimitiveType.Cylinder, "Coat Rack", g, new Vector3(2.6f, 0.9f, 2.65f), new Vector3(0.05f, 0.9f, 0.05f), M["Wood"]);
            Prim(PrimitiveType.Cylinder, "Rack Base", g, new Vector3(2.6f, 0.02f, 2.65f), new Vector3(0.35f, 0.02f, 0.35f), M["Wood"], false);
            Prim(PrimitiveType.Capsule, "Coat", g, new Vector3(2.6f, 1.2f, 2.52f), new Vector3(0.36f, 0.45f, 0.18f), M["Coat"], false);
            Prim(PrimitiveType.Cylinder, "Hat", g, new Vector3(2.6f, 1.83f, 2.65f), new Vector3(0.26f, 0.05f, 0.26f), M["Coat"], false);

            Prim(PrimitiveType.Cylinder, "Fixture", g, new Vector3(0f, 2.68f, 1.2f), new Vector3(0.3f, 0.02f, 0.3f), M["Metal"], false);
            var hallShade = Sphere("Shade", g, new Vector3(0f, 2.63f, 1.2f), new Vector3(0.24f, 0.1f, 0.24f), M["LampGlow"]);
            r.glowing.Add(hallShade.GetComponent<Renderer>());
            AddLamp(r, MakeLight("Hall Light", g, LightType.Point, new Vector3(0f, 2.42f, 1.2f), Quaternion.identity,
                new Color(1f, 0.76f, 0.52f), 1.15f, 4.6f, LightShadows.Soft, ~(1 << outsideLayer)), true, 0.02f);

            Box("Frame", g, new Vector3(-1.9f, 1.75f, 2.985f), new Vector3(0.5f, 0.38f, 0.03f), M["Wood"], false);
            Box("Picture", g, new Vector3(-1.9f, 1.75f, 2.968f), new Vector3(0.42f, 0.3f, 0.005f), M["Picture"], false);
        }

        // ================================================================== kitchen + back door

        static void BuildKitchen(Transform parent, Refs r)
        {
            var g = Group("Kitchen", parent);
            Floor("Floor", g, -6.5f, -3.075f, -4f, 1f, 0f, M["Tiles"], 0.8f);
            Floor("Ceiling", g, -6.5f, -3.075f, -4f, 1f, H + 0.1f, M["Ceiling"]);
            var wall = M["KitchenWall"];
            WallZ("Wall North", g, 1.075f, -6.65f, -3.0f, 0f, H, wall, new Hole(-5.45f, -4.35f, 1.0f, 2.0f));
            WallX("Wall West", g, -6.575f, -4.15f, 1.15f, 0f, H, wall, new Hole(-2.4f, -1.45f, 0f, 2.1f));
            WallZ("Wall South", g, -4.075f, -6.65f, -3.15f, 0f, H, wall, new Hole(-5.6f, -4.65f, 0f, 2.1f));
            TrimX(g, -6.5f, -2.4f, -1.45f, 2.1f);
            TrimZ(g, -3.99f, -5.6f, -4.65f, 2.1f);

            r.windows.Add(BuildWindow(g, "kitchen window", new Vector3(-4.9f, 1.0f, 1.075f), 1.1f, 1.0f, 0f,
                new Vector3(-4.9f, -0.45f, 1.7f), new Vector3(-4.9f, 0f, 0.3f)));

            // Counters, sink, stove, fridge
            Box("Counter", g, new Vector3(-5.0f, 0.45f, 0.65f), new Vector3(2.8f, 0.9f, 0.6f), M["Cabinet"]);
            Box("Countertop", g, new Vector3(-5.0f, 0.92f, 0.63f), new Vector3(2.85f, 0.05f, 0.66f), M["Countertop"], false);
            Box("Sink", g, new Vector3(-4.9f, 0.93f, 0.6f), new Vector3(0.6f, 0.03f, 0.42f), M["Metal"], false);
            Prim(PrimitiveType.Cylinder, "Faucet", g, new Vector3(-4.9f, 1.05f, 0.85f), new Vector3(0.03f, 0.12f, 0.03f), M["Metal"], false);
            Box("Stove", g, new Vector3(-6.05f, 0.95f, 0.63f), new Vector3(0.6f, 0.02f, 0.58f), M["Black"], false);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cylinder, "Burner", g, new Vector3(-6.2f + (i % 2) * 0.3f, 0.965f, 0.48f + (i / 2) * 0.3f), new Vector3(0.16f, 0.005f, 0.16f), M["Metal"], false);
            Box("Cabinet Up L", g, new Vector3(-6.0f, 2.0f, 0.8f), new Vector3(0.9f, 0.7f, 0.35f), M["Cabinet"], false);
            Box("Cabinet Up R", g, new Vector3(-3.8f, 2.0f, 0.8f), new Vector3(1.1f, 0.7f, 0.35f), M["Cabinet"], false);
            Box("Fridge", g, new Vector3(-6.1f, 0.9f, -3.55f), new Vector3(0.75f, 1.8f, 0.7f), M["Fridge"]);
            Box("Fridge Handle", g, new Vector3(-5.7f, 1.1f, -3.18f), new Vector3(0.03f, 0.5f, 0.03f), M["Metal"], false);
            var drawing = Box("Drawing", g, new Vector3(-6.0f, 1.35f, -3.19f), new Vector3(0.24f, 0.3f, 0.004f), M["Drawing"], false);
            AddMemory(r, drawing, "MEM_LIGHTHOUSE", "A crayon drawing",
                "A lighthouse, a big yellow beam, and a stick-figure boy at the top.\nLEO - LITEHOUSE KEEPR.\n\nYou didn't bring this. It was already on the fridge when you got here.", "Look at", false, new Vector3(0.32f, 0.38f, 0.08f));

            // Table
            Box("Kitchen Table", g, new Vector3(-4.6f, 0.75f, -1.6f), new Vector3(0.8f, 0.05f, 1.0f), M["Wood"]);
            foreach (var x in new[] { -4.95f, -4.25f })
            foreach (var z in new[] { -2.0f, -1.2f })
                Box("Leg", g, new Vector3(x, 0.37f, z), new Vector3(0.05f, 0.74f, 0.05f), M["Wood"], false);
            Box("Kitchen Chair", g, new Vector3(-5.25f, 0.45f, -1.6f), new Vector3(0.42f, 0.05f, 0.42f), M["Wood"], false);
            Box("Kitchen Chair Back", g, new Vector3(-5.45f, 0.75f, -1.6f), new Vector3(0.04f, 0.6f, 0.42f), M["Wood"], false);

            // The landline on the wall by the doorway
            var phoneGo = Box("Landline", g, new Vector3(-3.19f, 1.45f, 0.45f), new Vector3(0.08f, 0.24f, 0.16f), M["Intercom"]);
            Box("Handset", g, new Vector3(-3.25f, 1.47f, 0.45f), new Vector3(0.05f, 0.2f, 0.06f), M["Intercom"], false);
            var display = Box("Display", g, new Vector3(-3.235f, 1.38f, 0.45f), new Vector3(0.01f, 0.03f, 0.08f), M["LedGreen"], false);
            var landline = phoneGo.AddComponent<Landline>();
            landline.displayLight = display.GetComponent<Renderer>();
            landline.speaker = phoneGo.transform;
            phoneGo.AddComponent<LandlineInteractable>().phone = landline;
            r.landline = landline;

            // Fluorescent tube
            var tube = Box("Tube", g, new Vector3(-4.75f, 2.66f, -1.5f), new Vector3(1.2f, 0.06f, 0.14f), M["LampGlow"], false);
            r.glowing.Add(tube.GetComponent<Renderer>());
            AddLamp(r, MakeLight("Kitchen Light", g, LightType.Point, new Vector3(-4.75f, 2.4f, -1.5f), Quaternion.identity,
                new Color(0.76f, 0.96f, 0.88f), 1.05f, 5.5f, LightShadows.Soft, ~(1 << outsideLayer)), true, 0.09f);

            BuildBackDoor(g, r);

            // Basement door in the south wall
            r.basementDoor = BuildInteriorDoor(g, "basement door", new Vector3(-5.6f, 0f, -4.0f), 0.95f, true, -90f, M["DoorDark"]);
        }

        static void BuildBackDoor(Transform parent, Refs r)
        {
            var rootGo = new GameObject("Back Door");
            rootGo.transform.SetParent(parent, false);
            rootGo.transform.position = new Vector3(-6.575f, 0f, -1.925f);
            var bd = rootGo.AddComponent<BackDoor>();
            var hinge = Group("Hinge", rootGo.transform);
            hinge.localPosition = new Vector3(0f, 0f, -0.475f);
            bd.hinge = hinge;
            // Panel runs along +Z from the hinge; lower half wood, upper half glass with blinds.
            LocalBox("Panel Lower", hinge, new Vector3(0f, 0.5f, 0.475f), new Vector3(0.06f, 1.0f, 0.93f), M["DoorDark"], true);
            LocalBox("Panel Top Rail", hinge, new Vector3(0f, 2.06f, 0.475f), new Vector3(0.06f, 0.12f, 0.93f), M["DoorDark"], true);
            LocalBox("Panel Side A", hinge, new Vector3(0f, 1.5f, 0.05f), new Vector3(0.06f, 1.0f, 0.1f), M["DoorDark"], true);
            LocalBox("Panel Side B", hinge, new Vector3(0f, 1.5f, 0.9f), new Vector3(0.06f, 1.0f, 0.1f), M["DoorDark"], true);
            LocalBox("Glass", hinge, new Vector3(0f, 1.5f, 0.475f), new Vector3(0.01f, 1.0f, 0.75f), M["Glass"], true);
            for (int i = 0; i < 14; i++)
            {
                if (i == 6 || i == 7) continue;   // the gap you peek through
                LocalBox("Blind Slat", hinge, new Vector3(0.04f, 1.05f + i * 0.068f, 0.475f), new Vector3(0.004f, 0.045f, 0.74f), M["Blinds"], false);
            }
            var handle = Group("Handle", hinge);
            handle.localPosition = new Vector3(0.05f, 1.0f, 0.82f);
            bd.handle = handle;
            LocalBox("Knob", handle, new Vector3(0.02f, 0f, 0f), new Vector3(0.06f, 0.06f, 0.06f), M["Brass"], false);
            var bolt = LocalBox("Bolt", hinge, new Vector3(0.045f, 1.3f, 0.86f), new Vector3(0.025f, 0.03f, 0.14f), M["Brass"], false);
            bd.bolt = bolt.transform;
            bd.boltLockedOffset = new Vector3(0f, 0f, 0.08f);
            bd.openAngle = 80f;

            var boltTrig = new GameObject("Bolt Interact");
            boltTrig.transform.SetParent(hinge, false);
            boltTrig.transform.localPosition = new Vector3(0.08f, 1.15f, 0.82f);
            var bc = boltTrig.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(0.05f, 0.5f, 0.3f);
            boltTrig.AddComponent<BoltInteractable>().door = bd;

            var blindsTrig = new GameObject("Blinds Interact");
            blindsTrig.transform.SetParent(hinge, false);
            blindsTrig.transform.localPosition = new Vector3(0.08f, 1.6f, 0.45f);
            var blc = blindsTrig.AddComponent<BoxCollider>();
            blc.isTrigger = true;
            blc.size = new Vector3(0.04f, 0.6f, 0.5f);
            var blinds = blindsTrig.AddComponent<BlindsInteractable>();
            var view = new GameObject("Blinds View").transform;
            view.SetParent(rootGo.transform, false);
            view.localPosition = new Vector3(0.1f, 1.53f, 0f);
            view.localRotation = Quaternion.Euler(0f, -90f, 0f);
            blinds.viewAnchor = view;

            bd.outsideSpot = Marker("Back Door Outside", rootGo.transform, new Vector3(-7.25f, -0.05f, -1.925f), 90f);
            bd.insideSpot = Marker("Back Door Inside", rootGo.transform, new Vector3(-6.0f, 0f, -1.925f), 90f);
            bd.soundPoint = Marker("Back Door Sound", rootGo.transform, new Vector3(-6.5f, 1.4f, -1.925f), 0f);
            r.backDoor = bd;

            // A little landing outside
            Box("Back Landing", parent, new Vector3(-7.05f, -0.1f, -1.925f), new Vector3(0.9f, 0.1f, 1.3f), M["PorchFloor"]);
            Box("Back Step", parent, new Vector3(-7.65f, -0.3f, -1.925f), new Vector3(0.4f, 0.1f, 1.1f), M["PorchFloor"]);
            Box("Back Light Cage", parent, new Vector3(-6.68f, 2.35f, -0.95f), new Vector3(0.08f, 0.14f, 0.1f), M["Metal"], false);
        }

        // ================================================================== bedroom

        static void BuildBedroom(Transform parent, Refs r)
        {
            var g = Group("Bedroom", parent);
            Floor("Floor", g, -3f, 0.5f, -8f, -4f, 0f, M["Carpet"], 1.5f);
            Floor("Ceiling", g, -3f, 0.5f, -8f, -4f, H + 0.1f, M["Ceiling"]);
            var wall = M["BedroomWall"];
            WallX("Wall West", g, -3.075f, -8.15f, -4.0f, 0f, H, wall);
            WallX("Wall East", g, 0.5f, -8.0f, -4.0f, 0f, H, wall);
            WallZ("Wall South", g, -8.075f, -3.15f, 0.6f, 0f, H, wall, new Hole(-2.2f, -1.1f, 0.9f, 2.0f));
            r.windows.Add(BuildWindow(g, "bedroom window", new Vector3(-1.65f, 0.9f, -8.075f), 1.1f, 1.1f, 180f,
                new Vector3(-1.65f, -0.45f, -8.7f), new Vector3(-1.65f, 0f, -7.4f)));
            r.bedroomDoor = BuildInteriorDoor(g, "bedroom door", new Vector3(-1.7f, 0f, -4.0f), 0.95f, true, 90f, M["Door"]);

            // Bed
            Box("Bed Frame", g, new Vector3(-2.35f, 0.22f, -6.6f), new Vector3(1.1f, 0.44f, 2.0f), M["Wood"]);
            Box("Mattress", g, new Vector3(-2.35f, 0.5f, -6.55f), new Vector3(1.0f, 0.14f, 1.9f), M["Sheet"], false);
            Box("Blanket", g, new Vector3(-2.35f, 0.58f, -6.2f), new Vector3(1.04f, 0.06f, 1.2f), M["Blanket"], false);
            Box("Pillow", g, new Vector3(-2.35f, 0.62f, -7.3f), new Vector3(0.6f, 0.12f, 0.35f), M["Sheet"], false);
            Box("Headboard", g, new Vector3(-2.35f, 0.75f, -7.6f), new Vector3(1.1f, 0.9f, 0.08f), M["Wood"]);

            Box("Nightstand", g, new Vector3(-1.45f, 0.3f, -7.6f), new Vector3(0.45f, 0.6f, 0.4f), M["Wood"]);
            Prim(PrimitiveType.Cylinder, "Lamp Base", g, new Vector3(-1.4f, 0.65f, -7.65f), new Vector3(0.1f, 0.05f, 0.1f), M["Metal"], false);
            var bShade = Prim(PrimitiveType.Cylinder, "Lamp Shade", g, new Vector3(-1.4f, 0.82f, -7.65f), new Vector3(0.22f, 0.1f, 0.22f), M["LampShade"], false);
            r.glowing.Add(bShade.GetComponent<Renderer>());
            AddLamp(r, MakeLight("Bedside Lamp", g, LightType.Point, new Vector3(-1.4f, 0.95f, -7.4f), Quaternion.identity,
                new Color(1f, 0.62f, 0.36f), 0.95f, 4.2f, LightShadows.Soft, ~(1 << outsideLayer)), true, 0.03f);
            var radio = Box("Walkie-talkie", g, new Vector3(-1.55f, 0.66f, -7.5f), new Vector3(0.06f, 0.12f, 0.04f), M["Radio"], false);
            AddMemory(r, radio, "MEM_RADIO", "Leo's walkie-talkie",
                "One of a pair. You had the other one, once.\nHe used to call you on it from his room across the hall, every single night.", "Pick up", false, new Vector3(0.2f, 0.25f, 0.2f));

            // Wardrobe
            Box("Wardrobe", g, new Vector3(0.1f, 1.0f, -5.5f), new Vector3(0.6f, 2.0f, 1.2f), M["Wood"]);
            Box("Wardrobe Gap", g, new Vector3(-0.21f, 1.0f, -5.5f), new Vector3(0.01f, 1.9f, 0.02f), M["Black"], false);

            // Leo's box under the window
            Box("Leo's Box", g, new Vector3(-0.6f, 0.18f, -7.45f), new Vector3(0.5f, 0.36f, 0.45f), M["Cardboard"]);
            Box("Box Label", g, new Vector3(-0.6f, 0.25f, -7.22f), new Vector3(0.2f, 0.08f, 0.002f), M["Paper"], false);
            var card = Box("Knock Card", g, new Vector3(-0.58f, 0.37f, -7.45f), new Vector3(0.15f, 0.005f, 0.1f), M["Paper"], false);
            card.transform.rotation = Quaternion.Euler(0f, -20f, 0f);
            AddMemory(r, card, "MEM_KNOCK", "A card in Leo's handwriting",
                "OUR SEKRET KNOCK.\nTwo fast. One slow. Two fast.\n\"If your ever scared, knock our knock and I'll alwayz open.\"", "Read", true, new Vector3(0.28f, 0.1f, 0.24f));
        }

        // ================================================================== bathroom

        static void BuildBathroom(Transform parent, Refs r)
        {
            var g = Group("Bathroom", parent);
            Floor("Floor", g, 0.5f, 3f, -8f, -4f, 0f, M["Tiles"], 0.9f);
            Floor("Ceiling", g, 0.5f, 3f, -8f, -4f, H + 0.1f, M["Ceiling"]);
            var wall = M["BathWall"];
            WallX("Wall East", g, 3.075f, -8.15f, -4.0f, 0f, H, wall);
            WallZ("Wall South", g, -8.075f, 0.4f, 3.15f, 0f, H, wall);
            r.bathroomDoor = BuildInteriorDoor(g, "bathroom door", new Vector3(2.2f, 0f, -4.0f), 0.95f, false, -90f, M["Door"]);

            Box("Sink Cabinet", g, new Vector3(2.75f, 0.4f, -5.0f), new Vector3(0.45f, 0.8f, 0.6f), M["Cabinet"]);
            Box("Sink Basin", g, new Vector3(2.72f, 0.82f, -5.0f), new Vector3(0.38f, 0.05f, 0.45f), M["Porcelain"], false);
            Box("Mirror", g, new Vector3(2.99f, 1.55f, -5.0f), new Vector3(0.02f, 0.7f, 0.55f), M["Mirror"], false);
            Box("Mirror Frame", g, new Vector3(3.0f, 1.55f, -5.0f), new Vector3(0.01f, 0.76f, 0.61f), M["Metal"], false);
            Box("Toilet", g, new Vector3(2.65f, 0.22f, -6.4f), new Vector3(0.42f, 0.44f, 0.55f), M["Porcelain"]);
            Box("Tank", g, new Vector3(2.85f, 0.65f, -6.4f), new Vector3(0.2f, 0.45f, 0.45f), M["Porcelain"], false);
            Box("Tub", g, new Vector3(1.55f, 0.28f, -7.6f), new Vector3(1.75f, 0.56f, 0.72f), M["Porcelain"]);
            Box("Tub Inside", g, new Vector3(1.55f, 0.5f, -7.6f), new Vector3(1.6f, 0.08f, 0.56f), M["Black"], false);
            Box("Curtain Rod", g, new Vector3(1.55f, 2.1f, -7.22f), new Vector3(1.8f, 0.02f, 0.02f), M["Metal"], false);
            Box("Shower Curtain", g, new Vector3(1.2f, 1.35f, -7.22f), new Vector3(1.0f, 1.5f, 0.01f), M["ShowerCurtain"], false);

            var fixture = Sphere("Bath Fixture", g, new Vector3(1.75f, 2.66f, -6.0f), new Vector3(0.3f, 0.08f, 0.3f), M["LampGlow"]);
            r.glowing.Add(fixture.GetComponent<Renderer>());
            AddLamp(r, MakeLight("Bath Light", g, LightType.Point, new Vector3(1.75f, 2.4f, -6.0f), Quaternion.identity,
                new Color(0.85f, 0.95f, 1f), 0.8f, 4f, LightShadows.None, ~(1 << outsideLayer)), false, 0.02f);
        }

        // ================================================================== basement

        static void BuildBasement(Transform parent, Refs r)
        {
            var g = Group("Basement", parent);
            float y0 = -2.8f, y1 = -0.2f;
            Floor("Floor", g, -7f, -1f, -10f, -4f, y0, M["Concrete"], 1f);
            // Ceiling with a hole for the stairwell
            Box("Ceiling West", g, new Vector3(-6.3f, y1 + 0.05f, -7f), new Vector3(1.4f, 0.1f, 6f), M["Concrete"]);
            Box("Ceiling East", g, new Vector3(-2.825f, y1 + 0.05f, -7f), new Vector3(3.65f, 0.1f, 6f), M["Concrete"]);
            Box("Ceiling South", g, new Vector3(-5.125f, y1 + 0.05f, -8.7f), new Vector3(0.95f, 0.1f, 2.6f), M["Concrete"]);
            var brick = M["Brick"];
            WallX("Wall West", g, -7.075f, -10.15f, -3.85f, y0, y1, brick);
            WallX("Wall East", g, -0.925f, -10.15f, -3.85f, y0, y1, brick);
            WallZ("Wall South", g, -10.075f, -7.15f, -0.85f, y0, y1, brick);
            WallZ("Wall North", g, -3.925f, -7.15f, -0.85f, y0, y1, brick);

            // Stairwell from the kitchen down to the basement
            WallX("Stair Wall W", g, -5.675f, -7.4f, -4.0f, y0, H, M["KitchenWall"]);
            WallX("Stair Wall E", g, -4.575f, -7.4f, -4.0f, y0, H, M["KitchenWall"]);
            WallZ("Stair Wall End", g, -7.475f, -5.75f, -4.5f, y1, H, M["KitchenWall"]);
            Floor("Stair Ceiling", g, -5.6f, -4.65f, -7.4f, -4.0f, H + 0.1f, M["Ceiling"]);
            for (int i = 0; i < 10; i++)
            {
                float top = -0.28f * (i + 1);
                Box("Step", g, new Vector3(-5.125f, top - 0.06f, -4.0f - 0.34f * i - 0.17f), new Vector3(0.95f, 0.12f, 0.34f), M["Wood"], false);
            }
            var ramp = new GameObject("Stair Ramp");
            ramp.transform.SetParent(g, false);
            float run = 3.4f, drop = 2.8f;
            ramp.transform.position = new Vector3(-5.125f, -drop * 0.5f - 0.08f, -4.0f - run * 0.5f);
            ramp.transform.rotation = Quaternion.Euler(-Mathf.Atan2(drop, run) * Mathf.Rad2Deg, 0f, 0f);
            ramp.AddComponent<BoxCollider>().size = new Vector3(0.95f, 0.1f, Mathf.Sqrt(run * run + drop * drop) + 0.2f);
            Box("Rail", g, new Vector3(-4.62f, -0.6f, -5.7f), new Vector3(0.04f, 0.04f, 3.6f), M["Metal"], false).transform.rotation = Quaternion.Euler(-Mathf.Atan2(drop, run) * Mathf.Rad2Deg, 0f, 0f);

            // Stuff
            Prim(PrimitiveType.Cylinder, "Boiler", g, new Vector3(-6.3f, y0 + 1.0f, -4.8f), new Vector3(0.9f, 1.0f, 0.9f), M["Rust"]);
            Prim(PrimitiveType.Cylinder, "Pipe", g, new Vector3(-6.3f, y0 + 2.2f, -6.5f), new Vector3(0.12f, 1.8f, 0.12f), M["Rust"], false).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, "Pipe 2", g, new Vector3(-3.5f, y0 + 2.4f, -9.85f), new Vector3(0.1f, 3f, 0.1f), M["Rust"], false).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            for (int s = 0; s < 3; s++)
            {
                Box("Shelf", g, new Vector3(-6.75f, y0 + 0.5f + s * 0.6f, -8.0f), new Vector3(0.45f, 0.04f, 2.2f), M["Wood"], false);
                for (int b = 0; b < 3; b++)
                    Box("Box", g, new Vector3(-6.75f, y0 + 0.7f + s * 0.6f, -8.8f + b * 0.75f + (s % 2) * 0.2f), new Vector3(0.38f, 0.32f, 0.45f), M["Cardboard"], false);
            }
            Box("Old Mattress", g, new Vector3(-2.2f, y0 + 0.9f, -4.15f), new Vector3(1.2f, 1.8f, 0.15f), M["Sheet"], false).transform.rotation = Quaternion.Euler(-10f, 0f, 0f);
            Box("Crate", g, new Vector3(-3.4f, y0 + 0.3f, -9.4f), new Vector3(0.7f, 0.6f, 0.6f), M["Wood"]);
            Box("Crate 2", g, new Vector3(-1.6f, y0 + 0.25f, -5.6f), new Vector3(0.6f, 0.5f, 0.7f), M["Wood"]);

            // Mr. Buttons, in the box of things the police left behind.
            Box("Lost & Found", g, new Vector3(-1.5f, y0 + 0.2f, -9.4f), new Vector3(0.5f, 0.4f, 0.45f), M["Cardboard"]);
            var rabbit = Sphere("Mr. Buttons", g, new Vector3(-1.5f, y0 + 0.45f, -9.4f), new Vector3(0.14f, 0.16f, 0.12f), M["Rabbit"]);
            Sphere("Ear", g, new Vector3(-1.53f, y0 + 0.57f, -9.4f), new Vector3(0.03f, 0.12f, 0.03f), M["Rabbit"]);
            AddMemory(r, rabbit, "MEM_RABBIT", "Mr. Buttons",
                "Leo's stuffed rabbit. One ear torn half off.\nA tag on the box: ELM ST PORCH - 11/01 - UNCLAIMED.\nMr. Voss kept it. All this time.", "Pick up", false, new Vector3(0.32f, 0.4f, 0.32f));

            // The fuse box
            Box("Fuse Box", g, new Vector3(-0.98f, -1.45f, -8.2f), new Vector3(0.08f, 0.6f, 0.45f), M["Fridge"]);
            var lever = Box("Breaker Lever", g, new Vector3(-1.04f, -1.45f, -8.2f), new Vector3(0.05f, 0.18f, 0.06f), M["SignRed"]);
            lever.AddComponent<FuseBoxInteractable>();
            Box("Warning", g, new Vector3(-1.03f, -1.0f, -8.2f), new Vector3(0.01f, 0.18f, 0.3f), M["Paper"], false);
            BuildWorldLabel(g, "DON'T\nLOOK BACK", new Vector3(-1.045f, -1.0f, -8.2f), Quaternion.Euler(0f, 90f, 0f), 0.28f, 0.16f, new Color(0.35f, 0.05f, 0.03f));

            // A bare bulb on a wire, always swinging a little, so every shadow down here moves.
            var swing = Group("Bulb Swing", g);
            swing.position = new Vector3(-4f, -0.2f, -7f);
            var bulb = Sphere("Bulb", swing, new Vector3(-4f, -0.55f, -7f), 0.1f, M["LampGlow"]);
            r.glowing.Add(bulb.GetComponent<Renderer>());
            var wire = Box("Bulb Wire", swing, new Vector3(-4f, -0.35f, -7f), new Vector3(0.01f, 0.3f, 0.01f), M["Black"], false);
            foreach (var go in new[] { bulb, wire }) GameObjectUtility.SetStaticEditorFlags(go, 0);
            AddLamp(r, MakeLight("Basement Bulb", swing, LightType.Point, new Vector3(-4f, -0.68f, -7f), Quaternion.identity,
                new Color(1f, 0.78f, 0.5f), 1.4f, 7f, LightShadows.Soft, ~(1 << outsideLayer)), false, 0.06f);
            var swinging = swing.gameObject.AddComponent<SwingingLamp>();
            swinging.amplitude = 4f;
            swinging.period = 3.6f;
            r.basementLamp = swinging;
            r.basementPoint = Marker("Basement Sound", g, new Vector3(-4f, -1.5f, -7f), 0f);

        }

        // ================================================================== reusable pieces

        static WindowEntry BuildWindow(Transform parent, string name, Vector3 sillCenter, float width, float height, float yaw, Vector3 outside, Vector3 inside)
        {
            var rootGo = new GameObject(name);
            rootGo.transform.SetParent(parent, false);
            rootGo.transform.SetPositionAndRotation(sillCenter, Quaternion.Euler(0f, yaw, 0f));
            var w = rootGo.AddComponent<WindowEntry>();
            w.displayName = name;
            rootGo.AddComponent<WindowInteractable>().window = w;
            var t = rootGo.transform;
            float half = height * 0.5f;
            // Frame (local X = across the window, local Z = through the wall, +Z points outside)
            LocalBox("Frame Top", t, new Vector3(0f, height + 0.03f, -0.06f), new Vector3(width + 0.12f, 0.06f, 0.06f), M["WindowFrame"], false);
            LocalBox("Frame L", t, new Vector3(-width * 0.5f - 0.03f, half, -0.06f), new Vector3(0.06f, height + 0.06f, 0.06f), M["WindowFrame"], false);
            LocalBox("Frame R", t, new Vector3(width * 0.5f + 0.03f, half, -0.06f), new Vector3(0.06f, height + 0.06f, 0.06f), M["WindowFrame"], false);
            LocalBox("Sill", t, new Vector3(0f, -0.02f, -0.12f), new Vector3(width + 0.16f, 0.05f, 0.2f), M["WindowFrame"], false);
            // Upper pane (fixed)
            LocalBox("Upper Glass", t, new Vector3(0f, height * 0.75f, 0.02f), new Vector3(width, half, 0.01f), M["Glass"], false);
            LocalBox("Upper Rail", t, new Vector3(0f, half, 0.02f), new Vector3(width, 0.04f, 0.04f), M["WindowFrame"], false);
            // Lower sash (slides up)
            var sash = Group("Sash", t);
            sash.localPosition = new Vector3(0f, 0f, -0.02f);
            LocalBox("Glass", sash, new Vector3(0f, height * 0.25f, 0f), new Vector3(width, half, 0.01f), M["Glass"], false);
            LocalBox("Bottom Rail", sash, new Vector3(0f, 0.02f, 0f), new Vector3(width, 0.05f, 0.04f), M["WindowFrame"], false);
            LocalBox("Mullion", sash, new Vector3(0f, height * 0.25f, 0f), new Vector3(0.03f, half, 0.03f), M["WindowFrame"], false);
            var latch = Group("Latch", sash);
            latch.localPosition = new Vector3(0f, half - 0.01f, -0.03f);
            LocalBox("Latch Lever", latch, new Vector3(0.03f, 0f, 0f), new Vector3(0.07f, 0.015f, 0.02f), M["Brass"], false);
            w.sash = sash;
            w.latch = latch;
            w.openHeight = half * 0.85f;
            // Curtains (inside)
            var cl = Group("Curtain L", t);
            cl.localPosition = new Vector3(-width * 0.5f - 0.02f, height + 0.12f, -0.16f);
            LocalBox("Cloth", cl, new Vector3(0.12f, -half - 0.12f, 0f), new Vector3(0.3f, height + 0.3f, 0.03f), M["Curtain"], false);
            var cr = Group("Curtain R", t);
            cr.localPosition = new Vector3(width * 0.5f + 0.02f, height + 0.12f, -0.16f);
            LocalBox("Cloth", cr, new Vector3(-0.12f, -half - 0.12f, 0f), new Vector3(0.3f, height + 0.3f, 0.03f), M["Curtain"], false);
            w.curtains = new[] { cl, cr };
            // Something solid in the hole so nobody walks through, and a target for the interact ray.
            var blocker = new GameObject("Blocker");
            blocker.transform.SetParent(t, false);
            blocker.transform.localPosition = new Vector3(0f, half, 0f);
            var bc = blocker.AddComponent<BoxCollider>();
            bc.size = new Vector3(width, height, 0.12f);
            w.blocker = bc;
            w.outsideSpot = Marker("Outside", parent, outside, yaw + 180f);
            w.insideSpot = Marker("Inside", parent, inside, yaw);
            w.soundPoint = Marker("Sound", t, sillCenter + Vector3.up * half, 0f);
            w.soundPoint.position = sillCenter + Vector3.up * half;
            return w;
        }

        static InteriorDoor BuildInteriorDoor(Transform parent, string name, Vector3 hingePos, float width, bool panelAlongPlusX, float openAngle, Material m)
        {
            var hingeGo = new GameObject(name);
            hingeGo.transform.SetParent(parent, false);
            hingeGo.transform.position = hingePos;
            float dir = panelAlongPlusX ? 1f : -1f;
            LocalBox("Panel", hingeGo.transform, new Vector3(dir * width * 0.5f, 1.04f, 0f), new Vector3(width - 0.02f, 2.06f, 0.045f), m, true);
            LocalBox("Knob", hingeGo.transform, new Vector3(dir * (width - 0.12f), 1.0f, 0.04f), new Vector3(0.05f, 0.05f, 0.05f), M["Brass"], false);
            LocalBox("Knob B", hingeGo.transform, new Vector3(dir * (width - 0.12f), 1.0f, -0.04f), new Vector3(0.05f, 0.05f, 0.05f), M["Brass"], false);
            var d = hingeGo.AddComponent<InteriorDoor>();
            d.hinge = hingeGo.transform;
            d.openAngle = openAngle;
            d.doorName = name.Replace(" door", "") + " door";
            d.soundPoint = Marker("Sound", hingeGo.transform, hingePos + new Vector3(dir * width * 0.5f, 1.3f, 0f), 0f);
            d.soundPoint.position = hingePos + new Vector3(dir * width * 0.5f, 1.3f, 0f);
            return d;
        }

        static void AddMemory(Refs r, GameObject go, string id, string title, string description, string verb, bool knock, Vector3 colliderScale)
        {
            var old = go.GetComponent<Collider>();
            if (old != null) Object.DestroyImmediate(old);
            var b = go.AddComponent<BoxCollider>();
            b.isTrigger = true;
            var ls = go.transform.lossyScale;
            b.size = new Vector3(colliderScale.x / Mathf.Max(0.001f, ls.x), colliderScale.y / Mathf.Max(0.001f, ls.y), colliderScale.z / Mathf.Max(0.001f, ls.z));
            var m = go.AddComponent<MemoryItem>();
            m.memoryId = id;
            m.title = title;
            m.description = description;
            m.verb = verb;
            m.playsSecretKnock = knock;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            r.memories.Add(m);
        }

        static void AddLamp(Refs r, Light l, bool flickerEvents, float noise)
        {
            var f = l.gameObject.AddComponent<FlickerLight>();
            f.noise = noise;
            r.poweredLights.Add(l);
            if (flickerEvents) r.lamps.Add(f);
        }

        static void TrimX(Transform g, float x, float z0, float z1, float h)
        {
            Box("Trim", g, new Vector3(x, h * 0.5f, z0 - 0.03f), new Vector3(0.2f, h, 0.06f), M["Trim"], false);
            Box("Trim", g, new Vector3(x, h * 0.5f, z1 + 0.03f), new Vector3(0.2f, h, 0.06f), M["Trim"], false);
            Box("Trim Top", g, new Vector3(x, h + 0.03f, (z0 + z1) * 0.5f), new Vector3(0.2f, 0.06f, z1 - z0 + 0.12f), M["Trim"], false);
        }

        static void TrimZ(Transform g, float z, float x0, float x1, float h)
        {
            Box("Trim", g, new Vector3(x0 - 0.03f, h * 0.5f, z), new Vector3(0.06f, h, 0.2f), M["Trim"], false);
            Box("Trim", g, new Vector3(x1 + 0.03f, h * 0.5f, z), new Vector3(0.06f, h, 0.2f), M["Trim"], false);
            Box("Trim Top", g, new Vector3((x0 + x1) * 0.5f, h + 0.03f, z), new Vector3(x1 - x0 + 0.12f, 0.06f, 0.2f), M["Trim"], false);
        }

        static GameObject BuildNewsOverlay(Transform parent)
        {
            var go = new GameObject("News Overlay", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(600f, 420f);
            rt.position = new Vector3(-2.556f, 0.9f, -2.4f);
            rt.rotation = Quaternion.Euler(0f, -90f, 0f);
            rt.localScale = Vector3.one * 0.001f;
            UIBox(rt, "BG", new Color(0.04f, 0.07f, 0.18f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UIBox(rt, "Anchor Desk", new Color(0.12f, 0.1f, 0.12f), new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.42f), Vector2.zero, Vector2.zero);
            UIBox(rt, "Anchor Body", new Color(0.18f, 0.2f, 0.28f), new Vector2(0.38f, 0.35f), new Vector2(0.62f, 0.72f), Vector2.zero, Vector2.zero);
            UIBox(rt, "Anchor Head", new Color(0.75f, 0.6f, 0.5f), new Vector2(0.45f, 0.72f), new Vector2(0.55f, 0.86f), Vector2.zero, Vector2.zero);
            UIBox(rt, "Photo", new Color(0.85f, 0.85f, 0.82f), new Vector2(0.7f, 0.55f), new Vector2(0.92f, 0.85f), Vector2.zero, Vector2.zero);
            UIBox(rt, "Ghost", new Color(0.97f, 0.97f, 0.97f), new Vector2(0.77f, 0.58f), new Vector2(0.85f, 0.8f), Vector2.zero, Vector2.zero);
            UIBox(rt, "Banner", new Color(0.65f, 0.05f, 0.05f), new Vector2(0f, 0f), new Vector2(1f, 0.17f), Vector2.zero, Vector2.zero);
            UIBox(rt, "Live", new Color(0.85f, 0.05f, 0.05f), new Vector2(0.03f, 0.88f), new Vector2(0.17f, 0.97f), Vector2.zero, Vector2.zero);
            UIText(rt, "LIVE", 34, Color.white, new Vector2(0.03f, 0.88f), new Vector2(0.17f, 0.97f));
            UIText(rt, "NEWS 9  HOLLOW CREEK", 30, Color.white, new Vector2(0.2f, 0.88f), new Vector2(0.97f, 0.97f));
            UIText(rt, "ONE YEAR LATER: VIGIL FOR LEO HOLLIS", 28, Color.white, new Vector2(0.02f, 0f), new Vector2(0.98f, 0.17f));
            go.SetActive(false);
            return go;
        }

        static void UIBox(RectTransform parent, string name, Color c, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            go.GetComponent<Image>().color = c;
        }

        static Text UIText(RectTransform parent, string text, int size, Color c, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            return t;
        }

        static void BuildWorldLabel(Transform parent, string text, Vector3 pos, Quaternion rot, float w, float h, Color c)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(w * 1000f, h * 1000f);
            rt.position = pos;
            rt.rotation = rot;
            rt.localScale = Vector3.one * 0.001f;
            var label = UIText(rt, text, 34, c, Vector2.zero, Vector2.one);
            if (storyFont != null) label.font = storyFont;   // written by hand, in a hurry
        }

        static void BuildTvFeed(Transform parent, TVSet tv, Refs r)
        {
            string rtPath = RenderDir + "/TVFeed.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
            if (rt == null)
            {
                rt = new RenderTexture(256, 192, 16) { name = "TVFeed", filterMode = FilterMode.Point };
                AssetDatabase.CreateAsset(rt, rtPath);
            }
            var camGo = new GameObject("TV Feed Camera");
            camGo.transform.SetParent(parent, false);
            camGo.transform.SetPositionAndRotation(new Vector3(3.9f, 1.75f, -0.9f), Quaternion.Euler(12f, -105f, 0f));
            var cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.fieldOfView = 72f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 25f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = ~(1 << 5);
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            cam.enabled = false;
            tv.feedCamera = cam;
            tv.feedTexture = rt;

            var fig = Group("TV Feed Figure", parent);
            LocalPrim(PrimitiveType.Capsule, "Body", fig, new Vector3(0f, 1.0f, 0f), new Vector3(0.45f, 1.0f, 0.35f), Vector3.zero, M["Silhouette"]);
            LocalPrim(PrimitiveType.Sphere, "Head", fig, new Vector3(0f, 2.15f, 0f), new Vector3(0.26f, 0.32f, 0.28f), new Vector3(0f, 0f, 20f), M["Silhouette"]);
            SetLayerRecursively(fig.gameObject, tvOnlyLayer);
            fig.gameObject.SetActive(false);
            r.feedFigure = fig;
        }

        // ================================================================== exterior

        static void BuildPorch(Transform parent, Refs r)
        {
            var g = Group("Porch", parent);
            var floor = Box("Porch Floor", g, new Vector3(0f, -0.06f, 4.35f), new Vector3(3.2f, 0.12f, 2.4f), M["PorchFloor"]);
            Tile(floor, 3.2f, 2.4f);
            Box("Porch Roof", g, new Vector3(0f, 2.76f, 4.42f), new Vector3(3.5f, 0.12f, 2.65f), M["Siding"]);
            Box("Roof Beam", g, new Vector3(0f, 2.6f, 5.6f), new Vector3(3.5f, 0.22f, 0.14f), M["PostWhite"]);
            var fa = Box("Facade A", g, new Vector3(-3.6f, 1.6f, 3.17f), new Vector3(6.2f, 3.4f, 0.04f), M["Siding"]);
            Tile(fa, 6.2f, 3.4f);
            var fb = Box("Facade B", g, new Vector3(3.2f, 1.6f, 3.17f), new Vector3(5.4f, 3.4f, 0.04f), M["Siding"]);
            Tile(fb, 5.4f, 3.4f);
            Box("Facade Top", g, new Vector3(0f, 3.0f, 3.17f), new Vector3(1.0f, 0.6f, 0.04f), M["Siding"]);
            Box("House Number", g, new Vector3(0.78f, 1.75f, 3.2f), new Vector3(0.14f, 0.2f, 0.02f), M["Brass"], false);
            foreach (var x in new[] { -1.5f, 1.5f })
            {
                Box("Post", g, new Vector3(x, 1.35f, 5.5f), new Vector3(0.14f, 2.7f, 0.14f), M["PostWhite"]);
                Box("Rail", g, new Vector3(x, 0.9f, 4.35f), new Vector3(0.08f, 0.06f, 2.3f), M["PostWhite"]);
                for (float z = 3.35f; z < 5.45f; z += 0.26f)
                    Box("Baluster", g, new Vector3(x, 0.45f, z), new Vector3(0.04f, 0.9f, 0.04f), M["PostWhite"], false);
            }
            Box("Step 1", g, new Vector3(0f, -0.2f, 5.78f), new Vector3(1.5f, 0.12f, 0.42f), M["PorchFloor"]);
            Box("Step 2", g, new Vector3(0f, -0.38f, 6.15f), new Vector3(1.5f, 0.12f, 0.42f), M["PorchFloor"]);
            Prim(PrimitiveType.Cylinder, "Porch Pumpkin", g, new Vector3(1.15f, 0.12f, 3.5f), new Vector3(0.36f, 0.12f, 0.36f), M["Pumpkin"], false);
            Prim(PrimitiveType.Cylinder, "Planter", g, new Vector3(-1.15f, 0.2f, 3.45f), new Vector3(0.36f, 0.2f, 0.36f), M["Metal"]);
            Sphere("Dead Plant", g, new Vector3(-1.15f, 0.5f, 3.45f), new Vector3(0.4f, 0.35f, 0.4f), M["Bark"]);

            // Side-on sconce so a person's shadow falls where the peephole can see it.
            Box("Sconce Plate", g, new Vector3(-0.9f, 2.22f, 3.2f), new Vector3(0.12f, 0.2f, 0.04f), M["Metal"], false);
            Box("Sconce Arm", g, new Vector3(-0.9f, 2.26f, 3.27f), new Vector3(0.03f, 0.03f, 0.12f), M["Metal"], false);
            var bulb = Sphere("Sconce Bulb", g, new Vector3(-0.9f, 2.22f, 3.33f), 0.11f, M["Bulb"]);
            var l = MakeLight("Porch Light", g, LightType.Spot, new Vector3(-0.88f, 2.18f, 3.4f),
                Quaternion.LookRotation(new Vector3(0.75f, -2.1f, 1.3f)), new Color(1f, 0.8f, 0.55f), 7f, 8f, LightShadows.Soft, 1 << outsideLayer);
            l.spotAngle = 125f;
            l.innerSpotAngle = 70f;
            var porch = l.gameObject.AddComponent<PorchLight>();
            porch.lamp = l;
            porch.bulb = bulb.GetComponent<Renderer>();
            porch.intensity = 7f;
            r.porch = porch;

            MakeLight("Porch Fill", g, LightType.Point, new Vector3(0.6f, 1.2f, 7.2f), Quaternion.identity,
                new Color(0.45f, 0.55f, 0.8f), 0.35f, 5.5f, LightShadows.None, 1 << outsideLayer);
            MakeLight("Back Door Light", g, LightType.Point, new Vector3(-7.0f, 2.3f, -1.2f), Quaternion.identity,
                new Color(0.55f, 0.65f, 0.9f), 0.5f, 4.5f, LightShadows.None, 1 << outsideLayer);
        }

        static void BuildYard(Transform parent, Refs r)
        {
            var g = Group("Yard", parent);
            // The yard, in four pieces around the basement: one slab across everything sat right through the
            // stairwell, and you could never get more than a few steps down.
            float gx0 = -27f, gx1 = 23f, gz0 = -20f, gz1 = 24f;   // yard extents
            float hx0 = -7.15f, hx1 = -0.85f, hz0 = -10.15f, hz1 = -3.85f;   // basement footprint
            void GroundPiece(string n, float x0, float x1, float z0, float z1)
            {
                var piece = Box(n, g, new Vector3((x0 + x1) * 0.5f, -0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.1f, z1 - z0), M["Ground"]);
                Tile(piece, (x1 - x0) * 0.5f, (z1 - z0) * 0.5f);
            }
            GroundPiece("Ground North", gx0, gx1, hz1, gz1);
            GroundPiece("Ground South", gx0, gx1, gz0, hz0);
            GroundPiece("Ground West", gx0, hx0, hz0, hz1);
            GroundPiece("Ground East", hx1, gx1, hz0, hz1);
            Box("Path", g, new Vector3(0f, -0.444f, 10.2f), new Vector3(1.2f, 0.01f, 7.6f), M["Path"], false);

            for (float x = -14f; x <= 12f; x += 1.5f)
            {
                if (Mathf.Abs(x) < 1.2f) continue;
                Box("Fence Post", g, new Vector3(x, 0.1f, 13.5f), new Vector3(0.08f, 1.1f, 0.08f), M["Fence"], false);
            }
            Box("Fence Rail L", g, new Vector3(-7.6f, 0.4f, 13.5f), new Vector3(12.8f, 0.06f, 0.05f), M["Fence"], false);
            Box("Fence Rail R", g, new Vector3(6.6f, 0.4f, 13.5f), new Vector3(10.8f, 0.06f, 0.05f), M["Fence"], false);
            Box("Side Fence", g, new Vector3(-10.5f, 0.3f, -2f), new Vector3(0.06f, 1.5f, 20f), M["Fence"], false);
            Box("Back Fence", g, new Vector3(-2f, 0.3f, -12f), new Vector3(17f, 1.5f, 0.06f), M["Fence"], false);
            Box("Mailbox Post", g, new Vector3(1.3f, 0.1f, 13.2f), new Vector3(0.08f, 1.1f, 0.08f), M["Fence"], false);
            Box("Mailbox", g, new Vector3(1.3f, 0.72f, 13.2f), new Vector3(0.2f, 0.2f, 0.42f), M["Metal"], false);
            Box("Swing Frame", g, new Vector3(-1.5f, 1.0f, -10.5f), new Vector3(2.2f, 0.08f, 0.08f), M["Rust"], false);
            Box("Swing Seat", g, new Vector3(-1.5f, 0.15f, -10.5f), new Vector3(0.5f, 0.04f, 0.2f), M["Wood"], false);

            var trees = new[] { new Vector3(-4.2f, 0f, 8.8f), new Vector3(5.2f, 0f, 10.5f), new Vector3(-7.5f, 0f, 14.5f), new Vector3(3.6f, 0f, 16f), new Vector3(7.8f, 0f, 3.5f), new Vector3(9f, 0f, -4f), new Vector3(-9f, 0f, -7f), new Vector3(2.5f, 0f, -11f) };
            for (int i = 0; i < trees.Length; i++) DeadTree(g, trees[i] + Vector3.down * 0.45f, 3.2f + (i % 3) * 0.8f, i);

            // The houses across the street: pitched roofs and chimneys, so a lightning flash shows a street, not boxes.
            var houses = new[] { new Vector3(-9f, 0f, 24f), new Vector3(2f, 0f, 27f), new Vector3(12f, 0f, 23f) };
            for (int i = 0; i < houses.Length; i++)
            {
                var h = houses[i];
                Box("House", g, h + new Vector3(0f, 2.2f, 0f), new Vector3(7f, 5.4f, 5f), M["Siding"], false);
                for (int side = -1; side <= 1; side += 2)
                {
                    var roof = Box("Roof", g, h + new Vector3(0f, 5.85f, side * 1.32f), new Vector3(7.5f, 0.18f, 3.25f), M["Bark"], false);
                    roof.transform.rotation = Quaternion.Euler(side * 34f, 0f, 0f);
                }
                Box("Chimney", g, h + new Vector3(i == 1 ? -2.2f : 2.1f, 6.4f, 0.9f), new Vector3(0.55f, 1.6f, 0.55f), M["Brick"], false);
                Box("Porch Roof", g, h + new Vector3(0.6f, 2.35f, -2.85f), new Vector3(2.6f, 0.12f, 0.9f), M["Bark"], false);
                Box("Window Lit", g, h + new Vector3(-1.5f, 2.6f, -2.52f), new Vector3(0.8f, 1f, 0.02f), M["WindowLit"], false);
                Box("Window Dark", g, h + new Vector3(1.7f, 2.6f, -2.52f), new Vector3(0.8f, 1f, 0.02f), M["Black"], false);
                Box(i == 2 ? "Window Lit Upstairs" : "Window Dark Upstairs", g, h + new Vector3(i == 0 ? 1.7f : -1.5f, 4.25f, -2.52f),
                    new Vector3(0.7f, 0.85f, 0.02f), i == 2 ? M["WindowLit"] : M["Black"], false);
                Box("Door", g, h + new Vector3(0.6f, 0.55f, -2.52f), new Vector3(0.9f, 1.95f, 0.02f), M["DoorDark"], false);
            }
            StreetLamp(g, new Vector3(-2.3f, -0.45f, 13.1f), 11f, 14f);
            StreetLamp(g, new Vector3(8.6f, -0.45f, -1f), 8f, 12f);
            StreetLamp(g, new Vector3(-9.5f, -0.45f, -9f), 7f, 11f);
            Box("Shed", g, new Vector3(10.5f, 0.9f, -1.5f), new Vector3(2.2f, 2.8f, 3.4f), M["Fence"], false);
        }

        static void StreetLamp(Transform parent, Vector3 basePos, float intensity, float range)
        {
            Prim(PrimitiveType.Cylinder, "Lamp Pole", parent, basePos + new Vector3(0f, 1.9f, 0f), new Vector3(0.1f, 1.9f, 0.1f), M["Metal"], false);
            Box("Lamp Head", parent, basePos + new Vector3(0f, 3.85f, 0f), new Vector3(0.34f, 0.14f, 0.34f), M["Metal"], false);
            Sphere("Lamp Bulb", parent, basePos + new Vector3(0f, 3.74f, 0f), new Vector3(0.18f, 0.08f, 0.18f), M["StreetGlow"]);
            MakeLight("Street Light", parent, LightType.Point, basePos + new Vector3(0f, 3.5f, 0f), Quaternion.identity,
                new Color(0.62f, 0.72f, 1f), intensity, range, LightShadows.None, 1 << outsideLayer);
        }

        static void DeadTree(Transform parent, Vector3 basePos, float height, int seed)
        {
            var t = Group("Dead Tree", parent);
            t.position = basePos;
            Prim(PrimitiveType.Cylinder, "Trunk", t, basePos + new Vector3(0f, height * 0.5f, 0f), new Vector3(0.24f, height * 0.5f, 0.24f), M["Bark"], false);
            for (int i = 0; i < 4; i++)
            {
                float y = height * (0.55f + i * 0.12f);
                var b = Prim(PrimitiveType.Cylinder, "Branch", t, basePos + new Vector3(0f, y, 0f), new Vector3(0.07f, 0.7f - i * 0.1f, 0.07f), M["Bark"], false);
                b.transform.rotation = Quaternion.Euler(0f, seed * 47f + i * 97f, 50f + (i % 2) * 15f);
                b.transform.position += b.transform.up * (0.7f - i * 0.1f);
            }
        }

        static WindowFigure BuildWindowFigure(Transform parent)
        {
            var g = Group("Window Figure", parent);
            var wf = g.gameObject.AddComponent<WindowFigure>();
            var fig = Group("Figure", g);
            LocalPrim(PrimitiveType.Capsule, "Body", fig, new Vector3(0f, 0.9f, 0f), new Vector3(0.5f, 0.9f, 0.35f), Vector3.zero, M["Silhouette"]);
            LocalPrim(PrimitiveType.Sphere, "Head", fig, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.26f, Vector3.zero, M["Silhouette"]);
            foreach (var x in new[] { -0.055f, 0.055f })
                LocalPrim(PrimitiveType.Sphere, "Eye", fig, new Vector3(x, 1.98f, 0.12f), Vector3.one * 0.03f, Vector3.zero, M["EyeGlow"]);
            wf.figure = fig;
            wf.from = Marker("From", g, new Vector3(4.3f, -0.45f, -4.2f), 0f);
            wf.to = Marker("To", g, new Vector3(4.3f, -0.45f, 2.4f), 0f);
            wf.lookTarget = Marker("Look At", g, new Vector3(0f, 1.6f, -1f), 0f);
            fig.gameObject.SetActive(false);
            return wf;
        }

        static void BuildRain(Transform parent)
        {
            // Never over the house: front yard, east yard, west yard, back yard, and the nook by the kitchen window.
            var mat = M["Rain"];
            MakeRain(parent, "Rain Front", new Vector3(-1f, 6f, 12f), new Vector3(28f, 12.4f, 0.1f), 900f, mat);
            MakeRain(parent, "Rain East", new Vector3(8.7f, 6f, -2.6f), new Vector3(10.6f, 16.6f, 0.1f), 420f, mat);
            MakeRain(parent, "Rain West", new Vector3(-11.6f, 6f, -2.6f), new Vector3(9.2f, 16.6f, 0.1f), 380f, mat);
            MakeRain(parent, "Rain Back", new Vector3(-1.75f, 6f, -12.4f), new Vector3(10.2f, 7.4f, 0.1f), 320f, mat);
            MakeRain(parent, "Rain Nook", new Vector3(-4.85f, 6f, 2.05f), new Vector3(3.3f, 1.5f, 0.1f), 60f, mat);
            MakeRain(parent, "Rain Side Yard A", new Vector3(-3.825f, 6f, -6.1f), new Vector3(1.25f, 3.8f, 0.1f), 30f, mat);
            MakeRain(parent, "Rain Side Yard B", new Vector3(-6.175f, 6f, -6.1f), new Vector3(0.85f, 3.8f, 0.1f), 20f, mat);
            // Where it lands: the path and the porch steps, in the porch light and under the street lamp.
            MakeSplashes(parent, "Splashes Path", new Vector3(0f, -0.43f, 9.2f), new Vector2(7f, 6.4f), 220f, mat);
            MakeSplashes(parent, "Splashes Steps", new Vector3(0f, -0.13f, 5.95f), new Vector2(1.5f, 0.75f), 40f, mat);
            MakeSplashes(parent, "Splashes Street", new Vector3(-2.3f, -0.43f, 13.1f), new Vector2(5f, 4f), 120f, mat);
        }

        static void MakeSplashes(Transform parent, string name, Vector3 pos, Vector2 area, float rate, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(-90f, 0f, 0f));   // shape emits along +Z: straight up
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.028f);
            main.gravityModifier = 1.3f;
            main.startColor = new Color(0.82f, 0.87f, 0.96f, 0.75f);
            main.maxParticles = 800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area.x, area.y, 0.01f);
            shape.randomDirectionAmount = 0.55f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            var rr = go.GetComponent<ParticleSystemRenderer>();
            rr.renderMode = ParticleSystemRenderMode.Stretch;
            rr.velocityScale = 0.04f;
            rr.lengthScale = 1.2f;
            rr.sharedMaterial = mat;
            rr.shadowCastingMode = ShadowCastingMode.Off;
            rr.receiveShadows = false;
        }

        /// <summary>Dust hanging in the air. Lit particles: invisible in the dark, drifting through every lamp and torch beam.</summary>
        static void BuildDust(Transform parent)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-1.75f, 1.35f, -1.5f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 20f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.018f);
            main.startColor = new Color(1f, 0.95f, 0.86f, 0.55f);
            main.maxParticles = 260;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 16f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(9f, 2.3f, 7f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.05f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var rr = go.GetComponent<ParticleSystemRenderer>();
            rr.renderMode = ParticleSystemRenderMode.Billboard;
            rr.sharedMaterial = M["Dust"];
            rr.shadowCastingMode = ShadowCastingMode.Off;
            rr.receiveShadows = false;
        }

        static void MakeRain(Transform parent, string name, Vector3 pos, Vector3 boxSize, float rate, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 0.64f;   // from 6 m up at 10 m/s: gone at ground level, never into the basement
            main.startSpeed = 10f;
            main.startSize = 0.022f;
            main.startColor = new Color(0.62f, 0.68f, 0.8f, 0.32f);
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxSize;
            var rr = go.GetComponent<ParticleSystemRenderer>();
            rr.renderMode = ParticleSystemRenderMode.Stretch;
            rr.velocityScale = 0.055f;
            rr.lengthScale = 1f;
            rr.sharedMaterial = mat;
            rr.shadowCastingMode = ShadowCastingMode.Off;
            rr.receiveShadows = false;
        }

        // ================================================================== player

        static PlayerController BuildPlayer(Transform start, out Flashlight flashlight)
        {
            var go = new GameObject("Player");
            go.transform.SetPositionAndRotation(start.position, start.rotation);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.75f;
            cc.radius = 0.28f;
            cc.center = new Vector3(0f, 0.875f, 0f);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.02f;

            var head = new GameObject("Head").transform;
            head.SetParent(go.transform, false);
            head.localPosition = new Vector3(0f, 1.62f, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(head, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 60f;
            cam.fieldOfView = 68f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = FogColor;
            cam.cullingMask = ~(1 << tvOnlyLayer);
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
            data.dithering = true;   // no banding in the dark gradients

            var beamGo = new GameObject("Flashlight");
            beamGo.transform.SetParent(camGo.transform, false);
            beamGo.transform.localPosition = new Vector3(0.18f, -0.15f, 0.1f);
            var beam = beamGo.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.95f, 0.85f);
            beam.shadows = LightShadows.Soft;
            beam.shadowBias = 0.03f;
            beam.shadowNormalBias = 0.3f;
            beam.lightmapBakeType = LightmapBakeType.Realtime;
            beam.cookie = FlashlightCookie();
            beamGo.AddComponent<UniversalAdditionalLightData>();
            var beamData = new SerializedObject(beamGo.GetComponent<UniversalAdditionalLightData>());
            SetInt(beamData, "m_AdditionalLightsShadowResolutionTier", 2);
            beamData.ApplyModifiedPropertiesWithoutUndo();
            beam.enabled = false;
            flashlight = beamGo.AddComponent<Flashlight>();
            flashlight.beam = beam;

            var pc = go.AddComponent<PlayerController>();
            pc.cam = cam;
            pc.head = head;
            go.AddComponent<PlayerInteractor>().player = pc;
            return pc;
        }

        /// <summary>
        /// A real torch doesn't light a clean disc: a hot centre, a darker ring, a bright rim, a faint halo,
        /// and the scratches on the lens. Spot-light cookie.
        /// </summary>
        static Texture2D FlashlightCookie()
        {
            const int size = 128;
            string path = TexDir + "/T_FlashlightCookie.png";
            MakeTextureColor("T_FlashlightCookie", size, size, (x, y) =>
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                float hot = Mathf.Exp(-r * r / 0.035f);
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.42f) / 0.06f, 2f)) * 0.35f;
                float body = Mathf.Clamp01(1f - r / 0.55f) * 0.55f;
                float halo = Mathf.Clamp01(1f - r) * 0.16f;
                float scratch = 0.94f + 0.06f * Mathf.PerlinNoise(x * 0.3f, y * 0.04f);
                float v = Mathf.Clamp01((hot * 0.6f + ring + body + halo) * scratch);
                if (x == 0 || y == 0 || x == size - 1 || y == size - 1) v = 0f;   // nothing leaks past the clamp
                return new Color(v, v, v, v);
            }, true);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Cookie;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaSource = TextureImporterAlphaSource.FromGrayScale;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static VisitorAvatar MakeAvatar(string name, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.layer = outsideLayer;
            var av = go.AddComponent<VisitorAvatar>();
            av.litMaterial = M["AvatarBase"];
            av.unlitMaterial = M["UnlitBase"];
            av.vaporMaterial = M["Vapor"];
            av.characters = characters;
            return av;
        }

        // ================================================================== materials & textures

        static void EnsureFolders()
        {
            foreach (var p in new[] { Root, MatDir, TexDir, Root + "/Data", Root + "/Scenes", RenderDir })
            {
                if (AssetDatabase.IsValidFolder(p)) continue;
                var parent = Path.GetDirectoryName(p).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(p));
            }
        }

        static Dictionary<string, Material> CreateMaterials()
        {
            var tex = new Dictionary<string, Texture2D>
            {
                ["Wallpaper"] = MakeTexture("T_Wallpaper", 128, 128, WallpaperPx),
                ["Floor"] = MakeTexture("T_WoodFloor", 128, 128, WoodFloorPx),
                ["Tiles"] = MakeTexture("T_Tiles", 64, 64, TilesPx),
                ["Carpet"] = MakeTexture("T_Carpet", 64, 64, (x, y) => Noise(x, y, 0.85f, 1.05f, 3)),
                ["Ceiling"] = MakeTexture("T_Plaster", 64, 64, (x, y) => Noise(x, y, 0.82f, 1.02f, 7) * Stain(x, y, 64)),
                ["Siding"] = MakeTexture("T_Siding", 64, 64, (x, y) => ((y % 16) < 2 ? 0.55f : 1f) * Noise(x, y, 0.9f, 1.05f, 11)),
                ["Porch"] = MakeTexture("T_Planks", 64, 64, (x, y) => ((x % 16) < 1 ? 0.45f : 1f) * Noise(x / 4, y, 0.85f, 1.08f, 13)),
                ["Concrete"] = MakeTexture("T_Concrete", 64, 64, (x, y) => Noise(x, y, 0.75f, 1.05f, 17) * Stain(x, y, 64)),
                ["Brick"] = MakeTexture("T_Brick", 64, 64, BrickPx),
                ["Door"] = MakeTexture("T_DoorWood", 64, 128, (x, y) => (0.85f + 0.15f * Mathf.Sin(x * 0.9f + Mathf.Sin(y * 0.05f) * 3f)) * Noise(x, y, 0.92f, 1.04f, 19)),
                ["Grass"] = MakeTexture("T_Ground", 64, 64, (x, y) => Noise(x, y, 0.6f, 1.2f, 23)),
            };

            var m = new Dictionary<string, Material>();
            void Lit(string n, Color c, float smooth = 0.15f, float metal = 0f, Texture2D t = null) => m[n] = Mat(n, c, smooth, metal, false, null, t);
            void Unlit(string n, Color c) => m[n] = Mat(n, c, 0f, 0f, true, null, null);
            void Glow(string n, Color c, Color e) => m[n] = Mat(n, c, 0.2f, 0f, false, e, null);

            Lit("Floor", new Color(0.36f, 0.25f, 0.17f), 0.3f, 0f, tex["Floor"]);
            Lit("Wallpaper", new Color(0.42f, 0.45f, 0.38f), 0.05f, 0f, tex["Wallpaper"]);
            Lit("KitchenWall", new Color(0.55f, 0.5f, 0.38f), 0.1f, 0f, tex["Ceiling"]);
            Lit("BedroomWall", new Color(0.36f, 0.33f, 0.42f), 0.05f, 0f, tex["Wallpaper"]);
            Lit("BathWall", new Color(0.62f, 0.66f, 0.64f), 0.4f, 0f, tex["Tiles"]);
            Lit("Ceiling", new Color(0.32f, 0.31f, 0.29f), 0.05f, 0f, tex["Ceiling"]);
            Lit("Tiles", new Color(0.62f, 0.6f, 0.55f), 0.45f, 0f, tex["Tiles"]);
            Lit("Carpet", new Color(0.3f, 0.2f, 0.24f), 0.02f, 0f, tex["Carpet"]);
            Lit("Concrete", new Color(0.32f, 0.31f, 0.3f), 0.1f, 0f, tex["Concrete"]);
            Lit("Brick", new Color(0.42f, 0.25f, 0.2f), 0.05f, 0f, tex["Brick"]);
            Lit("Trim", new Color(0.13f, 0.085f, 0.06f), 0.25f);
            Lit("Door", new Color(0.42f, 0.22f, 0.13f), 0.3f, 0f, tex["Door"]);
            Lit("DoorDark", new Color(0.3f, 0.16f, 0.1f), 0.3f, 0f, tex["Door"]);
            Lit("Brass", new Color(0.72f, 0.56f, 0.26f), 0.6f, 0.7f);
            Lit("Metal", new Color(0.16f, 0.16f, 0.17f), 0.4f, 0.4f);
            Lit("Rust", new Color(0.3f, 0.15f, 0.08f), 0.2f, 0.3f, tex["Concrete"]);
            Lit("Black", new Color(0.02f, 0.02f, 0.02f), 0.3f);
            Lit("Intercom", new Color(0.74f, 0.72f, 0.66f), 0.35f);
            Unlit("ButtonGlow", new Color(1.6f, 0.45f, 0.15f));
            Unlit("LedGreen", new Color(0.3f, 1.8f, 0.4f));
            Lit("Paper", new Color(0.86f, 0.82f, 0.7f), 0.05f);
            Lit("Photo", new Color(0.75f, 0.72f, 0.65f), 0.5f);
            Lit("Drawing", new Color(0.92f, 0.86f, 0.55f), 0.05f);
            Lit("Ink", new Color(0.1f, 0.08f, 0.07f), 0.05f);
            Lit("SignRed", new Color(0.7f, 0.07f, 0.05f), 0.2f);
            Lit("Mat", new Color(0.18f, 0.1f, 0.06f), 0.02f);
            Lit("Wood", new Color(0.3f, 0.19f, 0.11f), 0.3f, 0f, tex["Door"]);
            Lit("Cabinet", new Color(0.4f, 0.32f, 0.22f), 0.25f, 0f, tex["Door"]);
            Lit("Countertop", new Color(0.55f, 0.52f, 0.48f), 0.5f);
            Lit("Fridge", new Color(0.8f, 0.78f, 0.7f), 0.5f);
            Lit("Porcelain", new Color(0.86f, 0.86f, 0.84f), 0.7f);
            Lit("Mirror", new Color(0.12f, 0.13f, 0.14f), 0.95f, 1f);
            Lit("ShowerCurtain", new Color(0.62f, 0.66f, 0.6f), 0.2f);
            Lit("Sheet", new Color(0.7f, 0.68f, 0.62f), 0.05f);
            Lit("Blanket", new Color(0.24f, 0.14f, 0.2f), 0.05f, 0f, tex["Carpet"]);
            Lit("Cardboard", new Color(0.55f, 0.42f, 0.28f), 0.05f);
            Lit("Radio", new Color(0.15f, 0.2f, 0.3f), 0.4f);
            Lit("Rabbit", new Color(0.7f, 0.62f, 0.58f), 0.05f, 0f, tex["Carpet"]);
            Lit("Pumpkin", new Color(0.85f, 0.36f, 0.05f), 0.25f);
            Lit("Stem", new Color(0.2f, 0.25f, 0.1f), 0.1f);
            Unlit("PumpkinGlow", new Color(2.8f, 0.95f, 0.1f));
            Lit("Coat", new Color(0.12f, 0.17f, 0.13f), 0.1f);
            Glow("LampGlow", new Color(0.9f, 0.85f, 0.75f), new Color(1.2f, 0.95f, 0.7f));
            Unlit("LampShade", new Color(0.95f, 0.5f, 0.2f));   // glowing fabric; lit, the bulb inside burns it white
            Lit("Picture", new Color(0.2f, 0.22f, 0.25f), 0.5f);
            Lit("Rug", new Color(0.32f, 0.08f, 0.07f), 0.02f, 0f, tex["Carpet"]);
            Lit("Couch", new Color(0.36f, 0.12f, 0.1f), 0.1f, 0f, tex["Carpet"]);
            Lit("Cushion", new Color(0.42f, 0.15f, 0.12f), 0.1f, 0f, tex["Carpet"]);
            Lit("TV", new Color(0.07f, 0.07f, 0.07f), 0.4f);
            Unlit("Screen", new Color(0.02f, 0.025f, 0.03f));
            Lit("ClockFace", new Color(0.85f, 0.83f, 0.76f), 0.3f);
            Lit("WindowFrame", new Color(0.55f, 0.53f, 0.48f), 0.2f);
            Lit("Curtain", new Color(0.3f, 0.14f, 0.26f), 0.05f, 0f, tex["Carpet"]);
            Lit("Blinds", new Color(0.78f, 0.74f, 0.66f), 0.2f);
            Lit("PorchFloor", new Color(0.42f, 0.35f, 0.28f), 0.32f, 0f, tex["Porch"]);
            Lit("Siding", new Color(0.24f, 0.26f, 0.3f), 0.32f, 0f, tex["Siding"]);
            Lit("PostWhite", new Color(0.55f, 0.53f, 0.48f), 0.15f);
            Unlit("Bulb", new Color(2.2f, 1.75f, 1.2f));
            Lit("Ground", new Color(0.12f, 0.13f, 0.1f), 0.5f, 0f, tex["Grass"]);
            Lit("Path", new Color(0.16f, 0.16f, 0.17f), 0.72f, 0f, tex["Concrete"]);
            Lit("Fence", new Color(0.14f, 0.12f, 0.1f), 0.3f);
            Lit("Bark", new Color(0.07f, 0.06f, 0.05f), 0.3f);
            Unlit("WindowLit", new Color(0.9f, 0.62f, 0.25f));
            Unlit("StreetGlow", new Color(1.4f, 1.6f, 2.2f));
            Lit("Silhouette", new Color(0.01f, 0.01f, 0.012f), 0.1f);
            Lit("PaleSkin", new Color(0.78f, 0.74f, 0.7f), 0.3f);
            Unlit("EyeGlow", new Color(1.3f, 1.25f, 0.8f));
            Lit("AvatarBase", Color.white, 0.15f);
            m["NightSky"] = NightSkyMaterial();
            Unlit("UnlitBase", Color.white);
            m["Glass"] = TransparentLit("Glass", new Color(0.55f, 0.62f, 0.7f, 0.16f), 0.95f);
            // Rain is lit: it sparkles in the porch light and the street lamps and all but vanishes in the dark,
            // with just enough glow of its own to read as a curtain when you look out.
            var softDot = MakeTexture("T_SoftDot", 64, 64, SoftDotPx, true);
            m["Rain"] = ParticleMaterial("Rain", "Universal Render Pipeline/Particles/Lit", new Color(0.8f, 0.85f, 0.95f, 0.55f), null);
            m["Rain"].SetColor("_EmissionColor", new Color(0.11f, 0.12f, 0.15f));
            m["Rain"].globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m["Rain"].EnableKeyword("_EMISSION");
            m["Vapor"] = ParticleMaterial("Vapor", "Universal Render Pipeline/Particles/Lit", new Color(0.92f, 0.94f, 0.97f, 0.65f), softDot);
            m["Dust"] = ParticleMaterial("Dust", "Universal Render Pipeline/Particles/Lit", new Color(1f, 0.96f, 0.88f, 0.6f), softDot);
            return m;
        }

        static Material Mat(string name, Color color, float smoothness, float metallic, bool unlit, Color? emission, Texture2D texture)
        {
            string path = MatDir + "/M_" + name + ".mat";
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", texture);
            if (!unlit)
            {
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Metallic", metallic);
            }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The night sky: a 360 panorama of storm cloud, lit faintly from behind by the moon.</summary>
        static Material NightSkyMaterial()
        {
            var shader = Shader.Find("Skybox/Panoramic");
            string path = MatDir + "/M_NightSky.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetTexture("_MainTex", NightSkyTexture());
            m.SetFloat("_Mapping", 1f);     // latitude-longitude layout
            m.SetFloat("_ImageType", 0f);   // full 360
            m.SetFloat("_Exposure", 1f);
            m.SetFloat("_Rotation", 0f);
            m.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
            m.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            m.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D NightSkyTexture()
        {
            const int w = 1024, h = 512;
            var moon = new Vector3(0.55f, 0.42f, 0.72f).normalized;   // opposite the moonlight's direction
            Color zenith = new Color(0.006f, 0.008f, 0.015f), horizon = FogColor, cloudLit = new Color(0.1f, 0.11f, 0.14f);
            MakeTextureColor("T_NightSky", w, h, (x, y) =>
            {
                // Inverse of the panoramic shader's lookup: u = 0.5 - atan2(z, x) / 2pi, v = 1 - acos(y) / pi.
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                float lon = (0.5f - u) * 2f * Mathf.PI, lat = (1f - v) * Mathf.PI;
                var dir = new Vector3(Mathf.Cos(lon) * Mathf.Sin(lat), Mathf.Cos(lat), Mathf.Sin(lon) * Mathf.Sin(lat));
                float elev = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) / (Mathf.PI * 0.5f);
                Color c = elev <= 0f ? horizon * Mathf.Lerp(1f, 0.6f, -elev) : Color.Lerp(horizon, zenith, Mathf.Pow(elev, 0.5f));
                if (elev > -0.05f)
                {
                    // Clouds on a flat layer overhead, so they bunch up toward the horizon.
                    float k = 1f / (Mathf.Max(0f, dir.y) + 0.12f);
                    float cx = dir.x * k * 1.6f + 40f, cz = dir.z * k * 1.6f + 17f;
                    float n = Mathf.PerlinNoise(cx, cz) * 0.55f + Mathf.PerlinNoise(cx * 2.3f + 5f, cz * 2.3f) * 0.3f + Mathf.PerlinNoise(cx * 5.1f, cz * 5.1f + 9f) * 0.15f;
                    float cloud = Mathf.SmoothStep(0.42f, 0.78f, n) * Mathf.Clamp01(1f - elev * 0.6f) * Mathf.Clamp01((elev + 0.05f) * 8f);
                    float lit = 0.55f + 0.45f * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(dir, moon)), 3f);
                    c = Color.Lerp(c, cloudLit * lit * 1.4f, cloud * 0.8f);
                }
                float mo = Mathf.Max(0f, Vector3.Dot(dir, moon));
                c += new Color(0.3f, 0.36f, 0.48f) * (Mathf.Pow(mo, 60f) * 0.5f + Mathf.Pow(mo, 8f) * 0.06f);
                c.a = 1f;
                return c;
            }, true);
            string path = TexDir + "/T_NightSky.png";
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapModeU = TextureWrapMode.Repeat;
            imp.wrapModeV = TextureWrapMode.Clamp;
            imp.alphaIsTransparency = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material TransparentLit(string name, Color color, float smoothness)
        {
            var m = Mat(name, color, smoothness, 0f, false, null, null);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material ParticleMaterial(string name, string shaderName, Color color, Texture2D texture)
        {
            string path = MatDir + "/M_" + name + ".mat";
            var shader = Shader.Find(shaderName);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            if (texture != null) m.SetTexture("_BaseMap", texture);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ procedural textures

        static Texture2D MakeTexture(string name, int w, int h, Func<int, int, float> lum, bool alphaOnly = false)
        {
            return MakeTextureColor(name, w, h, (x, y) =>
            {
                float v = lum(x, y);
                return alphaOnly ? new Color(1f, 1f, 1f, Mathf.Clamp01(v)) : new Color(v, v, v, 1f);
            }, alphaOnly);
        }

        static Texture2D MakeTextureColor(string name, int w, int h, Func<int, int, Color> px, bool smooth)
        {
            string path = TexDir + "/" + name + ".png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var data = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                data[y * w + x] = px(x, y);
            tex.SetPixels(data);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.filterMode = smooth ? FilterMode.Bilinear : FilterMode.Point;
            imp.wrapMode = smooth ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.mipmapEnabled = !smooth;
            imp.alphaIsTransparency = smooth;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int n = x * 374761393 + y * 668265263 + seed * 1442695041;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0xffff) / 65535f;
            }
        }

        static float Noise(int x, int y, float lo, float hi, int seed) => Mathf.Lerp(lo, hi, Hash(x, y, seed) * 0.6f + Hash(x / 4, y / 4, seed + 1) * 0.4f);

        static float Stain(int x, int y, int size)
        {
            float cx = size * 0.3f, cy = size * 0.6f;
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / (size * 0.35f);
            return Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(d));
        }

        static float WallpaperPx(int x, int y)
        {
            float stripe = (x % 32) < 3 ? 0.82f : 1f;
            int mx = x % 32, my = y % 32;
            float motif = (Mathf.Abs(mx - 16) + Mathf.Abs(my - 16)) < 5 ? 0.86f : 1f;
            float damp = Mathf.Lerp(0.78f, 1f, Mathf.Clamp01(y / 50f + Hash(x / 16, 0, 5) * 0.3f));
            return stripe * motif * damp * Noise(x, y, 0.93f, 1.03f, 1);
        }

        static float WoodFloorPx(int x, int y)
        {
            int plank = y / 16;
            float tone = 0.8f + Hash(plank, (x + plank * 37) / 64, 3) * 0.3f;
            float seam = (y % 16) == 0 || ((x + plank * 23) % 64) == 0 ? 0.45f : 1f;
            float grain = 0.92f + 0.08f * Mathf.Sin(x * 0.35f + Mathf.Sin(y * 0.6f + plank) * 2f);
            return tone * seam * grain;
        }

        static float TilesPx(int x, int y)
        {
            bool grout = (x % 16) == 0 || (y % 16) == 0;
            bool dark = ((x / 16) + (y / 16)) % 2 == 0;
            float v = grout ? 0.35f : dark ? 0.55f : 1.05f;
            return v * Noise(x, y, 0.88f, 1.02f, 9);
        }

        static float BrickPx(int x, int y)
        {
            int row = y / 8;
            int off = (row % 2) * 8;
            bool mortar = (y % 8) == 0 || ((x + off) % 16) == 0;
            float tone = 0.8f + Hash((x + off) / 16, row, 31) * 0.35f;
            return mortar ? 0.45f : tone * Noise(x, y, 0.9f, 1.05f, 33);
        }

        static float SoftDotPx(int x, int y)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f)) / 32f;
            return Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
        }

        // ------------------------------------------------------------------ post-processing

        static VolumeProfile CreateProfile(string path, Action<VolumeProfile> fill)
        {
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            fill(p);
            foreach (var c in p.components)
            {
                c.name = c.GetType().Name;
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        /// <summary>
        /// The base grade: filmic (ACES) contrast with crushed blacks, cold teal shadows against warm tungsten
        /// highlights, drained colour, grain that lives in the dark, bloom with a dirty lens around every bulb.
        /// </summary>
        static void FillGlobal(VolumeProfile p)
        {
            var tm = p.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.75f);
            ca.contrast.Override(22f);
            ca.saturation.Override(-20f);
            ca.colorFilter.Override(new Color(0.93f, 0.97f, 1f));
            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(-8f);
            wb.tint.Override(4f);
            var smh = p.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.9f, 1f, 1.12f, -0.02f));
            smh.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
            smh.highlights.Override(new Vector4(1.08f, 1f, 0.9f, 0f));
            smh.shadowsEnd.Override(0.35f);
            var lgg = p.Add<LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(1f, 1f, 1f, -0.025f));
            var vg = p.Add<Vignette>(true);
            vg.intensity.Override(0.42f);
            vg.smoothness.Override(0.5f);
            vg.color.Override(Color.black);
            var fg = p.Add<FilmGrain>(true);
            fg.type.Override(FilmGrainLookup.Medium4);
            fg.intensity.Override(0.42f);
            fg.response.Override(0.78f);
            var bl = p.Add<Bloom>(true);
            bl.threshold.Override(0.85f);
            bl.intensity.Override(0.95f);
            bl.scatter.Override(0.7f);
            bl.tint.Override(new Color(1f, 0.9f, 0.8f));
            var dirt = LensDirtTexture();
            if (dirt != null)
            {
                bl.dirtTexture.Override(dirt);
                bl.dirtIntensity.Override(1.1f);
            }
            var cab = p.Add<ChromaticAberration>(true);
            cab.intensity.Override(0.14f);
        }

        static void FillPeephole(VolumeProfile p)
        {
            var ld = p.Add<LensDistortion>(true);
            ld.intensity.Override(0.62f);
            ld.scale.Override(0.82f);
            var vg = p.Add<Vignette>(true);
            vg.intensity.Override(0.55f);
            vg.smoothness.Override(0.45f);
            vg.rounded.Override(true);
            var cab = p.Add<ChromaticAberration>(true);
            cab.intensity.Override(0.7f);
            var ca = p.Add<ColorAdjustments>(true);
            ca.saturation.Override(-35f);
            ca.postExposure.Override(0.3f);
            ca.colorFilter.Override(new Color(0.92f, 1f, 0.94f));
            var fg = p.Add<FilmGrain>(true);
            fg.type.Override(FilmGrainLookup.Thin2);
            fg.intensity.Override(0.6f);
        }

        /// <summary>Fear, blended in by <see cref="MoodVolume"/> as the heartbeat rises.</summary>
        static void FillDread(VolumeProfile p)
        {
            var ca = p.Add<ColorAdjustments>(true);
            ca.saturation.Override(-60f);
            ca.contrast.Override(35f);
            ca.postExposure.Override(-0.2f);
            var vg = p.Add<Vignette>(true);
            vg.intensity.Override(0.6f);
            vg.smoothness.Override(0.6f);
            vg.color.Override(new Color(0.16f, 0f, 0f));
            var cab = p.Add<ChromaticAberration>(true);
            cab.intensity.Override(0.65f);
            var ld = p.Add<LensDistortion>(true);
            ld.intensity.Override(-0.14f);
            var fg = p.Add<FilmGrain>(true);
            fg.type.Override(FilmGrainLookup.Large02);
            fg.intensity.Override(1f);
        }

        /// <summary>The basement: sick green, close and grainy.</summary>
        static void FillBasement(VolumeProfile p)
        {
            var ca = p.Add<ColorAdjustments>(true);
            ca.saturation.Override(-42f);
            ca.contrast.Override(30f);
            ca.colorFilter.Override(new Color(0.84f, 0.96f, 0.8f));
            var vg = p.Add<Vignette>(true);
            vg.intensity.Override(0.55f);
            vg.smoothness.Override(0.55f);
            var fg = p.Add<FilmGrain>(true);
            fg.type.Override(FilmGrainLookup.Medium6);
            fg.intensity.Override(0.85f);
        }

        /// <summary>Smudges and specks on the lens that light up when a bright light blooms across them.</summary>
        static Texture2D LensDirtTexture()
        {
            const int size = 256;
            var blobs = new List<Vector4>();
            var rng = new System.Random(1013);
            for (int i = 0; i < 46; i++)
                blobs.Add(new Vector4((float)rng.NextDouble() * size, (float)rng.NextDouble() * size, 4f + (float)rng.NextDouble() * (i < 8 ? 40f : 12f), 0.15f + (float)rng.NextDouble() * 0.5f));
            return MakeTextureColor("T_LensDirt", size, size, (x, y) =>
            {
                float v = 0f;
                foreach (var b in blobs)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(b.x, b.y)) / b.z;
                    if (d < 1f) v += (1f - d * d) * b.w;
                }
                // a long greasy wipe across the lower third
                float streak = Mathf.Exp(-Mathf.Pow((y - x * 0.35f - 50f) / 9f, 2f)) * 0.35f;
                v = Mathf.Clamp01(v * 0.8f + streak) * (0.85f + Hash(x, y, 71) * 0.15f);
                return new Color(v, v * 0.97f, v * 0.92f, 1f);
            }, true);
        }

        // ================================================================== helpers

        static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static Transform Marker(string name, Transform parent, Vector3 position, float yaw)
        {
            var t = Group(name, parent);
            t.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        static GameObject Box(string name, Transform parent, Vector3 worldPos, Vector3 size, Material mat, bool collider = true)
            => Prim(PrimitiveType.Cube, name, parent, worldPos, size, mat, collider);

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 worldPos, Vector3 scale, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static GameObject Sphere(string name, Transform parent, Vector3 worldPos, float diameter, Material mat)
            => Prim(PrimitiveType.Sphere, name, parent, worldPos, Vector3.one * diameter, mat, false);

        static GameObject Sphere(string name, Transform parent, Vector3 worldPos, Vector3 scale, Material mat)
            => Prim(PrimitiveType.Sphere, name, parent, worldPos, scale, mat, false);

        static GameObject LocalBox(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject LocalPrim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 size, Vector3 euler, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Light MakeLight(string name, Transform parent, LightType type, Vector3 pos, Quaternion rot, Color color, float intensity, float range, LightShadows shadows, int cullingMask = ~0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            if (type != LightType.Directional) l.range = range;
            l.shadows = shadows;
            l.cullingMask = cullingMask;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            if (shadows != LightShadows.None)
            {
                l.shadowStrength = 1f;
                l.shadowBias = 0.03f;
                l.shadowNormalBias = 0.35f;
                l.shadowNearPlane = 0.1f;
            }
            go.AddComponent<UniversalAdditionalLightData>();
            if (shadows != LightShadows.None && type != LightType.Directional)
            {
                // Point lights render six shadow faces: keep them small (and soft) so the atlas never overflows.
                var so = new SerializedObject(go.GetComponent<UniversalAdditionalLightData>());
                SetInt(so, "m_AdditionalLightsShadowResolutionTier", type == LightType.Point ? 0 : 2);
                so.ApplyModifiedPropertiesWithoutUndo();
                l.shadowBias = 0.05f;
                l.shadowNormalBias = 0.6f;
            }
            return l;
        }

        static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            for (int i = 8; i < 32; i++)
            {
                var p = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(p.stringValue)) continue;
                p.stringValue = name;
                tm.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            Debug.LogWarning("[Don't Answer] No free layer for '" + name + "'.");
            return 0;
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursively(c.gameObject, layer);
        }

        static void SetFloat(SerializedObject so, string prop, float v) { var p = so.FindProperty(prop); if (p != null) p.floatValue = v; }
        static void SetInt(SerializedObject so, string prop, int v) { var p = so.FindProperty(prop); if (p != null) p.intValue = v; }
        static void SetBool(SerializedObject so, string prop, bool v) { var p = so.FindProperty(prop); if (p != null) p.boolValue = v; }
    }
}
