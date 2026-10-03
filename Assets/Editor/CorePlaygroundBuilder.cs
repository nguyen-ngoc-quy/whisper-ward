using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using WhisperWard.AI.Navigation;
using WhisperWard.AI.Perception;
using WhisperWard.AI.FSM;
using WhisperWard.Core.Camera;
using WhisperWard.Core.Player;

namespace WhisperWard.Editor
{
    /// <summary>
    /// Automated builder for the CorePlayground 3D test arena in Unity 6 LTS.
    /// Satisfies Story SCENE-01 (Geometry Blockout & Moonlit Lighting Setup)
    /// and adheres to ADR-0002, ADR-0006, ADR-0007, and ADR-0008.
    /// </summary>
    public static class CorePlaygroundBuilder
    {
        private const string ScenePath = "Assets/Scenes/CorePlayground.unity";
        private const string MaterialsDir = "Assets/Materials";
        private const string SettingsDir = "Assets/Settings";
        private const string FloorMatPath = "Assets/Materials/Mat_Floor_Slate.mat";
        private const string WallMatPath = "Assets/Materials/Mat_Wall_Concrete.mat";
        private const string PlayerMatPath = "Assets/Materials/Mat_Player_Teal.mat";
        private const string GuardMatPath = "Assets/Materials/Mat_Guard_HostileRed.mat";
        private const string VolumeProfilePath = "Assets/Settings/PlaygroundVolumeProfile.asset";

        private const int WorldLayer = 20; // E20 Solid layer
        private const int HideSpotTriggerLayer = 10;

        [MenuItem("Whisper Ward/Build CorePlayground Scene")]
        public static void BuildScene()
        {
            // 1. Create or open scene
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create or load distinct materials (SCENE-06, AC-SCENE-18)
            Material floorMat = GetOrCreateMaterial(FloorMatPath, new Color(0.110f, 0.133f, 0.169f, 1f)); // #1C222B Slate dark
            Material wallMat = GetOrCreateMaterial(WallMatPath, new Color(0.220f, 0.251f, 0.294f, 1f)); // #38404B Concrete grey

            // 2. Setup Lighting & Environment Roots
            GameObject environmentRoot = new GameObject("--- ENVIRONMENT (Static E20) ---");
            environmentRoot.isStatic = true;

            GameObject lightingRoot = new GameObject("--- LIGHTING ---");

            // 3. Create Moonlit Directional Light
            GameObject moonLightGo = new GameObject("Directional Light (Moonlit)");
            moonLightGo.transform.SetParent(lightingRoot.transform);
            moonLightGo.transform.position = new Vector3(0f, 15f, 0f);
            moonLightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Light moonLight = moonLightGo.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.627f, 0.769f, 0.886f, 1.0f); // #A0C4E2
            moonLight.intensity = 0.85f;
            moonLight.shadows = LightShadows.Soft;
            moonLight.shadowBias = 0.05f;
            moonLight.shadowNormalBias = 0.4f;

            // 4. Create Main Floor (20m x 20m)
            GameObject floor = CreateSolidCube(
                "Arena_Floor_20x20",
                new Vector3(0f, -0.1f, 0f),
                new Vector3(20.0f, 0.2f, 20.0f),
                environmentRoot.transform,
                floorMat
            );

            // Configure NavMeshSurface on floor (SCENE-04, AC-SCENE-13)
            Type navSurfaceType = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (navSurfaceType != null)
            {
                Component surface = floor.AddComponent(navSurfaceType);
                var buildMethod = navSurfaceType.GetMethod("BuildNavMesh", Type.EmptyTypes);
                if (buildMethod != null)
                {
                    try { buildMethod.Invoke(surface, null); } catch { /* Ignore headless build exceptions */ }
                }
            }

            // 5. Create Perimeter Walls (Height: 3.0m, Thickness: 0.4m)
            GameObject perimeterRoot = new GameObject("Perimeter_Walls");
            perimeterRoot.transform.SetParent(environmentRoot.transform);
            perimeterRoot.isStatic = true;

