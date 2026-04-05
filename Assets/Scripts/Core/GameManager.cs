using DontLookGame.Gameplay;
using DontLookGame.Player;
using UnityEngine;
using UnityEngine.UI;

namespace DontLookGame.Core
{
    [DefaultExecutionOrder(-1000)]
    public class GameManager : MonoBehaviour
    {
        private const string LevelRootName = "DontLookLevelRoot";
        private const string BestTimeKey = "DontLookGame_BestTimeSec";

        [Header("Level Placement")]
        [SerializeField] private Vector3 levelOrigin = new Vector3(0f, 0f, 120f);

        private Transform _levelRoot;
        private Transform _level1Root;
        private Transform _level2Root;

        private PlayerController _playerController;

        private Text _instructionText;
        private Text _levelText;
        private Text _timerText;
        private Text _bestText;
        private Text _winText;

        private bool _won;
        private int _currentLevelIndex;

        private bool _timerRunning;
        private float _runStartTime;
        private float _bestTimeSeconds = -1f;

        public static GameManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGameManagerExists()
        {
            if (FindFirstObjectByType<GameManager>() != null)
            {
                return;
            }

            GameObject gameManager = new GameObject("GameManager");
            gameManager.AddComponent<GameManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            BuildGameIfNeeded();
            LoadBestTime();
            StartRun();
        }

        private void Update()
        {
            if (_timerRunning)
            {
                UpdateTimerUi(Time.time - _runStartTime);
            }
        }

        public void HandleFinishReached(int levelIndex)
        {
            if (_won || levelIndex != _currentLevelIndex)
            {
                return;
            }

            if (levelIndex == 0)
            {
                AdvanceToLevelTwo();
                return;
            }

            CompleteRun();
        }

        private void BuildGameIfNeeded()
        {
            GameObject existingRoot = GameObject.Find(LevelRootName);
            if (existingRoot != null)
            {
                _levelRoot = existingRoot.transform;
                _level1Root = _levelRoot.Find("Level_1");
                _level2Root = _levelRoot.Find("Level_2");
                return;
            }

            _levelRoot = new GameObject(LevelRootName).transform;
            _levelRoot.position = levelOrigin;

            SetupCameraAndLightingDefaults();
            EnsureVisibilitySystem();

            Material groundMaterial = CreateRuntimeMaterial("Ground_Mat", new Color(0.14f, 0.18f, 0.24f));
            Material platformMaterial = CreateRuntimeMaterial("GhostPlatform_Mat", new Color(0.89f, 0.75f, 0.29f));
            Material movingPlatformMaterial = CreateRuntimeMaterial("MovingGhostPlatform_Mat", new Color(0.90f, 0.49f, 0.26f));
            Material routeMaterial = CreateRuntimeMaterial("RouteBeacon_Mat", new Color(0.25f, 0.90f, 1f));
            Material finishMaterial = CreateRuntimeMaterial("Finish_Mat", new Color(0.28f, 0.78f, 0.41f));

            _level1Root = new GameObject("Level_1").transform;
            _level1Root.SetParent(_levelRoot);
            _level1Root.localPosition = Vector3.zero;

            _level2Root = new GameObject("Level_2").transform;
            _level2Root.SetParent(_levelRoot);
            _level2Root.localPosition = new Vector3(0f, 0f, 88f);

            BuildLevelOne(_level1Root, groundMaterial, platformMaterial, routeMaterial, finishMaterial);
            BuildLevelTwo(_level2Root, groundMaterial, movingPlatformMaterial, routeMaterial, finishMaterial);
            _level2Root.gameObject.SetActive(false);

            Vector3 level1Spawn = _level1Root.TransformPoint(new Vector3(0f, 1.1f, -3f));
            BuildPlayerAndCamera(level1Spawn);
            BuildUI();
        }

