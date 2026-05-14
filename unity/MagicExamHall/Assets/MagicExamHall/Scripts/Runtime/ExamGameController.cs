using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MagicExamHall
{
    public sealed class ExamGameController : MonoBehaviour
    {
        [Header("Scene References")]
        public Camera mainCamera = null!;
        public Transform player = null!;
        public Canvas canvas = null!;
        public ExamStationView[] stationViews = Array.Empty<ExamStationView>();

        private readonly List<ParticlePulse> pulses = new();
        private readonly List<CompletedTrial> completedTrials = new();
        private readonly float[] surveyScores = { 4, 4, 4, 4, 4 };

        private ExamLogger logger = null!;
        private SpellDrawingCanvas drawingCanvas = null!;
        private RectTransform hudPanel = null!;
        private RectTransform promptPanel = null!;
        private RectTransform drawPanel = null!;
        private RectTransform resultPanel = null!;
        private RectTransform surveyPanel = null!;
        private Image progressFill = null!;
        private Text hudTitle = null!;
        private Text hudCopy = null!;
        private Text promptTitle = null!;
        private Text promptCopy = null!;
        private Text resultTitle = null!;
        private Text resultCopy = null!;
        private Text resultHint = null!;
        private Text resultMetrics = null!;
        private Text surveyMeta = null!;
        private InputField surveyComment = null!;
        private Slider[] surveySliders = Array.Empty<Slider>();
        private Text[] surveyValueLabels = Array.Empty<Text>();
        private Font uiFont = null!;
        private string sessionId = "";
        private string lastBanner = "";
        private int activeStationIndex;
        private int currentAttempt = 1;
        private float stationStartTime;
        private float bannerShownAt;
        private Vector2 velocity;
        private SpellResult lastResult;
        private HintState currentHint = new();

        public bool IsDrawingPanelVisible => drawPanel != null && drawPanel.gameObject.activeSelf;
        public bool IsResultPanelVisible => resultPanel != null && resultPanel.gameObject.activeSelf;
        public int CurrentAssistLevel => currentHint.AssistLevelNumber;
        public int StationCount => stationViews.Length;
        public string LastHintText => currentHint.body;
        public string OutputDirectory => logger?.OutputDirectory ?? "";

        private void Awake()
        {
            sessionId = $"unity-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
            logger = new ExamLogger(sessionId);
            uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 18);
            ResolveSceneReferences();
            BuildUi();
            stationStartTime = Time.time;
            OpenPromptOnly();
        }

        private void Update()
        {
            TickPlayer();
            TickStations();
            TickPulses();
            UpdateHud();

            if (!IsDrawingPanelVisible && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space)))
            {
                TryOpenActiveStation();
            }
        }

        public void OpenCurrentStationForTests()
        {
            if (activeStationIndex < stationViews.Length && player != null)
            {
                player.position = stationViews[activeStationIndex].transform.position;
            }
            OpenDrawingPanel();
        }

        public SpellResult CastSyntheticForTests(List<List<StrokeSample>> strokes)
        {
            OpenDrawingPanel();
            drawingCanvas.SetTestStrokes(strokes);
            return CastCurrentGesture();
        }

        private void ResolveSceneReferences()
        {
            mainCamera ??= Camera.main;
            if (mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                mainCamera = cameraObject.AddComponent<Camera>();
                mainCamera.orthographic = true;
                mainCamera.orthographicSize = 6.2f;
                mainCamera.backgroundColor = new Color(0.06f, 0.08f, 0.11f);
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            }

            stationViews = stationViews == null || stationViews.Length == 0
                ? FindObjectsByType<ExamStationView>(FindObjectsSortMode.None).OrderBy(station => station.transform.position.x).ToArray()
                : stationViews;

            if (stationViews.Length == 0)
            {
                BuildFallbackWorld();
            }

            if (player == null)
            {
                var playerObject = new GameObject("Apprentice");
                playerObject.transform.position = new Vector3(0f, -4.1f, 0f);
                playerObject.AddComponent<SpriteRenderer>();
                var pixelSprite = playerObject.AddComponent<PixelSpriteView>();
                pixelSprite.kind = PixelSpriteKind.Player;
                pixelSprite.primary = new Color(0.95f, 0.92f, 0.78f);
                pixelSprite.secondary = new Color(0.28f, 0.62f, 0.96f);
                pixelSprite.sortingOrder = 4;
                playerObject.transform.localScale = Vector3.one * 0.78f;
                player = playerObject.transform;
            }

            foreach (var station in stationViews)
            {
                station.InitializeSprites();
            }

            if (canvas == null)
            {
                var canvasObject = new GameObject("Exam Canvas");
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280, 720);
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }
        }

        private void BuildFallbackWorld()
        {
            var floor = CreateWorldSprite("Stone Tile Floor", Vector2.zero, Vector3.one, new Color(0.16f, 0.18f, 0.23f), new Color(0.10f, 0.12f, 0.16f), PixelSpriteKind.FloorTile, -7, true, new Vector2(16.4f, 10f));
            floor.transform.SetParent(transform, true);
            CreateWorldSprite("North Carved Wall", new Vector2(0f, 4.95f), Vector3.one, new Color(0.22f, 0.20f, 0.27f), new Color(0.63f, 0.50f, 0.23f), PixelSpriteKind.WallTrim, -4, true, new Vector2(16.4f, 1.15f)).transform.SetParent(transform, true);
            CreateWorldSprite("Center Runner", new Vector2(0f, 0.15f), Vector3.one, new Color(0.55f, 0.10f, 0.17f), new Color(0.95f, 0.69f, 0.26f), PixelSpriteKind.Rug, -5, true, new Vector2(2.2f, 7.6f)).transform.SetParent(transform, true);
            CreateWorldSprite("West Bookcase", new Vector2(-7.25f, 1.25f), Vector3.one * 1.25f, new Color(0.42f, 0.23f, 0.12f), new Color(0.42f, 0.80f, 0.88f), PixelSpriteKind.Bookshelf, -1).transform.SetParent(transform, true);
            CreateWorldSprite("East Bookcase", new Vector2(7.25f, 1.25f), Vector3.one * 1.25f, new Color(0.42f, 0.23f, 0.12f), new Color(0.68f, 0.36f, 0.86f), PixelSpriteKind.Bookshelf, -1).transform.SetParent(transform, true);
            var specs = StationSpecs();
            var built = new List<ExamStationView>();
            foreach (var spec in specs)
            {
                built.Add(CreateStation(spec));
            }
            stationViews = built.ToArray();
        }

        private void BuildUi()
        {
            ClearChildren(canvas.transform);
            hudPanel = CreatePanel("HUD", canvas.transform, new Vector2(20, -20), new Vector2(540, 120), Anchor.TopLeft);
            hudTitle = CreateText("Title", hudPanel, "마법 시험장", 24, FontStyle.Bold, new Vector2(16, -12), new Vector2(500, 30), Anchor.TopLeft);
            hudCopy = CreateText("Copy", hudPanel, "", 16, FontStyle.Normal, new Vector2(16, -48), new Vector2(500, 48), Anchor.TopLeft);
            progressFill = CreateProgressBar(hudPanel);

            promptPanel = CreatePanel("Station Prompt", canvas.transform, new Vector2(-24, -24), new Vector2(400, 142), Anchor.TopRight);
            promptTitle = CreateText("Prompt Title", promptPanel, "", 20, FontStyle.Bold, new Vector2(18, -14), new Vector2(360, 28), Anchor.TopLeft);
            promptCopy = CreateText("Prompt Copy", promptPanel, "", 15, FontStyle.Normal, new Vector2(18, -50), new Vector2(360, 54), Anchor.TopLeft);
            CreateButton("Open Button", promptPanel, "입력 열기", new Vector2(18, 16), new Vector2(130, 34), Anchor.BottomLeft, TryOpenActiveStation);

            drawPanel = CreatePanel("Drawing Panel", canvas.transform, Vector2.zero, new Vector2(920, 560), Anchor.Center);
            CreateText("Drawing Title", drawPanel, "문양 입력", 22, FontStyle.Bold, new Vector2(22, -18), new Vector2(420, 30), Anchor.TopLeft);
            var drawingBackground = CreateImage("Drawing Surface Background", drawPanel, new Vector2(24, -74), new Vector2(520, 380), Anchor.TopLeft, new Color(0.93f, 0.97f, 1f, 1f));
            drawingCanvas = CreateDrawingSurface(drawingBackground.rectTransform);
            drawingCanvas.raycastTarget = true;
            resultPanel = CreatePanel("Result Panel", drawPanel, new Vector2(-24, -74), new Vector2(320, 380), Anchor.TopRight);
            resultTitle = CreateText("Result Title", resultPanel, "인식 피드백", 19, FontStyle.Bold, new Vector2(16, -14), new Vector2(280, 28), Anchor.TopLeft);
            resultCopy = CreateText("Result Copy", resultPanel, "", 15, FontStyle.Normal, new Vector2(16, -54), new Vector2(280, 96), Anchor.TopLeft);
            resultHint = CreateText("Result Hint", resultPanel, "", 15, FontStyle.Bold, new Vector2(16, -166), new Vector2(280, 92), Anchor.TopLeft);
            resultMetrics = CreateText("Result Metrics", resultPanel, "", 14, FontStyle.Normal, new Vector2(16, -276), new Vector2(280, 84), Anchor.TopLeft);
            CreateButton("Clear Button", drawPanel, "다시 그리기", new Vector2(24, 24), new Vector2(136, 42), Anchor.BottomLeft, ClearAttempt);
            CreateButton("Cast Button", drawPanel, "마법 시전", new Vector2(176, 24), new Vector2(136, 42), Anchor.BottomLeft, () => CastCurrentGesture());
            CreateButton("Next Button", drawPanel, "다음", new Vector2(328, 24), new Vector2(136, 42), Anchor.BottomLeft, CompleteCurrentStation);
            CreateButton("Close Button", drawPanel, "닫기", new Vector2(-24, 24), new Vector2(116, 42), Anchor.BottomRight, OpenPromptOnly);

            surveyPanel = CreatePanel("Survey Panel", canvas.transform, Vector2.zero, new Vector2(760, 520), Anchor.Center);
            CreateSurveyUi();
            drawPanel.gameObject.SetActive(false);
            surveyPanel.gameObject.SetActive(false);
        }

        private void CreateSurveyUi()
        {
            CreateText("Survey Title", surveyPanel, "사후 설문", 23, FontStyle.Bold, new Vector2(24, -18), new Vector2(360, 32), Anchor.TopLeft);
            surveyMeta = CreateText("Survey Meta", surveyPanel, "", 15, FontStyle.Normal, new Vector2(24, -56), new Vector2(620, 26), Anchor.TopLeft);
            var labels = new[]
            {
                "문양 안내가 명확했다",
                "실패 이유가 납득 가능했다",
                "피드백이 다음 시도에 도움이 됐다",
                "마우스로 조작하기 편했다",
                "마법을 직접 시전하는 느낌이 있었다"
            };

            surveySliders = new Slider[labels.Length];
            surveyValueLabels = new Text[labels.Length];
            for (var index = 0; index < labels.Length; index++)
            {
                var y = -104 - index * 52;
                CreateText($"Survey Label {index}", surveyPanel, labels[index], 15, FontStyle.Normal, new Vector2(24, y), new Vector2(290, 24), Anchor.TopLeft);
                surveySliders[index] = CreateSlider($"Survey Slider {index}", surveyPanel, new Vector2(326, y - 2), new Vector2(250, 22), Anchor.TopLeft, surveyScores[index]);
                var labelIndex = index;
                surveyValueLabels[index] = CreateText($"Survey Value {index}", surveyPanel, Mathf.RoundToInt(surveyScores[index]).ToString(CultureInfo.InvariantCulture), 15, FontStyle.Bold, new Vector2(596, y), new Vector2(34, 24), Anchor.TopLeft);
                surveySliders[index].onValueChanged.AddListener(value =>
                {
                    surveyScores[labelIndex] = value;
                    surveyValueLabels[labelIndex].text = Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
                });
            }

            CreateText("Comment Label", surveyPanel, "자유 의견", 15, FontStyle.Normal, new Vector2(24, -378), new Vector2(110, 24), Anchor.TopLeft);
            surveyComment = CreateInputField("Survey Comment", surveyPanel, new Vector2(140, -358), new Vector2(570, 76), Anchor.TopLeft);
            CreateButton("Save Survey", surveyPanel, "설문 저장", new Vector2(24, 24), new Vector2(140, 42), Anchor.BottomLeft, SaveSurvey);
        }

        private void TickPlayer()
        {
            if (IsDrawingPanelVisible || surveyPanel.gameObject.activeSelf)
            {
                return;
            }

            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }
            velocity = Vector2.Lerp(velocity, input * 4.2f, Time.deltaTime * 12f);
            player.position += (Vector3)(velocity * Time.deltaTime);
            player.position = new Vector3(Mathf.Clamp(player.position.x, -7.2f, 7.2f), Mathf.Clamp(player.position.y, -4.3f, 4.3f), 0f);
        }

        private void TickStations()
        {
            foreach (var station in stationViews)
            {
                station.TickIdle(Time.time);
            }
        }

        private void TickPulses()
        {
            for (var index = pulses.Count - 1; index >= 0; index--)
            {
                var pulse = pulses[index];
                pulse.age += Time.deltaTime;
                if (pulse.body == null)
                {
                    pulse.body = CreateWorldSprite("Spell Pulse", pulse.position, Vector3.one * 0.35f, pulse.color, Color.white, PixelSpriteKind.Pulse, 5);
                }
                var t = pulse.age / 0.85f;
                pulse.body.transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 2.6f, t);
                var pulseRenderer = pulse.body.GetComponent<SpriteRenderer>();
                pulseRenderer.sharedMaterial = PixelMaterialProvider.SpriteMaterial;
                pulseRenderer.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.85f, 0f, t));
                if (t >= 1f)
                {
                    Destroy(pulse.body);
                    pulses.RemoveAt(index);
                }
            }
        }

        private void UpdateHud()
        {
            if (activeStationIndex < stationViews.Length)
            {
                var station = stationViews[activeStationIndex];
                hudTitle.text = "마법 시험장";
                hudCopy.text = $"현재 시험: {station.title} / 목표 문양: {SpellLabels.Korean(station.family)}\nWASD 이동, E 또는 Space로 입력";
                promptTitle.text = station.title;
                promptCopy.text = station.prompt;
                promptPanel.gameObject.SetActive(!IsDrawingPanelVisible && !surveyPanel.gameObject.activeSelf && Vector2.Distance(player.position, station.transform.position) <= 1.9f);
            }
            else
            {
                hudCopy.text = "모든 시험을 통과했습니다. 설문을 저장하면 HCI 로그가 완성됩니다.";
                promptPanel.gameObject.SetActive(false);
                surveyPanel.gameObject.SetActive(true);
            }
            progressFill.fillAmount = stationViews.Length == 0 ? 0f : activeStationIndex / (float)stationViews.Length;
        }

        private void TryOpenActiveStation()
        {
            if (activeStationIndex >= stationViews.Length)
            {
                surveyPanel.gameObject.SetActive(true);
                return;
            }

            var station = stationViews[activeStationIndex];
            if (Vector2.Distance(player.position, station.transform.position) > 1.9f)
            {
                ShowBanner("현재 시험대 가까이 이동한 뒤 E 또는 Space를 누르세요.");
                return;
            }

            OpenDrawingPanel();
        }

        private void OpenDrawingPanel()
        {
            var station = stationViews[activeStationIndex];
            currentAttempt = station.attempts + 1;
            currentHint = HintAssistance.PreviewFor(station.family, station.failureCount, lastResult);
            drawingCanvas.Clear();
            drawingCanvas.SetGuide(station.family, currentHint.currentLevel, station.color);
            drawPanel.gameObject.SetActive(true);
            surveyPanel.gameObject.SetActive(false);
            resultCopy.text = $"목표: {SpellLabels.Korean(station.family)}\n시도 {currentAttempt}\n안내선을 참고해 문양을 그려 보세요.";
            resultHint.text = $"{currentHint.title}\n{currentHint.body}";
            resultMetrics.text = "";
            lastResult = null;
        }

        private void OpenPromptOnly()
        {
            drawPanel.gameObject.SetActive(false);
        }

        private void ClearAttempt()
        {
            drawingCanvas.Clear();
            lastResult = null;
            var station = stationViews[activeStationIndex];
            var hint = HintAssistance.PreviewFor(station.family, station.failureCount);
            drawingCanvas.SetGuide(station.family, hint.currentLevel, station.color);
            resultCopy.text = $"목표: {SpellLabels.Korean(station.family)}\n다시 그려 보세요.";
            resultHint.text = $"{hint.title}\n{hint.body}";
            resultMetrics.text = "";
        }

        private SpellResult CastCurrentGesture()
        {
            var station = stationViews[activeStationIndex];
            station.attempts++;
            currentAttempt = station.attempts;
            var priorFailures = station.failureCount;
            var result = GestureRecognizer.Recognize(drawingCanvas.SnapshotStrokes(), station.family);
            lastResult = result;
            currentHint = HintAssistance.ForAttempt(station.family, priorFailures, result.success, result);

            if (!result.success)
            {
                station.failureCount++;
                currentHint = HintAssistance.ForAttempt(station.family, priorFailures, false, result);
                drawingCanvas.SetGuide(station.family, currentHint.currentLevel, station.color);
            }
            else
            {
                station.ApplySuccessEffect();
                completedTrials.Add(new CompletedTrial(station.family, currentAttempt, Time.time - stationStartTime));
                pulses.Add(new ParticlePulse(station.transform.position, station.color));
            }

            LogAttempt(station, result, currentHint);
            RenderResult(station, result, currentHint);
            return result;
        }

        private void RenderResult(ExamStationView station, SpellResult result, HintState hint)
        {
            var status = result.success ? "성공" : result.status.ToString();
            resultCopy.text = $"상태: {status}\n인식: {result.RecognizedFamilyText}\n신뢰도: {result.confidence * 100f:0}%\n{result.feedbackReason}";
            resultHint.text = $"{hint.title}\n{hint.body}";
            resultMetrics.text = $"닫힘 {result.quality.closure * 100f:0}% / 매끄러움 {result.quality.smoothness * 100f:0}%\n속도 {result.quality.tempo * 100f:0}% / 안정감 {result.quality.stability * 100f:0}%\n보조 단계: {hint.AssistLevelNumber}";
        }

        private void CompleteCurrentStation()
        {
            if (lastResult?.success != true)
            {
                ShowBanner("성공한 뒤 다음 시험으로 넘어갈 수 있습니다.");
                return;
            }
            drawPanel.gameObject.SetActive(false);
            activeStationIndex++;
            stationStartTime = Time.time;
            if (activeStationIndex >= stationViews.Length)
            {
                surveyPanel.gameObject.SetActive(true);
            }
        }

        private void SaveSurvey()
        {
            logger.LogSurvey(new SurveyLog
            {
                sessionId = sessionId,
                clarity = Mathf.RoundToInt(surveyScores[0]),
                fairness = Mathf.RoundToInt(surveyScores[1]),
                feedbackHelpfulness = Mathf.RoundToInt(surveyScores[2]),
                controlFeeling = Mathf.RoundToInt(surveyScores[3]),
                immersion = Mathf.RoundToInt(surveyScores[4]),
                comment = surveyComment.text,
                completedTrials = completedTrials.Count,
                totalAttempts = stationViews.Sum(station => station.attempts)
            });
            surveyMeta.text = $"저장 완료: {logger.OutputDirectory}";
        }

        private void LogAttempt(ExamStationView station, SpellResult result, HintState hint)
        {
            logger.LogAttempt(new AttemptLog
            {
                sessionId = sessionId,
                trialId = $"{activeStationIndex + 1}-{station.attempts}",
                targetFamily = SpellLabels.English(station.family),
                recognizedFamily = result.RecognizedFamilyText,
                status = result.status.ToString(),
                confidence = result.confidence,
                closure = result.quality.closure,
                smoothness = result.quality.smoothness,
                tempo = result.quality.tempo,
                stability = result.quality.stability,
                rotationBias = result.quality.rotationBias,
                attemptIndex = station.attempts,
                elapsedMs = Mathf.RoundToInt((Time.time - stationStartTime) * 1000f),
                feedbackViewed = true,
                success = result.success,
                hintShown = hint.hintShown,
                assistLevel = hint.AssistLevelNumber,
                assisted = hint.assisted
            });
        }

        private void ShowBanner(string message)
        {
            lastBanner = message;
            bannerShownAt = Time.time;
            resultHint.text = message;
        }

        private static ExamStationView CreateStation(StationSpec spec)
        {
            var root = new GameObject($"{spec.family} Station");
            root.transform.position = spec.position;
            root.transform.localScale = Vector3.one * 1.3f;
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 2;
            var station = root.AddComponent<ExamStationView>();
            station.family = spec.family;
            station.title = spec.title;
            station.prompt = spec.prompt;
            station.color = spec.color;
            station.stationRenderer = renderer;
            var rune = new GameObject("Rune Circle");
            rune.transform.SetParent(root.transform, false);
            rune.transform.localPosition = new Vector3(0f, -0.12f, 0f);
            rune.transform.localScale = Vector3.one * 1.55f;
            rune.AddComponent<SpriteRenderer>();
            var runeSprite = rune.AddComponent<PixelSpriteView>();
            runeSprite.kind = PixelSpriteKind.RuneCircle;
            runeSprite.primary = spec.color;
            runeSprite.secondary = Color.white;
            runeSprite.sortingOrder = 1;
            var target = new GameObject("Target");
            target.transform.SetParent(root.transform, false);
            target.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            target.transform.localScale = Vector3.one * 0.7f;
            var targetRenderer = target.AddComponent<SpriteRenderer>();
            targetRenderer.sortingOrder = 3;
            station.targetTransform = target.transform;
            station.targetRenderer = targetRenderer;
            return station;
        }

        public static StationSpec[] StationSpecs()
        {
            return new[]
            {
                new StationSpec(SpellFamily.Fire, new Vector2(-5.5f, 2.8f), "점화 시험", "닫힌 삼각형으로 목표물을 태우세요.", new Color(1f, 0.31f, 0.18f)),
                new StationSpec(SpellFamily.Water, new Vector2(0f, 3.2f), "정화 시험", "둥근 폐합 루프로 불길을 식히세요.", new Color(0.18f, 0.56f, 1f)),
                new StationSpec(SpellFamily.Wind, new Vector2(5.5f, 2.8f), "흐름 시험", "평행선 3개로 돌기둥을 밀어내세요.", new Color(0.52f, 0.95f, 0.95f)),
                new StationSpec(SpellFamily.Earth, new Vector2(-3.2f, -2.6f), "축조 시험", "닫힌 사다리꼴로 방어벽을 세우세요.", new Color(0.74f, 0.55f, 0.32f)),
                new StationSpec(SpellFamily.Life, new Vector2(3.2f, -2.6f), "생장 시험", "뿌리와 가지가 있는 Y로 씨앗을 성장시키세요.", new Color(0.35f, 0.86f, 0.42f))
            };
        }

        private GameObject CreateWorldSprite(string name, Vector2 position, Vector3 scale, Color primary, Color secondary, PixelSpriteKind kind, int sortingOrder, bool tiled = false, Vector2 tiledSize = default)
        {
            var body = new GameObject(name);
            body.transform.position = position;
            body.transform.localScale = scale;
            body.AddComponent<SpriteRenderer>();
            var pixelSprite = body.AddComponent<PixelSpriteView>();
            pixelSprite.kind = kind;
            pixelSprite.primary = primary;
            pixelSprite.secondary = secondary;
            pixelSprite.sortingOrder = sortingOrder;
            pixelSprite.tiled = tiled;
            pixelSprite.tiledSize = tiledSize == default ? Vector2.one : tiledSize;
            return body;
        }

        private static void ClearChildren(Transform parent)
        {
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                DestroyImmediate(parent.GetChild(index).gameObject);
            }
        }

        private RectTransform CreatePanel(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Anchor anchor)
        {
            var image = CreateImage(name, parent, anchoredPosition, size, anchor, new Color(0.05f, 0.07f, 0.10f, 0.94f));
            return image.rectTransform;
        }

        private Image CreateImage(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Anchor anchor, Color color)
        {
            var body = new GameObject(name);
            body.transform.SetParent(parent, false);
            var rect = body.AddComponent<RectTransform>();
            ApplyAnchor(rect, anchor);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var image = body.AddComponent<Image>();
            image.color = color;
            image.material = PixelMaterialProvider.UiMaterial;
            return image;
        }

        private SpellDrawingCanvas CreateDrawingSurface(RectTransform parent)
        {
            var body = new GameObject("Drawing Surface");
            body.transform.SetParent(parent, false);
            var rect = body.AddComponent<RectTransform>();
            ApplyAnchor(rect, Anchor.Stretch);
            var surface = body.AddComponent<SpellDrawingCanvas>();
            surface.color = Color.clear;
            surface.material = PixelMaterialProvider.UiMaterial;
            return surface;
        }

        private Text CreateText(string name, Transform parent, string content, int size, FontStyle style, Vector2 anchoredPosition, Vector2 rectSize, Anchor anchor)
        {
            var body = new GameObject(name);
            body.transform.SetParent(parent, false);
            var rect = body.AddComponent<RectTransform>();
            ApplyAnchor(rect, anchor);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = rectSize;
            var text = body.AddComponent<Text>();
            text.font = uiFont;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPosition, Vector2 size, Anchor anchor, Action action)
        {
            var image = CreateImage(name, parent, anchoredPosition, size, anchor, new Color(0.14f, 0.24f, 0.32f, 1f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            var text = CreateText("Label", image.transform, label, 15, FontStyle.Bold, Vector2.zero, size, Anchor.Center);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private Image CreateProgressBar(Transform parent)
        {
            var back = CreateImage("Progress Back", parent, new Vector2(16, 14), new Vector2(500, 14), Anchor.BottomLeft, new Color(1f, 1f, 1f, 0.14f));
            var fill = CreateImage("Progress Fill", back.transform, Vector2.zero, new Vector2(500, 14), Anchor.StretchLeft, new Color(0.18f, 0.95f, 1f, 1f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            return fill;
        }

        private Slider CreateSlider(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Anchor anchor, float value)
        {
            var back = CreateImage(name, parent, anchoredPosition, size, anchor, new Color(1f, 1f, 1f, 0.16f));
            var slider = back.gameObject.AddComponent<Slider>();
            slider.minValue = 1f;
            slider.maxValue = 5f;
            slider.wholeNumbers = true;
            slider.value = value;
            var fill = CreateImage("Fill", back.transform, Vector2.zero, size, Anchor.Stretch, new Color(0.18f, 0.95f, 1f, 0.8f));
            slider.fillRect = fill.rectTransform;
            var handle = CreateImage("Handle", back.transform, Vector2.zero, new Vector2(18, 26), Anchor.Center, Color.white);
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            return slider;
        }

        private InputField CreateInputField(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Anchor anchor)
        {
            var image = CreateImage(name, parent, anchoredPosition, size, anchor, new Color(1f, 1f, 1f, 0.12f));
            var input = image.gameObject.AddComponent<InputField>();
            input.lineType = InputField.LineType.MultiLineNewline;
            var text = CreateText("Text", image.transform, "", 14, FontStyle.Normal, new Vector2(8, -8), size - new Vector2(16, 16), Anchor.TopLeft);
            var placeholder = CreateText("Placeholder", image.transform, "좋았던 점/불편했던 점을 적어 주세요.", 14, FontStyle.Italic, new Vector2(8, -8), size - new Vector2(16, 16), Anchor.TopLeft);
            placeholder.color = new Color(1f, 1f, 1f, 0.45f);
            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }

        private static void ApplyAnchor(RectTransform rect, Anchor anchor)
        {
            switch (anchor)
            {
                case Anchor.TopLeft:
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    break;
                case Anchor.TopRight:
                    rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(1f, 1f);
                    break;
                case Anchor.BottomLeft:
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
                    rect.pivot = new Vector2(0f, 0f);
                    break;
                case Anchor.BottomRight:
                    rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(1f, 0f);
                    break;
                case Anchor.Stretch:
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    break;
                case Anchor.StretchLeft:
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0f, 0.5f);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    break;
                default:
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    break;
            }
        }

        public readonly struct StationSpec
        {
            public readonly SpellFamily family;
            public readonly Vector2 position;
            public readonly string title;
            public readonly string prompt;
            public readonly Color color;

            public StationSpec(SpellFamily family, Vector2 position, string title, string prompt, Color color)
            {
                this.family = family;
                this.position = position;
                this.title = title;
                this.prompt = prompt;
                this.color = color;
            }
        }

        private enum Anchor
        {
            Center,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
            Stretch,
            StretchLeft
        }

        private sealed class ParticlePulse
        {
            public readonly Vector2 position;
            public readonly Color color;
            public GameObject body;
            public float age;

            public ParticlePulse(Vector2 position, Color color)
            {
                this.position = position;
                this.color = color;
            }
        }

        private readonly struct CompletedTrial
        {
            public readonly SpellFamily family;
            public readonly int attempts;
            public readonly float elapsedSeconds;

            public CompletedTrial(SpellFamily family, int attempts, float elapsedSeconds)
            {
                this.family = family;
                this.attempts = attempts;
                this.elapsedSeconds = elapsedSeconds;
            }
        }
    }
}