            CreateSolidCube("Wall_North", new Vector3(0f, 1.5f, 10.2f), new Vector3(20.8f, 3.0f, 0.4f), perimeterRoot.transform, wallMat);
            CreateSolidCube("Wall_South", new Vector3(0f, 1.5f, -10.2f), new Vector3(20.8f, 3.0f, 0.4f), perimeterRoot.transform, wallMat);
            CreateSolidCube("Wall_East", new Vector3(10.2f, 1.5f, 0f), new Vector3(0.4f, 3.0f, 20.8f), perimeterRoot.transform, wallMat);
            CreateSolidCube("Wall_West", new Vector3(-10.2f, 1.5f, 0f), new Vector3(0.4f, 3.0f, 20.8f), perimeterRoot.transform, wallMat);

            // 6. Create Standardized Corridors (ADR-0007, AC-NAV-08)
            GameObject corridorRoot = new GameObject("Corridors");
            corridorRoot.transform.SetParent(environmentRoot.transform);
            corridorRoot.isStatic = true;

            // Main Corridor: Clear width 1.50m running Z = -4.0m to +6.0m
            // Inner faces at X = -0.75m and X = +0.75m. Wall thickness 0.40m.
            // Center of Left Wall: X = -0.75 - 0.20 = -0.95m
            // Center of Right Wall: X = +0.75 + 0.20 = +0.95m
            CreateSolidCube("MainCorridor_Wall_Left", new Vector3(-0.95f, 1.5f, 1.0f), new Vector3(0.4f, 3.0f, 10.0f), corridorRoot.transform, wallMat);
            CreateSolidCube("MainCorridor_Wall_Right", new Vector3(0.95f, 1.5f, 1.0f), new Vector3(0.4f, 3.0f, 10.0f), corridorRoot.transform, wallMat);

            // Branch Corridor: Clear width 1.20m running X = 0.95m to +6.0m at Z = 3.0m
            // Inner faces at Z = 2.40m and Z = 3.60m (clear width = 1.20m)
            CreateSolidCube("BranchCorridor_Wall_South", new Vector3(3.5f, 1.5f, 2.2f), new Vector3(5.1f, 3.0f, 0.4f), corridorRoot.transform, wallMat);
            CreateSolidCube("BranchCorridor_Wall_North", new Vector3(3.5f, 1.5f, 3.8f), new Vector3(5.1f, 3.0f, 0.4f), corridorRoot.transform, wallMat);

            // 7. Create Low-Headroom HideSpot Alcove (ADR-0006, AC-SCENE-03)
            // Depth 2.0m, Width 1.5m, Ceiling underside at Y = 1.40m (< 1.80m Stand Height)
            GameObject alcoveRoot = new GameObject("HideSpot_Alcove");
            alcoveRoot.transform.SetParent(environmentRoot.transform);
            alcoveRoot.isStatic = true;

            // Back wall at X = -8.0m, Z = -5.0m
            CreateSolidCube("Alcove_BackWall", new Vector3(-8.0f, 1.5f, -5.0f), new Vector3(0.4f, 3.0f, 1.9f), alcoveRoot.transform, wallMat);
            CreateSolidCube("Alcove_SideWall_North", new Vector3(-7.0f, 1.5f, -4.05f), new Vector3(2.0f, 3.0f, 0.4f), alcoveRoot.transform, wallMat);
            CreateSolidCube("Alcove_SideWall_South", new Vector3(-7.0f, 1.5f, -5.95f), new Vector3(2.0f, 3.0f, 0.4f), alcoveRoot.transform, wallMat);

            // Ceiling Slab: Thickness 0.20m, Center Y = 1.40 + 0.10 = 1.50m (Underside = 1.40m)
            CreateSolidCube("Alcove_LowCeiling_H1.40m", new Vector3(-7.0f, 1.50f, -5.0f), new Vector3(2.0f, 0.2f, 1.9f), alcoveRoot.transform, wallMat);

            // Post-Processing Global Volume (SCENE-06, AC-SCENE-19)
            SetupPostProcessingVolume(environmentRoot.transform);