        private void SetupCameraAndLightingDefaults()
        {
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 90f;

            Light sceneLight = FindFirstObjectByType<Light>();
            if (sceneLight == null)
            {
                GameObject lightObject = new GameObject("DontLookDirectionalLight");
                lightObject.transform.SetParent(_levelRoot);
                lightObject.transform.localRotation = Quaternion.Euler(44f, -34f, 0f);

                sceneLight = lightObject.AddComponent<Light>();
                sceneLight.type = LightType.Directional;
            }

            sceneLight.intensity = 1.25f;
            sceneLight.shadows = LightShadows.Soft;
        }

        private void EnsureVisibilitySystem()
        {
            if (FindFirstObjectByType<VisibilitySystem>() != null)
            {
                return;
            }

            GameObject visibilitySystemObject = new GameObject("VisibilitySystem");
            visibilitySystemObject.transform.SetParent(_levelRoot);
            visibilitySystemObject.AddComponent<VisibilitySystem>();
        }

        private void BuildLevelOne(Transform parent, Material groundMaterial, Material platformMaterial, Material routeMaterial, Material finishMaterial)
        {
            CreateCube(parent, "StartGround", new Vector3(0f, -0.5f, 0f), new Vector3(14f, 1f, 11f), groundMaterial, true);
            CreateCube(parent, "EndGround", new Vector3(0f, -0.5f, 34f), new Vector3(16f, 1f, 12f), groundMaterial, true);
            CreateCube(parent, "PitVisual", new Vector3(0f, -3.2f, 17f), new Vector3(20f, 1f, 32f), CreateRuntimeMaterial("Pit_Mat", new Color(0.05f, 0.05f, 0.07f)), false);

            CreateCylinder(parent, "StartMarker", new Vector3(0f, 0.15f, -3f), new Vector3(0.65f, 0.15f, 0.65f), CreateRuntimeMaterial("Start_Mat", new Color(0.23f, 0.66f, 0.95f)), false);

            Vector3[] localPlatformPositions =
            {
                new Vector3(-1.2f, 1.0f, 7.4f),
                new Vector3(0.9f, 1.0f, 10.1f),
                new Vector3(-0.8f, 1.0f, 12.8f),
                new Vector3(1.1f, 1.0f, 15.5f),
                new Vector3(-0.8f, 1.0f, 18.1f),
                new Vector3(0.8f, 1.0f, 20.8f),
                new Vector3(0.0f, 1.0f, 23.5f)
            };

            for (int i = 0; i < localPlatformPositions.Length; i++)
            {
                GameObject platform = CreateCube(
                    parent,
                    $"GhostPlatform_{i + 1}",
                    localPlatformPositions[i],
                    new Vector3(2.9f, 0.45f, 2.9f),
                    platformMaterial,
                    true);

                platform.AddComponent<VisibilityDependentObject>();
                CreateGuideBeacon(parent, localPlatformPositions[i], routeMaterial);
            }

            BuildLevelOneDecorations(parent);
            BuildFinishZone(parent, finishMaterial, 0, new Vector3(0f, 0.2f, 37f), new Vector3(0f, 1.3f, 37f));
        }