            // HideSpot Trigger Volume (Layer 10: HideSpotTrigger, AC-SCENE-12)
            GameObject triggerGo = new GameObject("HideSpot_Trigger_Volume");
            triggerGo.transform.SetParent(alcoveRoot.transform);
            triggerGo.transform.position = new Vector3(-7.0f, 0.70f, -5.0f);
            triggerGo.layer = ResolveLayer(HideSpotTriggerLayer, "HideSpotTrigger");
            BoxCollider triggerCol = triggerGo.AddComponent<BoxCollider>();
            triggerCol.isTrigger = true;
            triggerCol.size = new Vector3(1.8f, 1.4f, 1.5f);

            HideSpotTriggerZone triggerZone = triggerGo.AddComponent<HideSpotTriggerZone>();
            triggerZone.Configure(Vector3.right, new Vector3(0.5f, 0.4f, 0f));

            // 8. Create Waypoint Markers for Patrol NPC
            GameObject waypointsRoot = new GameObject("--- WAYPOINTS ---");
            GameObject wpA = new GameObject("Waypoint_A (Corridor South)");
            wpA.transform.SetParent(waypointsRoot.transform);
            wpA.transform.position = new Vector3(0f, 0.0f, -3.0f);

            GameObject wpB = new GameObject("Waypoint_B (Corridor North)");
            wpB.transform.SetParent(waypointsRoot.transform);
            wpB.transform.position = new Vector3(0f, 0.0f, 5.0f);

            // 9. Create Spawn Points
            GameObject spawnRoot = new GameObject("--- SPAWN POINTS ---");
            GameObject playerSpawn = new GameObject("Player_SpawnPoint");
            playerSpawn.transform.SetParent(spawnRoot.transform);
            playerSpawn.transform.position = new Vector3(-5.0f, 0.0f, 0.0f);
            playerSpawn.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            GameObject guardSpawn = new GameObject("Guard_SpawnPoint");
            guardSpawn.transform.SetParent(spawnRoot.transform);
            guardSpawn.transform.position = new Vector3(0f, 0.0f, -3.0f);

            // 10. Build Player Capsule & Guard NPC Prefabs & Instantiate at Spawn Points (SCENE-02, SCENE-04)
            GameObject charactersRoot = new GameObject("--- CHARACTERS ---");
            GameObject playerRoot = BuildPlayerCapsule(playerSpawn.transform.position, playerSpawn.transform.rotation, charactersRoot.transform);
            GameObject guardRoot = BuildGuardPatrolNPC(guardSpawn.transform.position, guardSpawn.transform.rotation, charactersRoot.transform, new Transform[] { wpA.transform, wpB.transform });

            // Wire player reference to guard perception and FSM systems (Sprint 03)
            VisionConeSensor guardSensor = guardRoot.GetComponent<VisionConeSensor>();
            if (guardSensor != null)
            {
                guardSensor.Configure(guardRoot.transform, playerRoot.transform);
            }

            GuardFSMRuntimeController guardFsm = guardRoot.GetComponent<GuardFSMRuntimeController>();
            if (guardFsm != null)
            {
                guardFsm.Configure(
                    guardRoot.GetComponent<NavMeshAgent>(),
                    guardSensor,
                    guardRoot.GetComponent<SuspicionAccumulator>(),
                    guardRoot.GetComponent<SimplePatrolDriver>(),
                    playerRoot.transform);
            }

            // 11. Create Main Camera with CameraOrbitDriver & CinemachineBrain (SCENE-03, AC-SCENE-09)
            GameObject cameraRoot = new GameObject("--- CAMERA ---");
            GameObject mainCamGo = new GameObject("Main Camera");
            mainCamGo.tag = "MainCamera";
            mainCamGo.transform.SetParent(cameraRoot.transform);
            mainCamGo.transform.position = new Vector3(-7.80f, 1.35f, 0.0f);
            mainCamGo.transform.rotation = Quaternion.Euler(10f, 90f, 0f);

            Camera mainCam = mainCamGo.AddComponent<Camera>();
            mainCam.nearClipPlane = 0.10f;
            mainCam.farClipPlane = 100.0f;
            mainCam.fieldOfView = 60.0f;
            mainCamGo.AddComponent<AudioListener>();

            // Mount CinemachineBrain dynamically if available (AC-SCENE-09)
            Type brainType = Type.GetType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine")
                ?? Type.GetType("Cinemachine.CinemachineBrain, Cinemachine");
            if (brainType != null)
            {
                mainCamGo.AddComponent(brainType);
            }

            CameraOrbitDriver orbitDriver = mainCamGo.AddComponent<CameraOrbitDriver>();
            orbitDriver.Configure(playerRoot.transform, mainCam);

            // Re-configure PlayerRuntimeDriver to reference the active Main Camera
            PlayerRuntimeDriver playerDriver = playerRoot.GetComponent<PlayerRuntimeDriver>();
            if (playerDriver != null)
            {
                playerDriver.Configure(playerRoot.GetComponent<PlayerThirdPersonController>(), mainCam);
            }

            // 12. Create Debug Locomotion HUD (SCENE-05, AC-SCENE-16)
            GameObject hudRoot = new GameObject("--- UI_DEBUG_HUD ---");
            DebugLocomotionHUD hud = hudRoot.AddComponent<DebugLocomotionHUD>();
            hud.Configure(playerRoot.GetComponent<PlayerThirdPersonController>(), orbitDriver);

            // 13. Ensure Scenes directory exists and Save Scene
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[CorePlaygroundBuilder] Successfully built and saved CorePlayground scene at {ScenePath}!");
        }

        private const string PrefabDir = "Assets/Prefabs";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player_Capsule.prefab";
        private const string GuardPrefabPath = "Assets/Prefabs/Guard_PatrolNPC.prefab";
        private const int PlayerLayer = 6;
        private const int GuardLayer = 7;

        [MenuItem("Whisper Ward/Build Player Capsule Prefab")]
        public static GameObject BuildPlayerCapsuleMenu()
        {
            return BuildPlayerCapsule(Vector3.zero, Quaternion.identity, null);
        }

        [MenuItem("Whisper Ward/Build Guard Patrol NPC Prefab")]
        public static GameObject BuildGuardPatrolNPCMenu()
        {
            return BuildGuardPatrolNPC(Vector3.zero, Quaternion.identity, null, null);
        }