        private void BuildLevelTwo(Transform parent, Material groundMaterial, Material movingPlatformMaterial, Material routeMaterial, Material finishMaterial)
        {
            CreateCube(parent, "StartGround_L2", new Vector3(0f, -0.5f, 0f), new Vector3(14f, 1f, 11f), groundMaterial, true);
            CreateCube(parent, "EndGround_L2", new Vector3(0f, -0.5f, 33f), new Vector3(15f, 1f, 12f), groundMaterial, true);
            CreateCube(parent, "PitVisual_L2", new Vector3(0f, -3.4f, 16.5f), new Vector3(22f, 1f, 31f), CreateRuntimeMaterial("Pit2_Mat", new Color(0.08f, 0.06f, 0.09f)), false);

            CreateCylinder(parent, "StartMarker_L2", new Vector3(0f, 0.15f, -3f), new Vector3(0.65f, 0.15f, 0.65f), CreateRuntimeMaterial("Start2_Mat", new Color(0.90f, 0.58f, 0.23f)), false);

            Vector3[] localPlatformPositions =
            {
                new Vector3(-1.1f, 1.0f, 7.8f),
                new Vector3(1.2f, 1.0f, 10.8f),
                new Vector3(-0.6f, 1.0f, 14.0f),
                new Vector3(1.1f, 1.0f, 17.1f),
                new Vector3(-0.6f, 1.0f, 20.3f),
                new Vector3(0.8f, 1.0f, 23.4f)
            };

            Vector3[] moveOffsets =
            {
                new Vector3(1.2f, 0f, 0f),
                new Vector3(-1.1f, 0f, 0f),
                new Vector3(0f, 0f, 1.0f),
                new Vector3(0f, 0f, -1.1f),
                new Vector3(1.0f, 0f, 0f),
                new Vector3(-0.9f, 0f, 0f)
            };

            for (int i = 0; i < localPlatformPositions.Length; i++)
            {
                GameObject platform = CreateCube(
                    parent,
                    $"MovingGhostPlatform_{i + 1}",
                    localPlatformPositions[i],
                    new Vector3(2.8f, 0.45f, 2.8f),
                    movingPlatformMaterial,
                    true);

                platform.AddComponent<VisibilityDependentObject>();

                MovingGhostPlatform movingPlatform = platform.AddComponent<MovingGhostPlatform>();
                movingPlatform.Configure(moveOffsets[i], 0.95f + i * 0.08f, i * 0.55f);

                CreateGuideBeacon(parent, localPlatformPositions[i], routeMaterial);
            }

            BuildLevelTwoDecorations(parent);
            BuildFinishZone(parent, finishMaterial, 1, new Vector3(0f, 0.2f, 36f), new Vector3(0f, 1.3f, 36f));
        }

        private void BuildLevelOneDecorations(Transform parent)
        {
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/tree_pineTallA", new Vector3(-7f, 0f, -5f), 1.1f, 12f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/tree_small", new Vector3(6.5f, 0f, -4.5f), 1.0f, -15f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/rock_largeA", new Vector3(-5f, 0f, 3f), 1.1f, 45f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/stone_smallA", new Vector3(5.2f, 0f, 2.7f), 1.0f, 15f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/rock_largeC", new Vector3(-8.8f, 0f, 14f), 1.2f, 80f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/rock_smallFlatA", new Vector3(8.8f, 0f, 18f), 1.3f, -40f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/grass_large", new Vector3(-6.7f, 0f, 11.5f), 1.25f, 0f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/plant_bush", new Vector3(6.9f, 0f, 19f), 1.1f, 0f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/tree_pineTallC", new Vector3(-7.3f, 0f, 35f), 1.15f, 24f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/tree_small", new Vector3(7.1f, 0f, 34.3f), 1.0f, -20f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/stone_largeA", new Vector3(-4.6f, 0f, 40f), 1.05f, -12f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/rock_smallA", new Vector3(4.8f, 0f, 40f), 1.1f, 35f);
        }

        private void BuildLevelTwoDecorations(Transform parent)
        {
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/tree_pineTallC", new Vector3(-7.2f, 0f, -4.6f), 1.08f, 10f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/tree_pineTallA", new Vector3(7.1f, 0f, -4.4f), 1.0f, -14f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/stone_largeA", new Vector3(-8.4f, 0f, 16f), 1.2f, 35f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/rock_largeC", new Vector3(8.6f, 0f, 15.8f), 1.2f, -20f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/plant_bush", new Vector3(-6f, 0f, 26f), 1.2f, 0f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/grass_large", new Vector3(6.2f, 0f, 27f), 1.2f, 0f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/rock_smallFlatA", new Vector3(-5.3f, 0f, 35.9f), 1.2f, -10f);
            CreateDecorativeModel(parent, "ThirdParty/KenneyNature/Models/stone_smallA", new Vector3(4.9f, 0f, 36.1f), 1.1f, 18f);
        }

        private void BuildFinishZone(Transform parent, Material finishMaterial, int levelIndex, Vector3 padPosition, Vector3 triggerPosition)
        {
            GameObject finishPad = CreateCylinder(parent, $"FinishPad_L{levelIndex + 1}", padPosition, new Vector3(1.1f, 0.2f, 1.1f), finishMaterial, true);
            finishPad.GetComponent<Collider>().enabled = false;

            GameObject finishTriggerObject = new GameObject($"FinishTrigger_L{levelIndex + 1}");
            finishTriggerObject.transform.SetParent(parent);
            finishTriggerObject.transform.localPosition = triggerPosition;

            BoxCollider triggerCollider = finishTriggerObject.AddComponent<BoxCollider>();
            triggerCollider.size = new Vector3(3.5f, 2.6f, 3.5f);
            triggerCollider.isTrigger = true;

            FinishTrigger finishTrigger = finishTriggerObject.AddComponent<FinishTrigger>();
            finishTrigger.Initialize(this, levelIndex);
        }

        private void BuildPlayerAndCamera(Vector3 spawnWorldPosition)
        {
            Camera camera = EnsureMainGameplayCamera();

            GameObject playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerObject.name = "Player";
            playerObject.transform.SetParent(_levelRoot);
            playerObject.transform.position = spawnWorldPosition;
            playerObject.transform.rotation = Quaternion.identity;
            playerObject.tag = "Player";

            Destroy(playerObject.GetComponent<CapsuleCollider>());
            playerObject.GetComponent<Renderer>().sharedMaterial = CreateRuntimeMaterial("Player_Mat", new Color(0.88f, 0.92f, 0.97f));

            playerObject.AddComponent<CharacterController>();
            _playerController = playerObject.AddComponent<PlayerController>();
            _playerController.SetSpawnPoint(spawnWorldPosition);

            CameraController cameraController = camera.GetComponent<CameraController>();
            if (cameraController == null)
            {
                cameraController = camera.gameObject.AddComponent<CameraController>();
            }
            cameraController.SetFollowTarget(playerObject.transform);
        }