        public static GameObject BuildGuardPatrolNPC(Vector3 spawnPosition, Quaternion spawnRotation, Transform parent, Transform[] waypoints)
        {
            Directory.CreateDirectory(PrefabDir);

            GameObject guardRoot = new GameObject("Guard_PatrolNPC");
            guardRoot.transform.position = spawnPosition;
            guardRoot.transform.rotation = spawnRotation;
            if (parent != null)
            {
                guardRoot.transform.SetParent(parent);
            }

            int layer = ResolveLayer(GuardLayer, "Guard");
            guardRoot.layer = layer;
            guardRoot.tag = "Respawn";

            // Add NavMeshAgent (radius 0.40m, height 1.80m, speed 2.30m/s, stoppingDistance 0.30m)
            NavMeshAgent agent = guardRoot.AddComponent<NavMeshAgent>();
            agent.radius = 0.40f;
            agent.height = 1.80f;
            agent.speed = 2.30f;
            agent.angularSpeed = 120.0f;
            agent.acceleration = 8.0f;
            agent.stoppingDistance = 0.30f;
            agent.autoBraking = true;

            // Add Visual Capsule child with distinct Guard profile (SCENE-06, AC-SCENE-18)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual_Capsule";
            visual.transform.SetParent(guardRoot.transform);
            visual.transform.localPosition = new Vector3(0f, 0.90f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(0.8f, 0.90f, 0.8f); // 0.8m diameter, 1.8m height
            visual.layer = layer;

            Material guardMat = GetOrCreateMaterial(GuardMatPath, new Color(0.851f, 0.220f, 0.227f, 1f)); // #D9383A Hostile Red
            Renderer guardRend = visual.GetComponent<Renderer>();
            if (guardRend != null && guardMat != null)
            {
                guardRend.sharedMaterial = guardMat;
            }

            Collider visualCol = visual.GetComponent<Collider>();
            if (visualCol != null)
            {
                UnityEngine.Object.DestroyImmediate(visualCol);
            }

            // Add SimplePatrolDriver
            SimplePatrolDriver patrolDriver = guardRoot.AddComponent<SimplePatrolDriver>();
            if (waypoints != null && waypoints.Length > 0)
            {
                patrolDriver.Configure(agent, waypoints, dwellDuration: 2.0f, speed: 2.30f);
            }

            // Add VisionConeSensor (GUARD-01)
            VisionConeSensor sensor = guardRoot.AddComponent<VisionConeSensor>();
            sensor.Configure(guardRoot.transform, playerTransform: null);

            // Add SuspicionAccumulator (GUARD-02)
            SuspicionAccumulator accumulator = guardRoot.AddComponent<SuspicionAccumulator>();

            // Add VisionConeVisualizer (GUARD-03)
            VisionConeVisualizer visualizer = guardRoot.AddComponent<VisionConeVisualizer>();

            // Add GuardFSMRuntimeController (GUARD-04, GUARD-05)
            GuardFSMRuntimeController fsm = guardRoot.AddComponent<GuardFSMRuntimeController>();
            fsm.Configure(agent, sensor, accumulator, patrolDriver, playerTransform: null);

            // Add GuardHearingSensor (NOISE-01)
            GuardHearingSensor hearing = guardRoot.AddComponent<GuardHearingSensor>();
            hearing.Configure(fsm);

            // Save as Prefab Asset
            PrefabUtility.SaveAsPrefabAssetAndConnect(guardRoot, GuardPrefabPath, InteractionMode.AutomatedAction);
            Debug.Log($"[CorePlaygroundBuilder] Saved Guard Patrol NPC prefab at {GuardPrefabPath}");

            return guardRoot;
        }

        public static GameObject BuildPlayerCapsule(Vector3 spawnPosition, Quaternion spawnRotation, Transform parent)
        {
            Directory.CreateDirectory(PrefabDir);

            GameObject playerRoot = new GameObject("Player_Capsule");
            playerRoot.transform.position = spawnPosition;
            playerRoot.transform.rotation = spawnRotation;
            if (parent != null)
            {
                playerRoot.transform.SetParent(parent);
            }

            int layer = ResolveLayer(PlayerLayer, "Player");
            playerRoot.layer = layer;
            playerRoot.tag = "Player";

            // Add CharacterController (Radius 0.30m, Stand Height 1.80m, SkinWidth 0.03m, Center Y 0.90m)
            CharacterController cc = playerRoot.AddComponent<CharacterController>();
            cc.radius = 0.30f;
            cc.height = 1.80f;
            cc.center = new Vector3(0f, 0.90f, 0f);
            cc.skinWidth = 0.03f;
            cc.minMoveDistance = 0f;
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.30f;

            // Add PlayerThirdPersonController
            PlayerThirdPersonController controller = playerRoot.AddComponent<PlayerThirdPersonController>();

            // Add Visual Capsule child (SCENE-06, AC-SCENE-18)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual_Capsule";
            visual.transform.SetParent(playerRoot.transform);
            visual.transform.localPosition = new Vector3(0f, 0.90f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(0.6f, 0.90f, 0.6f); // 0.6m diameter, 1.8m height
            visual.layer = layer;

            Material playerMat = GetOrCreateMaterial(PlayerMatPath, new Color(0.165f, 0.765f, 0.635f, 1f)); // #2AC3A2 Teal
            Renderer playerRend = visual.GetComponent<Renderer>();
            if (playerRend != null && playerMat != null)
            {
                playerRend.sharedMaterial = playerMat;
            }

            // Remove primitive capsule collider so CharacterController handles collision
            Collider visualCol = visual.GetComponent<Collider>();
            if (visualCol != null)
            {
                UnityEngine.Object.DestroyImmediate(visualCol);
            }

            // Add PlayerRuntimeDriver
            PlayerRuntimeDriver driver = playerRoot.AddComponent<PlayerRuntimeDriver>();
            driver.Configure(controller, Camera.main, visual.transform);

            // Add PlayerFootstepNoiseEmitter (NOISE-01)
            PlayerFootstepNoiseEmitter noiseEmitter = playerRoot.AddComponent<PlayerFootstepNoiseEmitter>();
            noiseEmitter.Configure(controller);

            // Save as Prefab Asset
            PrefabUtility.SaveAsPrefabAssetAndConnect(playerRoot, PlayerPrefabPath, InteractionMode.AutomatedAction);
            Debug.Log($"[CorePlaygroundBuilder] Saved Player Capsule prefab at {PlayerPrefabPath}");

            return playerRoot;
        }

        private static GameObject CreateSolidCube(string name, Vector3 position, Vector3 scale, Transform parent, Material mat = null)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.transform.SetParent(parent);
            cube.isStatic = true;

            int targetLayer = ResolveLayer(WorldLayer, "World");
            cube.layer = targetLayer;

            if (mat != null)
            {
                Renderer rend = cube.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.sharedMaterial = mat;
                }
            }

            return cube;
        }