        private Camera EnsureMainGameplayCamera()
        {
            Camera gameplayCamera = null;
            GameObject existing = GameObject.Find("DontLookMainCamera");
            if (existing != null)
            {
                gameplayCamera = existing.GetComponent<Camera>();
            }

            if (gameplayCamera == null)
            {
                GameObject cameraObject = new GameObject("DontLookMainCamera");
                gameplayCamera = cameraObject.AddComponent<Camera>();
                gameplayCamera.transform.position = _levelRoot.TransformPoint(new Vector3(0f, 2.2f, -8f));
            }

            gameplayCamera.tag = "MainCamera";
            gameplayCamera.clearFlags = CameraClearFlags.Skybox;
            gameplayCamera.enabled = true;

            if (gameplayCamera.GetComponent<AudioListener>() == null)
            {
                gameplayCamera.gameObject.AddComponent<AudioListener>();
            }

            Camera[] allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < allCameras.Length; i++)
            {
                if (allCameras[i] == gameplayCamera)
                {
                    continue;
                }

                if (allCameras[i].CompareTag("MainCamera"))
                {
                    allCameras[i].tag = "Untagged";
                }

                allCameras[i].enabled = false;
                AudioListener listener = allCameras[i].GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = false;
                }
            }

            return gameplayCamera;
        }

        private void BuildUI()
        {
            GameObject canvasObject = new GameObject("GameUI");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiFont == null)
            {
                uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            _instructionText = CreateUIText(
                canvas.transform,
                uiFont,
                "L1: Не смотри на платформы — они исчезают. Иди по маякам.",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -36f),
                28,
                TextAnchor.MiddleCenter);

            _levelText = CreateUIText(
                canvas.transform,
                uiFont,
                "Уровень 1 / 2",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -76f),
                24,
                TextAnchor.MiddleCenter);

            _timerText = CreateUIText(
                canvas.transform,
                uiFont,
                "Время: 00:00.00",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(190f, -32f),
                24,
                TextAnchor.MiddleLeft);

            _bestText = CreateUIText(
                canvas.transform,
                uiFont,
                "Рекорд: --:--.--",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(190f, -66f),
                24,
                TextAnchor.MiddleLeft);

            _winText = CreateUIText(
                canvas.transform,
                uiFont,
                "ПОБЕДА",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                52,
                TextAnchor.MiddleCenter);
            _winText.gameObject.SetActive(false);
        }

        private static Text CreateUIText(
            Transform parent,
            Font font,
            string text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            int fontSize,
            TextAnchor alignment)
        {
            GameObject textObject = new GameObject($"UI_{text}");
            textObject.transform.SetParent(parent);

            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1200f, 120f);
            rect.anchoredPosition = anchoredPosition;

            Text uiText = textObject.AddComponent<Text>();
            uiText.font = font;
            uiText.text = text;
            uiText.fontSize = fontSize;
            uiText.alignment = alignment;
            uiText.color = Color.white;

            return uiText;
        }

        private void StartRun()
        {
            _won = false;
            _currentLevelIndex = 0;
            _timerRunning = true;
            _runStartTime = Time.time;

            if (_level1Root != null)
            {
                _level1Root.gameObject.SetActive(true);
            }

            if (_level2Root != null)
            {
                _level2Root.gameObject.SetActive(false);
            }

            if (_winText != null)
            {
                _winText.gameObject.SetActive(false);
            }

            if (_instructionText != null)
            {
                _instructionText.text = "L1: Не смотри на платформы — они исчезают. Иди по маякам.";
            }

            UpdateLevelLabel();
            UpdateBestUi();
            UpdateTimerUi(0f);
        }

        private void AdvanceToLevelTwo()
        {
            _currentLevelIndex = 1;

            if (_level1Root != null)
            {
                _level1Root.gameObject.SetActive(false);
            }

            if (_level2Root != null)
            {
                _level2Root.gameObject.SetActive(true);
            }

            Vector3 level2Spawn = _level2Root.TransformPoint(new Vector3(0f, 1.1f, -3f));
            _playerController?.TeleportTo(level2Spawn, Quaternion.identity);
            _playerController?.SetSpawnPoint(level2Spawn);

            if (_instructionText != null)
            {
                _instructionText.text = "L2: Платформы двигаются и исчезают при взгляде. Сохраняй ритм.";
            }

            UpdateLevelLabel();
        }

        private void CompleteRun()
        {
            _won = true;
            _timerRunning = false;
            _playerController?.SetInputEnabled(false);

            float finalTime = Time.time - _runStartTime;
            bool isRecord = _bestTimeSeconds < 0f || finalTime < _bestTimeSeconds;

            if (isRecord)
            {
                _bestTimeSeconds = finalTime;
                PlayerPrefs.SetFloat(BestTimeKey, _bestTimeSeconds);
                PlayerPrefs.Save();
            }

            if (_winText != null)
            {
                string recordSuffix = isRecord ? "\nНовый рекорд!" : string.Empty;
                _winText.text = $"ПОБЕДА\nВремя: {FormatTime(finalTime)}\nРекорд: {FormatTime(_bestTimeSeconds)}{recordSuffix}";
                _winText.gameObject.SetActive(true);
            }

            if (_instructionText != null)
            {
                _instructionText.text = "Забег завершён. Нажми Stop/Play для нового старта.";
            }

            UpdateBestUi();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void LoadBestTime()
        {
            _bestTimeSeconds = PlayerPrefs.GetFloat(BestTimeKey, -1f);
            if (_bestTimeSeconds <= 0f)
            {
                _bestTimeSeconds = -1f;
            }
        }

        private void UpdateTimerUi(float elapsedSeconds)
        {
            if (_timerText != null)
            {
                _timerText.text = $"Время: {FormatTime(elapsedSeconds)}";
            }
        }

        private void UpdateBestUi()
        {
            if (_bestText != null)
            {
                _bestText.text = _bestTimeSeconds > 0f
                    ? $"Рекорд: {FormatTime(_bestTimeSeconds)}"
                    : "Рекорд: --:--.--";
            }
        }

        private void UpdateLevelLabel()
        {
            if (_levelText != null)
            {
                _levelText.text = _currentLevelIndex == 0 ? "Уровень 1 / 2" : "Уровень 2 / 2";
            }
        }

        private static string FormatTime(float seconds)
        {
            if (seconds < 0f)
            {
                return "--:--.--";
            }

            int minutes = Mathf.FloorToInt(seconds / 60f);
            float sec = seconds - minutes * 60f;
            return $"{minutes:00}:{sec:00.00}";
        }

        private GameObject CreateCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, bool withCollider)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = localScale;

            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.sharedMaterial = material;
            }

            if (!withCollider)
            {
                Collider collider = cube.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }
            }

            return cube;
        }

        private GameObject CreateCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, bool withCollider)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent);
            cylinder.transform.localPosition = localPosition;
            cylinder.transform.localScale = localScale;

            Renderer renderer = cylinder.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.sharedMaterial = material;
            }

            if (!withCollider)
            {
                Collider collider = cylinder.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }
            }

            return cylinder;
        }

        private void CreateGuideBeacon(Transform parent, Vector3 localPlatformPosition, Material routeMaterial)
        {
            Vector3 polePosition = localPlatformPosition + new Vector3(0f, 1.35f, 0f);
            GameObject pole = CreateCylinder(parent, "RouteBeaconPole", polePosition, new Vector3(0.1f, 1.1f, 0.1f), routeMaterial, false);
            pole.name = $"RouteBeaconPole_{Mathf.RoundToInt(localPlatformPosition.z * 10f)}";

            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = $"RouteBeaconOrb_{Mathf.RoundToInt(localPlatformPosition.z * 10f)}";
            orb.transform.SetParent(parent);
            orb.transform.localPosition = localPlatformPosition + new Vector3(0f, 2.55f, 0f);
            orb.transform.localScale = Vector3.one * 0.38f;
            orb.GetComponent<Renderer>().sharedMaterial = routeMaterial;
            Destroy(orb.GetComponent<Collider>());
        }

        private void CreateDecorativeModel(Transform parent, string resourcesPath, Vector3 localPosition, float uniformScale, float yRotation)
        {
            GameObject modelPrefab = Resources.Load<GameObject>(resourcesPath);
            if (modelPrefab == null)
            {
                return;
            }

            GameObject instance = Instantiate(modelPrefab, parent);
            instance.name = $"Deco_{modelPrefab.name}";
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
            instance.transform.localScale = Vector3.one * uniformScale;

            // Decorative objects should not block the traversal path.
            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Destroy(colliders[i]);
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderers[i].receiveShadows = true;
            }
        }

        private static Material CreateRuntimeMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                name = materialName
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private sealed class FinishTrigger : MonoBehaviour
        {
            private GameManager _gameManager;
            private int _levelIndex;

            public void Initialize(GameManager gameManager, int levelIndex)
            {
                _gameManager = gameManager;
                _levelIndex = levelIndex;
            }

            private void OnTriggerEnter(Collider other)
            {
                if (other.GetComponent<PlayerController>() == null)
                {
                    return;
                }

                _gameManager?.HandleFinishReached(_levelIndex);
            }
        }
    }
}