        public static Material GetOrCreateMaterial(string matPath, Color albedoColor, float smoothness = 0.2f)
        {
            Directory.CreateDirectory(MaterialsDir);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                mat.color = albedoColor;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", albedoColor);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            return mat;
        }

        public static void SetupPostProcessingVolume(Transform parent)
        {
            Directory.CreateDirectory(SettingsDir);
            GameObject volumeGo = new GameObject("Global_PostProcess_Volume");
            volumeGo.transform.SetParent(parent);

            Type volumeType = Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime")
                ?? Type.GetType("UnityEngine.Rendering.Volume, UnityEngine.CoreModule");

            if (volumeType != null)
            {
                Component volComp = volumeGo.AddComponent(volumeType);
                var isGlobalProp = volumeType.GetProperty("isGlobal");
                if (isGlobalProp != null) isGlobalProp.SetValue(volComp, true);

                Type profileType = Type.GetType("UnityEngine.Rendering.VolumeProfile, Unity.RenderPipelines.Core.Runtime")
                    ?? Type.GetType("UnityEngine.Rendering.VolumeProfile, UnityEngine.CoreModule");

                if (profileType != null)
                {
                    ScriptableObject profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(VolumeProfilePath);
                    if (profile == null)
                    {
                        profile = ScriptableObject.CreateInstance(profileType);

                        Type tonemappingType = Type.GetType("UnityEngine.Rendering.Universal.Tonemapping, Unity.RenderPipelines.Universal.Runtime");
                        if (tonemappingType != null)
                        {
                            var addMethod = profileType.GetMethod("Add", new Type[] { typeof(bool) })?.MakeGenericMethod(tonemappingType);
                            addMethod?.Invoke(profile, new object[] { true });
                        }

                        Type vignetteType = Type.GetType("UnityEngine.Rendering.Universal.Vignette, Unity.RenderPipelines.Universal.Runtime");
                        if (vignetteType != null)
                        {
                            var addMethod = profileType.GetMethod("Add", new Type[] { typeof(bool) })?.MakeGenericMethod(vignetteType);
                            addMethod?.Invoke(profile, new object[] { true });
                        }

                        AssetDatabase.CreateAsset(profile, VolumeProfilePath);
                    }

                    var profileProp = volumeType.GetProperty("sharedProfile");
                    if (profileProp != null) profileProp.SetValue(volComp, profile);
                }
            }
        }

        private static int ResolveLayer(int preferredIndex, string layerName)
        {
            int layerIndex = LayerMask.NameToLayer(layerName);
            if (layerIndex >= 0 && layerIndex <= 31)
            {
                return layerIndex;
            }
            return preferredIndex;
        }
    }
}
