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

        private readonly List<SealView> seals = new();
        private readonly List<ParticlePulse> pulses = new();
        private readonly List<GameObject> floorObjects = new();
        private readonly List<WorldStateGoal> activeGoals = new();
        private readonly List<HazardZone> activeHazards = new();
        private readonly Dictionary<SpellFamily, int> baseFailureCounts = new();

        private ExamLogger logger = null!;
        private WorldDrawingController worldDrawing = null!;
        private FloorController floorController = null!;
        private MagicNote magicNote = null!;
        private EndingReport endingReport = null!;
        private RectTransform hudPanel = null!;
        private RectTransform notePanel = null!;
        private RectTransform reportPanel = null!;
        private Text hudTitle = null!;
        private Text hudCopy = null!;
        private Text floorProgress = null!;
        private Text noteText = null!;
        private Text reportText = null!;
        private Font uiFont = null!;
        private string sessionId = "";
        private int trialCounter;
        private float floorStartedAt;
        private float pendingAdvanceAt = -1f;
        private Vector2 velocity;
        private Vector2 safePosition;

        public int CurrentFloorNumber => floorController?.CurrentFloorNumber ?? 1;
        public int FloorCount => floorController?.FloorCount ?? 5;
        public int ActiveSealCount => seals.Count;
        public int ActiveGoalCount => activeGoals.Count;
        public Vector2 PlayerPosition => player == null ? Vector2.zero : player.position;
        public bool HasEndingReport => reportPanel != null && reportPanel.gameObject.activeSelf;
        public bool IsDrawingPanelVisible => false;
        public bool IsResultPanelVisible => false;
        public int CurrentAssistLevel { get; private set; }
        public string LastHintText { get; private set; } = "";
        public string LastMagicNoteText => magicNote?.Text ?? "";
        public string OutputDirectory => logger?.OutputDirectory ?? "";
        public float LastSealLifetimeSecondsForTests => seals.Count == 0 ? 0f : seals[^1].seal.expiresAt - seals[^1].seal.createdAt;
        public IReadOnlyList<OverlayOperator> LastOverlayStack => seals.Count == 0 ? Array.Empty<OverlayOperator>() : seals[^1].seal.overlayStack;

        private void Awake()
        {
            sessionId = $"unity-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
            logger = new ExamLogger(sessionId);
            uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 18);
            floorController = new FloorController();
            magicNote = new MagicNote();
            endingReport = new EndingReport();
            ResolveSceneReferences();
            BuildUi();
            ConfigureWorldDrawing();
            LoadFloor(0);
        }

        private void Update()
        {
            TickPlayer();
            TickSeals();
            TickPulses();
            TickHazards();
            TickFloorAdvance();
            magicNote.Tick(Time.deltaTime);
            UpdateHud();
        }

        public BaseRecognitionResult CastSyntheticBaseForTests(SpellFamily family, Vector2 worldCenter)
        {
            var strokes = Offset(GestureRecognizer.CreateCanonicalSamples(family, 1.6f, 0.03f), worldCenter, 0.8f);
            return ProcessSpellGroup(strokes, worldCenter, strokes.Count).baseResult;
        }

        public BaseRecognitionResult CastRawBaseForTests(List<List<StrokeSample>> strokes, Vector2 worldCenter)
        {
            return ProcessBase(strokes, worldCenter, strokes.Count).baseResult;
        }

        public OverlayRecognitionResult CastSyntheticOverlayForTests(OverlayOperator op, Vector2 worldCenter)
        {
            var nearestSeal = FindAttachableSeal(worldCenter);
            var scale = nearestSeal == null ? 0.48f : nearestSeal.seal.worldScale * 0.24f;
            var strokes = OverlayRecognizer.CreateCanonicalSamples(op, worldCenter, scale, 0.03f);
            return ProcessSpellGroup(strokes, worldCenter, strokes.Count).overlayResult;
        }

        public void CompleteCurrentFloorForTests()
        {
            foreach (var goal in activeGoals)
            {
                ActivateGoal(goal, "test_completion");
            }
            EvaluateFloorCompletion();
        }

        public void AdvanceFloorForTests()
        {
            if (floorController.CurrentFloorIndex < floorController.FloorCount - 1)
            {
                LoadFloor(floorController.CurrentFloorIndex + 1);
            }
            else
            {
                ShowEndingReport();
            }
        }

        public void LoadFloorForTests(int index)
        {
            LoadFloor(index);
        }

        public void MovePlayerForTests(Vector2 worldPosition)
        {
            player.position = worldPosition;
        }

        private void ResolveSceneReferences()
        {
            mainCamera ??= Camera.main;
            if (mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                mainCamera = cameraObject.AddComponent<Camera>();
                mainCamera.orthographic = true;
                mainCamera.orthographicSize = 6.2f;
                mainCamera.backgroundColor = new Color(0.06f, 0.08f, 0.11f);
            }

            if (player == null)
            {
                var playerObject = new GameObject("Apprentice");
                playerObject.transform.position = new Vector3(0f, -4.05f, 0f);
                playerObject.transform.localScale = Vector3.one * 0.78f;
                playerObject.AddComponent<SpriteRenderer>();
                var sprite = playerObject.AddComponent<PixelSpriteView>();
                sprite.kind = PixelSpriteKind.Player;
                sprite.primary = new Color(0.95f, 0.92f, 0.78f);
                sprite.secondary = new Color(0.28f, 0.62f, 0.96f);
                sprite.sortingOrder = 30;
                player = playerObject.transform;
            }

            if (canvas == null)
            {
                var canvasObject = new GameObject("Exam Canvas");
                canvasObject.AddComponent<RectTransform>();
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }
        }

        private void ConfigureWorldDrawing()
        {
            worldDrawing = gameObject.GetComponent<WorldDrawingController>() ?? gameObject.AddComponent<WorldDrawingController>();
            worldDrawing.mainCamera = mainCamera;
            worldDrawing.ApplyPlayableDefaults();
            worldDrawing.SpellBuffered += OnSpellBuffered;
        }

        private void BuildUi()
        {
            ClearChildren(canvas.transform);
            hudPanel = CreatePanel("HUD", canvas.transform, new Vector2(20, -20), new Vector2(560, 132), Anchor.TopLeft, new Color(0.04f, 0.055f, 0.075f, 0.88f));
            hudTitle = CreateText("HUD Title", hudPanel, "Magic Exam Hall", 24, FontStyle.Bold, new Vector2(16, -12), new Vector2(520, 28), Anchor.TopLeft);
            hudCopy = CreateText("HUD Copy", hudPanel, "", 15, FontStyle.Normal, new Vector2(16, -46), new Vector2(520, 60), Anchor.TopLeft);
            floorProgress = CreateText("Floor Progress", hudPanel, "", 15, FontStyle.Bold, new Vector2(16, 12), new Vector2(520, 24), Anchor.BottomLeft);

            notePanel = CreatePanel("Magic Note", canvas.transform, new Vector2(20, 20), new Vector2(560, 112), Anchor.BottomLeft, new Color(0.04f, 0.055f, 0.075f, 0.84f));
            noteText = CreateText("Note Text", notePanel, "", 14, FontStyle.Normal, new Vector2(14, -12), new Vector2(530, 88), Anchor.TopLeft);

            reportPanel = CreatePanel("Ending Report", canvas.transform, Vector2.zero, new Vector2(760, 520), Anchor.Center, new Color(0.035f, 0.045f, 0.065f, 0.96f));
            reportText = CreateText("Report Text", reportPanel, "", 17, FontStyle.Normal, new Vector2(28, -28), new Vector2(704, 464), Anchor.TopLeft);
            reportPanel.gameObject.SetActive(false);
        }

        private void LoadFloor(int index)
        {
            pendingAdvanceAt = -1f;
            ClearFloorObjects();
            floorController.Load(index);
            safePosition = new Vector2(0f, -4.05f);
            player.position = safePosition;
            floorStartedAt = Time.time;
            activeGoals.Clear();
            activeGoals.AddRange(floorController.Current.goals.Select(goal => goal.Clone()));
            activeHazards.Clear();
            activeHazards.AddRange(floorController.Current.hazards.Select(hazard => hazard.Clone()));
            BuildFloorArt(floorController.Current);
            magicNote.Show(floorController.Current.entryNote);
        }

        private void BuildFloorArt(FloorDefinition floor)
        {
            var floorRoot = new GameObject($"Floor {floor.number} - {floor.title}");
            floorObjects.Add(floorRoot);
            CreateWorldSprite("Stone Tile Floor", Vector2.zero, Vector3.one, new Color(0.15f, 0.17f, 0.22f), new Color(0.09f, 0.11f, 0.15f), PixelSpriteKind.FloorTile, -7, true, new Vector2(16.4f, 10f), floorRoot.transform);
            CreateWorldSprite("North Carved Wall", new Vector2(0f, 4.95f), Vector3.one, new Color(0.22f, 0.20f, 0.27f), floor.accentColor, PixelSpriteKind.WallTrim, -4, true, new Vector2(16.4f, 1.15f), floorRoot.transform);
            CreateWorldSprite("South Carved Wall", new Vector2(0f, -4.95f), Vector3.one, new Color(0.18f, 0.17f, 0.22f), new Color(0.50f, 0.40f, 0.20f), PixelSpriteKind.WallTrim, -4, true, new Vector2(16.4f, 0.8f), floorRoot.transform);
            CreateWorldSprite("Center Runner", new Vector2(0f, 0.12f), Vector3.one, floor.rugColor, floor.accentColor, PixelSpriteKind.Rug, -5, true, new Vector2(2.2f, 7.6f), floorRoot.transform);
            CreateWorldSprite("West Bookcase", new Vector2(-7.25f, 1.1f), Vector3.one * 1.15f, new Color(0.42f, 0.23f, 0.12f), floor.accentColor, PixelSpriteKind.Bookshelf, -1, false, Vector2.one, floorRoot.transform);
            CreateWorldSprite("East Bookcase", new Vector2(7.25f, 1.1f), Vector3.one * 1.15f, new Color(0.42f, 0.23f, 0.12f), floor.accentColor, PixelSpriteKind.Bookshelf, -1, false, Vector2.one, floorRoot.transform);
            CreateWorldSprite("Northwest Candle", new Vector2(-6.85f, 3.65f), Vector3.one * 0.85f, new Color(0.63f, 0.57f, 0.44f), new Color(1f, 0.56f, 0.15f), PixelSpriteKind.Candle, 2, false, Vector2.one, floorRoot.transform);
            CreateWorldSprite("Northeast Candle", new Vector2(6.85f, 3.65f), Vector3.one * 0.85f, new Color(0.63f, 0.57f, 0.44f), new Color(1f, 0.56f, 0.15f), PixelSpriteKind.Candle, 2, false, Vector2.one, floorRoot.transform);

            foreach (var goal in activeGoals)
            {
                var body = CreateWorldSprite(goal.title, goal.position, Vector3.one * goal.visualScale, goal.color, Color.white, goal.kind, 3, false, Vector2.one, floorRoot.transform);
                goal.body = body;
                goal.renderer = body.GetComponent<SpriteRenderer>();
                if (goal.kind == PixelSpriteKind.RuneCircle)
                {
                    body.transform.localScale *= 1.45f;
                }
            }

            foreach (var hazard in activeHazards)
            {
                var body = CreateWorldSprite(hazard.title, hazard.position, Vector3.one * hazard.radius, hazard.color, new Color(1f, 1f, 1f, 0.6f), PixelSpriteKind.Pulse, 1, false, Vector2.one, floorRoot.transform);
                hazard.body = body;
            }
        }

        private void OnSpellBuffered(List<List<StrokeSample>> strokes, Vector2 center, int strokeCount)
        {
            ProcessSpellGroup(strokes, center, strokeCount);
        }

        private ProcessedSpell ProcessSpellGroup(List<List<StrokeSample>> strokes, Vector2 center, int strokeCount)
        {
            trialCounter++;
            var nearestSeal = FindAttachableSeal(center);
            if (nearestSeal != null)
            {
                return ProcessOverlay(strokes, center, strokeCount, nearestSeal);
            }

            return ProcessBase(strokes, center, strokeCount);
        }

        private ProcessedSpell ProcessBase(List<List<StrokeSample>> strokes, Vector2 center, int strokeCount)
        {
            var baseResult = SpellRuntime.RecognizeBase(strokes);
            baseResult.center = center;
            baseResult.bufferStrokeCount = strokeCount;
            var feedbackFamily = baseResult.spell.recognizedFamily ?? baseResult.spell.targetFamily;
            var priorFailures = GetBaseFailureCount(feedbackFamily);
            if (baseResult.spell.status != RecognitionStatus.Recognized || !baseResult.spell.recognizedFamily.HasValue)
            {
                var hintState = HintAssistance.ForAttempt(feedbackFamily, priorFailures, false, baseResult.spell);
                baseFailureCounts[feedbackFamily] = priorFailures + 1;
                CurrentAssistLevel = hintState.AssistLevelNumber;
                LastHintText = hintState.body;
                magicNote.Show(BuildBaseFailureNote(baseResult.spell, hintState));
                pulses.Add(new ParticlePulse(center, new Color(0.75f, 0.75f, 0.82f), weak: true));
                LogBaseAttempt(baseResult, null, "failed", hintState);
                return new ProcessedSpell { baseResult = baseResult };
            }

            var seal = SpellRuntime.CreateSeal(baseResult, Time.time);
            var successHintState = HintAssistance.ForAttempt(seal.baseFamily, priorFailures, true, baseResult.spell);
            baseFailureCounts[seal.baseFamily] = 0;
            CurrentAssistLevel = successHintState.AssistLevelNumber;
            LastHintText = successHintState.assisted ? successHintState.body : "";
            var view = CreateSealView(seal);
            seals.Add(view);
            endingReport.RecordBase(seal.baseFamily, seal.quality, success: true);
            var effect = ApplyBaseToGoals(seal.baseFamily, center);
            magicNote.Show(BuildBaseSuccessNote(seal, effect, successHintState));
            pulses.Add(new ParticlePulse(center, FamilyColor(seal.baseFamily)));
            LogBaseAttempt(baseResult, seal, effect.worldEffect, successHintState);
            EvaluateFloorCompletion();
            return new ProcessedSpell { baseResult = baseResult };
        }

        private ProcessedSpell ProcessOverlay(List<List<StrokeSample>> strokes, Vector2 center, int strokeCount, SealView sealView)
        {
            var result = OverlayRecognizer.Recognize(strokes, sealView.seal);
            if (!result.success)
            {
                CurrentAssistLevel = 1;
                LastHintText = OverlayActionHint(result, sealView.seal);
                magicNote.Show(BuildOverlayFailureNote(result, sealView.seal));
                pulses.Add(new ParticlePulse(center, new Color(0.75f, 0.75f, 0.82f), weak: true));
                LogOverlayAttempt(result, sealView.seal, center, strokeCount, "failed");
                return new ProcessedSpell { overlayResult = result };
            }

            var op = result.recognizedOperator!.Value;
            if (sealView.seal.overlayStack.Contains(op))
            {
                CurrentAssistLevel = 1;
                LastHintText = "같은 장식 대신 아직 비어 있는 다른 장식을 seal 위에 그려 보세요.";
                magicNote.Show($"{SpellLabels.Korean(op)} 장식은 이미 이 seal에 붙어 있습니다.");
            }
            else if (sealView.seal.overlayStack.Count >= 3)
            {
                CurrentAssistLevel = 1;
                LastHintText = "새 base seal을 만든 뒤 남은 장식을 붙여 보세요.";
                magicNote.Show("하나의 seal에는 overlay를 3개까지만 안정적으로 붙일 수 있습니다.");
            }
            else
            {
                sealView.seal.overlayStack.Add(op);
                sealView.RefreshLabel(uiFont);
                sealView.AddOverlayMark(op);
                endingReport.RecordOverlay(op);
                var effect = ApplyOverlayToGoals(sealView.seal, op, center);
                CurrentAssistLevel = 0;
                LastHintText = "";
                magicNote.Show(BuildOverlaySuccessNote(sealView.seal, op, effect));
                LogOverlayAttempt(result, sealView.seal, center, strokeCount, effect.worldEffect);
                EvaluateFloorCompletion();
            }

            pulses.Add(new ParticlePulse(center, OverlayColor(op)));
            return new ProcessedSpell { overlayResult = result };
        }

        private SealView FindAttachableSeal(Vector2 center)
        {
            return seals
                .Where(seal => Time.time <= seal.seal.expiresAt)
                .OrderBy(seal => Vector2.Distance(center, seal.seal.worldCenter))
                .FirstOrDefault(seal => Vector2.Distance(center, seal.seal.worldCenter) <= Mathf.Max(1.35f, seal.seal.worldScale * 0.95f));
        }

        private GoalEffect ApplyBaseToGoals(SpellFamily family, Vector2 center)
        {
            foreach (var goal in activeGoals.Where(goal => !goal.completed))
            {
                if (goal.MatchesBase(family, center))
                {
                    ActivateGoal(goal, SpellLabels.English(family));
                    return new GoalEffect($"{goal.discoveryNote}", goal.id);
                }
            }

            return new GoalEffect($"{SpellLabels.Korean(family)} seal이 바닥에 잠깐 고정되었습니다.", "seal_only");
        }

        private GoalEffect ApplyOverlayToGoals(CompiledSeal seal, OverlayOperator op, Vector2 center)
        {
            foreach (var goal in activeGoals.Where(goal => !goal.completed))
            {
                if (goal.MatchesOverlay(seal, op, center))
                {
                    ActivateGoal(goal, SpellLabels.English(op));
                    return new GoalEffect($"{goal.discoveryNote}", goal.id);
                }
            }

            return new GoalEffect($"{seal.Label}: overlay stack이 빛났습니다.", "overlay_stack");
        }

        private int GetBaseFailureCount(SpellFamily family)
        {
            return baseFailureCounts.TryGetValue(family, out var count) ? count : 0;
        }

        private static string BuildBaseFailureNote(SpellResult result, HintState hintState)
        {
            return
                $"노트: {SpellLabels.Korean(hintState.family)} 문양이 아직 안정되지 않았습니다.\n" +
                $"{result.feedbackReason}\n" +
                $"{hintState.title}: {hintState.body}";
        }

        private static string BuildBaseSuccessNote(CompiledSeal seal, GoalEffect effect, HintState hintState)
        {
            var assisted = hintState.assisted ? " 이전 힌트가 이번 시전에 도움이 되었습니다." : "";
            return $"{SpellLabels.Korean(seal.baseFamily)} seal 성공.{assisted}\n{effect.note}";
        }

        private static string BuildOverlayFailureNote(OverlayRecognitionResult result, CompiledSeal seal)
        {
            return
                "노트: 장식이 seal에 안정적으로 붙지 않았습니다.\n" +
                $"{result.feedbackReason}\n" +
                $"다음: {OverlayActionHint(result, seal)}";
        }

        private static string BuildOverlaySuccessNote(CompiledSeal seal, OverlayOperator op, GoalEffect effect)
        {
            return $"{SpellLabels.Korean(op)} 장식 성공. {seal.Label}\n{effect.note}";
        }

        private static string OverlayActionHint(OverlayRecognitionResult result, CompiledSeal seal)
        {
            if (result.recognizedOperator == OverlayOperator.MartialAxis && !seal.overlayStack.Contains(OverlayOperator.VoidCut))
            {
                return "먼저 같은 seal에 대각선 절단(void_cut)을 붙인 뒤, 중심을 가르는 축을 다시 그리세요.";
            }

            if (result.scaleRatio > 0f && result.scaleRatio < 0.10f)
            {
                return "장식이 너무 작습니다. seal 중심을 기준으로 조금 더 크게 그려 보세요.";
            }

            if (result.scaleRatio > 0.64f)
            {
                return "장식이 너무 큽니다. seal 안쪽에 들어오도록 작게 줄여 보세요.";
            }

            return AnchorHint(result.anchorZone);
        }

        private static string AnchorHint(string anchorZone)
        {
            return anchorZone switch
            {
                "upper_right" => "seal의 오른쪽 위 가장자리에서 짧고 또렷하게 다시 그려 보세요.",
                "right" => "seal의 오른쪽 가장자리 옆에 붙이듯 다시 그려 보세요.",
                "lower_right" => "seal의 오른쪽 아래 가장자리에서 다시 그려 보세요.",
                "upper" => "seal의 위쪽 가장자리 가까이에서 다시 그려 보세요.",
                "left" => "seal의 왼쪽 가장자리 가까이에서 다시 그려 보세요.",
                _ => "seal 중심 가까이에 작게, 한 가지 장식만 다시 그려 보세요."
            };
        }

        private void ActivateGoal(WorldStateGoal goal, string effect)
        {
            goal.completed = true;
            if (goal.renderer != null)
            {
                goal.renderer.sprite = PixelArtFactory.CreateSprite($"{goal.title} Active", Color.white, goal.color, goal.kind);
                goal.renderer.sharedMaterial = PixelMaterialProvider.SpriteMaterial;
            }
            if (goal.body != null)
            {
                goal.body.transform.localScale *= 1.15f;
            }
            endingReport.RecordDiscovery(goal.id, effect);
            pulses.Add(new ParticlePulse(goal.position, goal.color));
        }

        private void EvaluateFloorCompletion()
        {
            if (!activeGoals.All(goal => goal.completed) || pendingAdvanceAt > 0f || HasEndingReport)
            {
                return;
            }

            magicNote.Show(floorController.Current.completeNote);
            pendingAdvanceAt = Time.time + 1.4f;
        }

        private void TickFloorAdvance()
        {
            if (pendingAdvanceAt < 0f || Time.time < pendingAdvanceAt)
            {
                return;
            }

            pendingAdvanceAt = -1f;
            if (floorController.CurrentFloorIndex < floorController.FloorCount - 1)
            {
                LoadFloor(floorController.CurrentFloorIndex + 1);
                return;
            }

            ShowEndingReport();
        }

        private void TickPlayer()
        {
            if (HasEndingReport)
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
            player.position = new Vector3(Mathf.Clamp(player.position.x, -7.35f, 7.35f), Mathf.Clamp(player.position.y, -4.25f, 4.25f), 0f);
        }

        private void TickHazards()
        {
            if (floorController.Current.number != 4)
            {
                return;
            }

            foreach (var hazard in activeHazards)
            {
                hazard.Tick(Time.time);
                if (Vector2.Distance(player.position, hazard.position) <= hazard.radius * 0.58f)
                {
                    player.position = safePosition;
                    velocity = Vector2.zero;
                    magicNote.Show("균열이 몸을 밀어냈습니다. 가까운 안전 지점에서 다시 시작합니다.");
                    pulses.Add(new ParticlePulse(hazard.position, hazard.color, weak: true));
                    return;
                }
            }
        }

        private void TickSeals()
        {
            for (var index = seals.Count - 1; index >= 0; index--)
            {
                var seal = seals[index];
                var remaining = seal.seal.expiresAt - Time.time;
                if (remaining <= 0f)
                {
                    Destroy(seal.root);
                    seals.RemoveAt(index);
                    continue;
                }

                seal.Tick(Time.time, remaining / Mathf.Max(seal.seal.expiresAt - seal.seal.createdAt, 0.1f));
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
                    pulse.body = CreateWorldSprite("Spell Pulse", pulse.position, Vector3.one * (pulse.weak ? 0.22f : 0.35f), pulse.color, Color.white, PixelSpriteKind.Pulse, 28);
                }

                var t = pulse.age / (pulse.weak ? 0.7f : 0.95f);
                pulse.body.transform.localScale = Vector3.one * Mathf.Lerp(pulse.weak ? 0.35f : 0.45f, pulse.weak ? 1.4f : 2.5f, t);
                var renderer = pulse.body.GetComponent<SpriteRenderer>();
                renderer.sharedMaterial = PixelMaterialProvider.SpriteMaterial;
                renderer.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.8f, 0f, t));
                if (t >= 1f)
                {
                    Destroy(pulse.body);
                    pulses.RemoveAt(index);
                }
            }
        }

        private void UpdateHud()
        {
            if (HasEndingReport)
            {
                return;
            }

            var floor = floorController.Current;
            hudTitle.text = $"층 {floor.number}: {floor.title}";
            hudCopy.text = $"{floor.objective}\nWASD 이동 / 우클릭 hold로 바닥에 직접 문양을 그리세요.";
            var completed = activeGoals.Count(goal => goal.completed);
            floorProgress.text = $"탑 진행 {floorController.CurrentFloorNumber}/{floorController.FloorCount}   목표 {completed}/{activeGoals.Count}   seal {seals.Count}";
            notePanel.gameObject.SetActive(magicNote.Visible);
            noteText.text = magicNote.Text;
        }

        private void ShowEndingReport()
        {
            reportPanel.gameObject.SetActive(true);
            notePanel.gameObject.SetActive(false);
            hudCopy.text = "입학 마법진이 다시 밝아졌습니다.";
            logger.LogSurvey(new SurveyLog
            {
                sessionId = sessionId,
                clarity = 5,
                fairness = 5,
                feedbackHelpfulness = 5,
                controlFeeling = 5,
                immersion = 5,
                comment = "auto ending report",
                completedTrials = endingReport.DiscoveryCount,
                totalAttempts = trialCounter
            });
            reportText.text = endingReport.BuildText(trialCounter, OutputDirectory);
        }

        private SealView CreateSealView(CompiledSeal seal)
        {
            var root = CreateWorldSprite($"Seal {seal.sealId}", seal.worldCenter, Vector3.one * Mathf.Max(seal.worldScale * 1.08f, 0.9f), FamilyColor(seal.baseFamily), Color.white, PixelSpriteKind.RuneCircle, 18);
            var labelObject = new GameObject("Rune Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            var canvasObject = new GameObject("Rune Label Canvas");
            canvasObject.transform.SetParent(labelObject.transform, false);
            var worldCanvas = canvasObject.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.sortingOrder = 40;
            var rect = canvasObject.GetComponent<RectTransform>() ?? canvasObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(2.7f, 0.45f);
            canvasObject.transform.localScale = Vector3.one * 0.012f;
            var textObject = new GameObject("Text");
            textObject.transform.SetParent(canvasObject.transform, false);
            var textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObject.AddComponent<Text>();
            text.font = uiFont;
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = seal.Label;
            return new SealView(root, seal, text);
        }

        private void LogBaseAttempt(BaseRecognitionResult result, CompiledSeal seal, string worldEffect, HintState hintState = null)
        {
            var success = result.spell.status == RecognitionStatus.Recognized;
            logger.LogAttempt(new AttemptLog
            {
                sessionId = sessionId,
                trialId = trialCounter.ToString(CultureInfo.InvariantCulture),
                targetFamily = "",
                recognizedFamily = result.spell.RecognizedFamilyText,
                phase = SpellPhase.Base.ToString(),
                baseFamily = result.spell.RecognizedFamilyText,
                overlayStack = "",
                sealId = seal?.sealId ?? "",
                floorId = floorController.Current.number.ToString(CultureInfo.InvariantCulture),
                targetObject = worldEffect,
                worldEffect = worldEffect,
                status = result.spell.status.ToString(),
                confidence = result.spell.confidence,
                closure = result.spell.quality.closure,
                smoothness = result.spell.quality.smoothness,
                tempo = result.spell.quality.tempo,
                stability = result.spell.quality.stability,
                rotationBias = result.spell.quality.rotationBias,
                worldX = result.center.x,
                worldY = result.center.y,
                bufferStrokeCount = result.bufferStrokeCount,
                attemptIndex = trialCounter,
                elapsedMs = Mathf.RoundToInt((Time.time - floorStartedAt) * 1000f),
                feedbackViewed = true,
                success = success,
                hintShown = hintState?.hintShown ?? !success,
                assistLevel = hintState?.AssistLevelNumber ?? (success ? 0 : 1),
                assisted = hintState?.assisted ?? false
            });
        }

        private void LogOverlayAttempt(OverlayRecognitionResult result, CompiledSeal seal, Vector2 center, int strokeCount, string worldEffect)
        {
            logger.LogAttempt(new AttemptLog
            {
                sessionId = sessionId,
                trialId = trialCounter.ToString(CultureInfo.InvariantCulture),
                targetFamily = "",
                recognizedFamily = result.OperatorText,
                phase = SpellPhase.Overlay.ToString(),
                baseFamily = SpellLabels.English(seal.baseFamily),
                overlayStack = string.Join(">", seal.overlayStack.Select(SpellLabels.English)),
                sealId = seal.sealId,
                floorId = floorController.Current.number.ToString(CultureInfo.InvariantCulture),
                targetObject = worldEffect,
                worldEffect = worldEffect,
                status = result.status.ToString(),
                confidence = result.score,
                closure = 0f,
                smoothness = result.shapeConfidence,
                tempo = 0f,
                stability = 0f,
                rotationBias = result.scaleRatio,
                worldX = center.x,
                worldY = center.y,
                bufferStrokeCount = strokeCount,
                attemptIndex = trialCounter,
                elapsedMs = Mathf.RoundToInt((Time.time - floorStartedAt) * 1000f),
                feedbackViewed = true,
                success = result.success,
                hintShown = !result.success,
                assistLevel = result.success ? 0 : CurrentAssistLevel,
                assisted = false
            });
        }

        private void ClearFloorObjects()
        {
            foreach (var body in floorObjects)
            {
                if (body != null)
                {
                    Destroy(body);
                }
            }
            floorObjects.Clear();
            foreach (var seal in seals)
            {
                if (seal.root != null)
                {
                    Destroy(seal.root);
                }
            }
            seals.Clear();
        }

        private GameObject CreateWorldSprite(string name, Vector2 position, Vector3 scale, Color primary, Color secondary, PixelSpriteKind kind, int sortingOrder, bool tiled = false, Vector2 tiledSize = default, Transform parent = null)
        {
            var body = new GameObject(name);
            body.transform.SetParent(parent, true);
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
            pixelSprite.Apply();
            return body;
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

        private RectTransform CreatePanel(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Anchor anchor, Color color)
        {
            return CreateImage(name, parent, anchoredPosition, size, anchor, color).rectTransform;
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

        private static void ClearChildren(Transform parent)
        {
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                DestroyImmediate(parent.GetChild(index).gameObject);
            }
        }

        private static void ApplyAnchor(RectTransform rect, Anchor anchor)
        {
            switch (anchor)
            {
                case Anchor.TopLeft:
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    break;
                case Anchor.BottomLeft:
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
                    rect.pivot = new Vector2(0f, 0f);
                    break;
                case Anchor.Center:
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    break;
            }
        }

        private static Color FamilyColor(SpellFamily family)
        {
            return family switch
            {
                SpellFamily.Fire => new Color(1f, 0.31f, 0.18f),
                SpellFamily.Water => new Color(0.24f, 0.48f, 0.86f),
                SpellFamily.Wind => new Color(0.44f, 0.72f, 0.74f),
                SpellFamily.Earth => new Color(0.74f, 0.55f, 0.32f),
                SpellFamily.Life => new Color(0.35f, 0.86f, 0.42f),
                _ => Color.white
            };
        }

        private static Color OverlayColor(OverlayOperator op)
        {
            return op switch
            {
                OverlayOperator.SteelBrace => new Color(0.78f, 0.82f, 0.86f),
                OverlayOperator.ElectricFork => new Color(1f, 0.9f, 0.22f),
                OverlayOperator.IceBar => new Color(0.48f, 0.84f, 1f),
                OverlayOperator.SoulDot => new Color(0.95f, 0.62f, 1f),
                OverlayOperator.VoidCut => new Color(0.58f, 0.42f, 0.92f),
                OverlayOperator.MartialAxis => new Color(1f, 0.58f, 0.34f),
                _ => Color.white
            };
        }

        private static List<List<StrokeSample>> Offset(List<List<StrokeSample>> strokes, Vector2 center, float canonicalCenter)
        {
            return strokes
                .Select(stroke => stroke.Select(sample => new StrokeSample(sample.position - Vector2.one * canonicalCenter + center, sample.time)).ToList())
                .ToList();
        }

        private enum Anchor
        {
            Center,
            TopLeft,
            BottomLeft
        }

        private readonly struct GoalEffect
        {
            public readonly string note;
            public readonly string worldEffect;

            public GoalEffect(string note, string worldEffect)
            {
                this.note = note;
                this.worldEffect = worldEffect;
            }
        }

        private sealed class ProcessedSpell
        {
            public BaseRecognitionResult baseResult = null;
            public OverlayRecognitionResult overlayResult = null;
        }

        private sealed class ParticlePulse
        {
            public readonly Vector2 position;
            public readonly Color color;
            public readonly bool weak;
            public GameObject body;
            public float age;

            public ParticlePulse(Vector2 position, Color color, bool weak = false)
            {
                this.position = position;
                this.color = color;
                this.weak = weak;
            }
        }

        private sealed class SealView
        {
            public readonly GameObject root;
            public readonly CompiledSeal seal;
            private readonly Text label;
            private readonly List<GameObject> overlayMarks = new();

            public SealView(GameObject root, CompiledSeal seal, Text label)
            {
                this.root = root;
                this.seal = seal;
                this.label = label;
            }

            public void RefreshLabel(Font font)
            {
                label.font = font;
                label.text = seal.Label;
            }

            public void AddOverlayMark(OverlayOperator op)
            {
                var index = overlayMarks.Count;
                var mark = new GameObject($"Overlay Mark {op}");
                mark.transform.SetParent(root.transform, false);
                var angle = index * Mathf.PI * 2f / 3f + Mathf.PI / 4f;
                mark.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.52f, Mathf.Sin(angle) * 0.52f, -0.1f);
                mark.transform.localScale = Vector3.one * 0.22f;
                var renderer = mark.AddComponent<SpriteRenderer>();
                renderer.sprite = PixelArtFactory.CreateSprite($"Overlay {op}", OverlayColor(op), Color.white, PixelSpriteKind.Pulse);
                renderer.sharedMaterial = PixelMaterialProvider.SpriteMaterial;
                renderer.sortingOrder = 24;
                overlayMarks.Add(mark);
            }

            public void Tick(float time, float normalizedLifetime)
            {
                if (root == null)
                {
                    return;
                }
                root.transform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1f, normalizedLifetime) * (1f + Mathf.Sin(time * 4f) * 0.025f);
                var renderer = root.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(normalizedLifetime + 0.16f));
                }
            }
        }
    }

    public sealed class MagicNote
    {
        private float ttl;
        public string Text { get; private set; } = "";
        public bool Visible => ttl > 0f && !string.IsNullOrWhiteSpace(Text);

        public void Show(string text)
        {
            Text = text;
            ttl = 4.4f;
        }

        public void Tick(float deltaTime)
        {
            ttl = Mathf.Max(0f, ttl - deltaTime);
        }
    }

    public sealed class EndingReport
    {
        private readonly Dictionary<SpellFamily, int> baseUse = new();
        private readonly Dictionary<OverlayOperator, int> overlayUse = new();
        private readonly HashSet<string> discoveries = new();
        private readonly List<float> qualityScores = new();

        public int DiscoveryCount => discoveries.Count;

        public void RecordBase(SpellFamily family, QualityVector quality, bool success)
        {
            baseUse[family] = baseUse.TryGetValue(family, out var count) ? count + 1 : 1;
            if (success)
            {
                qualityScores.Add(quality.Average());
            }
        }

        public void RecordOverlay(OverlayOperator op)
        {
            overlayUse[op] = overlayUse.TryGetValue(op, out var count) ? count + 1 : 1;
        }

        public void RecordDiscovery(string id, string effect)
        {
            discoveries.Add($"{id}:{effect}");
        }

        public string BuildText(int totalAttempts, string outputDirectory)
        {
            var favoriteBase = baseUse.Count == 0 ? "없음" : SpellLabels.Korean(baseUse.OrderByDescending(item => item.Value).First().Key);
            var favoriteOverlay = overlayUse.Count == 0 ? "없음" : SpellLabels.Korean(overlayUse.OrderByDescending(item => item.Value).First().Key);
            var averageQuality = qualityScores.Count == 0 ? 0f : qualityScores.Average() * 100f;
            return
                "입학 마법진 복구 완료\n\n" +
                $"총 시도: {totalAttempts}\n" +
                $"가장 많이 사용한 base: {favoriteBase}\n" +
                $"가장 많이 사용한 overlay: {favoriteOverlay}\n" +
                $"발견한 반응: {discoveries.Count}\n" +
                $"평균 품질 경향: {averageQuality:0}%\n\n" +
                "마법 노트가 마지막 문장을 남깁니다.\n" +
                "\"정답을 외운 것이 아니라, 탑이 알아들을 문법을 만들었다.\"\n\n" +
                $"로그 저장 위치:\n{outputDirectory}";
        }
    }

    public sealed class FloorController
    {
        private readonly List<FloorDefinition> floors;
        public int CurrentFloorIndex { get; private set; }
        public int CurrentFloorNumber => CurrentFloorIndex + 1;
        public int FloorCount => floors.Count;
        public FloorDefinition Current => floors[CurrentFloorIndex];

        public FloorController()
        {
            floors = FloorDefinition.BuildAll();
        }

        public void Load(int index)
        {
            CurrentFloorIndex = Mathf.Clamp(index, 0, floors.Count - 1);
        }
    }

    public sealed class FloorDefinition
    {
        public int number;
        public string title = "";
        public string objective = "";
        public string entryNote = "";
        public string completeNote = "";
        public Color accentColor = Color.white;
        public Color rugColor = new(0.52f, 0.12f, 0.18f);
        public readonly List<WorldStateGoal> goals = new();
        public readonly List<HazardZone> hazards = new();

        public static List<FloorDefinition> BuildAll()
        {
            return new List<FloorDefinition>
            {
                new()
                {
                    number = 1,
                    title = "발착층",
                    objective = "다섯 base 문양으로 시험장의 반응 오브젝트를 깨우세요.",
                    entryNote = "노트: 바닥에 직접 그린 선은 탑이 읽는 말이 된다.",
                    completeNote = "승강 룬이 깨어났습니다. 탑이 다음 층을 열어 줍니다.",
                    accentColor = new Color(0.96f, 0.68f, 0.28f),
                    rugColor = new Color(0.54f, 0.12f, 0.18f),
                    goals =
                    {
                        WorldStateGoal.Base("ember", "불씨", SpellFamily.Fire, new Vector2(-5.5f, 2.6f), new Color(1f, 0.31f, 0.18f), "불씨가 살아나며 오래된 룬을 데웁니다."),
                        WorldStateGoal.Base("puddle", "물웅덩이", SpellFamily.Water, new Vector2(0f, 3.0f), new Color(0.24f, 0.48f, 0.86f), "물길이 맑아지며 바닥 홈을 채웁니다."),
                        WorldStateGoal.Base("vane", "바람개비", SpellFamily.Wind, new Vector2(5.5f, 2.6f), new Color(0.44f, 0.72f, 0.74f), "바람개비가 돌며 승강 룬에 숨을 넣습니다."),
                        WorldStateGoal.Base("pillar", "돌기둥", SpellFamily.Earth, new Vector2(-3.2f, -2.45f), new Color(0.74f, 0.55f, 0.32f), "돌기둥이 제자리를 잡아 시험장을 고정합니다."),
                        WorldStateGoal.Base("vine", "마른 덩굴", SpellFamily.Life, new Vector2(3.2f, -2.45f), new Color(0.35f, 0.86f, 0.42f), "마른 덩굴에 초록 빛이 돌아옵니다.")
                    }
                },
                new()
                {
                    number = 2,
                    title = "반응층",
                    objective = "base seal 위에 6개 overlay를 모두 붙여 반응 벽화를 깨우세요.",
                    entryNote = "노트: base는 동사이고, 장식은 동사의 방식을 바꾼다.",
                    completeNote = "여섯 장식이 모두 벽화에 새겨졌습니다.",
                    accentColor = new Color(0.65f, 0.48f, 0.92f),
                    rugColor = new Color(0.18f, 0.18f, 0.42f),
                    goals =
                    {
                        WorldStateGoal.Overlay("steel", "보강 벽화", OverlayOperator.SteelBrace, new Vector2(-5.8f, 2.7f), new Color(0.78f, 0.82f, 0.86f), "열린 brace가 seal 가장자리를 단단히 붙잡습니다."),
                        WorldStateGoal.Overlay("fork", "번개 벽화", OverlayOperator.ElectricFork, new Vector2(-3.2f, 3.0f), new Color(1f, 0.9f, 0.22f), "번개는 갈라진 길을 좋아합니다."),
                        WorldStateGoal.Overlay("ice", "얼음 벽화", OverlayOperator.IceBar, new Vector2(-0.65f, 3.0f), new Color(0.48f, 0.84f, 1f), "수평 막대가 흐름을 잠깐 멈춥니다."),
                        WorldStateGoal.Overlay("soul", "집중 벽화", OverlayOperator.SoulDot, new Vector2(1.9f, 3.0f), new Color(0.95f, 0.62f, 1f), "작은 점 하나가 주문의 핵심을 잡습니다."),
                        WorldStateGoal.Overlay("void", "절단 벽화", OverlayOperator.VoidCut, new Vector2(4.45f, 3.0f), new Color(0.58f, 0.42f, 0.92f), "절단은 엉킨 흐름을 분리합니다."),
                        WorldStateGoal.Overlay("axis", "축 벽화", OverlayOperator.MartialAxis, new Vector2(6.4f, 2.7f), new Color(1f, 0.58f, 0.34f), "절단 뒤에 축이 섭니다.")
                    }
                },
                new()
                {
                    number = 3,
                    title = "흐름층",
                    objective = "base + overlay 조합으로 끊어진 공중 다리의 네 경로를 연결하세요.",
                    entryNote = "노트: 길은 하나가 아니다. 같은 목표도 여러 문법으로 이어진다.",
                    completeNote = "다리 조각들이 이어져 발밑에 경로가 생겼습니다.",
                    accentColor = new Color(0.48f, 0.8f, 0.92f),
                    rugColor = new Color(0.12f, 0.34f, 0.42f),
                    goals =
                    {
                        WorldStateGoal.Combo("brace_bridge", "보강 지지대", SpellFamily.Earth, OverlayOperator.SteelBrace, new Vector2(-4.6f, 1.8f), new Color(0.74f, 0.55f, 0.32f), "땅과 보강이 떠 있는 돌을 지지합니다."),
                        WorldStateGoal.Combo("axis_bridge", "축 정렬 발판", SpellFamily.Wind, OverlayOperator.MartialAxis, new Vector2(4.6f, 1.8f), new Color(0.44f, 0.72f, 0.74f), "바람과 축이 발판의 방향을 맞춥니다."),
                        WorldStateGoal.Combo("vine_bridge", "덩굴 고리", SpellFamily.Life, OverlayOperator.SoulDot, new Vector2(-3.2f, -2.3f), new Color(0.35f, 0.86f, 0.42f), "생명과 집중이 고리 모양 덩굴을 키웁니다."),
                        WorldStateGoal.Combo("ice_bridge", "얼음 다리", SpellFamily.Water, OverlayOperator.IceBar, new Vector2(3.2f, -2.3f), new Color(0.48f, 0.84f, 1f), "물과 얼음이 잠깐 딛고 설 길을 만듭니다.")
                    }
                },
                new()
                {
                    number = 4,
                    title = "균열층",
                    objective = "위험한 균열을 피해 룬 폭주를 안정화하세요.",
                    entryNote = "노트: 실패는 떨어지는 것이 아니라, 안전한 자리에서 다시 보는 일이다.",
                    completeNote = "균열의 박동이 잦아들고 통로가 안정됩니다.",
                    accentColor = new Color(1f, 0.42f, 0.28f),
                    rugColor = new Color(0.42f, 0.10f, 0.16f),
                    goals =
                    {
                        WorldStateGoal.Base("earth_stable", "흔들림 고정", SpellFamily.Earth, new Vector2(-5.2f, 2.4f), new Color(0.74f, 0.55f, 0.32f), "땅의 주문이 균열의 가장자리를 붙잡습니다."),
                        WorldStateGoal.Overlay("ice_still", "냉각 정지", OverlayOperator.IceBar, new Vector2(-1.7f, 2.9f), new Color(0.48f, 0.84f, 1f), "얼음 막대가 폭주의 열을 낮춥니다."),
                        WorldStateGoal.Overlay("void_split", "오염 분리", OverlayOperator.VoidCut, new Vector2(1.8f, 2.9f), new Color(0.58f, 0.42f, 0.92f), "절단이 위험한 흐름을 끊어 냅니다."),
                        WorldStateGoal.Overlay("fork_ground", "전도 분산", OverlayOperator.ElectricFork, new Vector2(5.2f, 2.4f), new Color(1f, 0.9f, 0.22f), "번개 갈래가 남은 전하를 흩습니다.")
                    },
                    hazards =
                    {
                        new HazardZone("Crack West", new Vector2(-3.1f, -0.4f), 1.1f, new Color(1f, 0.18f, 0.15f)),
                        new HazardZone("Crack Center", new Vector2(0.3f, -0.1f), 1.25f, new Color(1f, 0.18f, 0.15f)),
                        new HazardZone("Crack East", new Vector2(3.7f, -0.55f), 1.05f, new Color(1f, 0.18f, 0.15f))
                    }
                },
                new()
                {
                    number = 5,
                    title = "성좌심",
                    objective = "대형 입학 마법진의 여섯 요구치를 어떤 조합으로든 채우세요.",
                    entryNote = "노트: 마지막 시험은 정답을 묻지 않는다. 탑이 요구하는 상태를 채워라.",
                    completeNote = "대형 마법진이 복구되었습니다. 입학 시험이 끝났습니다.",
                    accentColor = new Color(0.95f, 0.75f, 0.34f),
                    rugColor = new Color(0.18f, 0.16f, 0.32f),
                    goals =
                    {
                        WorldStateGoal.Combo("stability", "안정", SpellFamily.Earth, OverlayOperator.SteelBrace, new Vector2(-4.8f, 2.6f), new Color(0.74f, 0.55f, 0.32f), "안정의 조각이 고정됩니다."),
                        WorldStateGoal.Base("cleanse", "정화", SpellFamily.Water, new Vector2(-1.6f, 3.0f), new Color(0.24f, 0.48f, 0.86f), "정화의 조각이 맑아집니다."),
                        WorldStateGoal.Combo("connection", "연결", SpellFamily.Life, OverlayOperator.SoulDot, new Vector2(1.6f, 3.0f), new Color(0.35f, 0.86f, 0.42f), "연결의 조각이 새싹처럼 이어집니다."),
                        WorldStateGoal.Overlay("cut", "절단", OverlayOperator.VoidCut, new Vector2(4.8f, 2.6f), new Color(0.58f, 0.42f, 0.92f), "절단의 조각이 오염을 분리합니다."),
                        WorldStateGoal.Overlay("focus", "집중", OverlayOperator.SoulDot, new Vector2(-2.2f, -2.5f), new Color(0.95f, 0.62f, 1f), "집중의 조각이 심장을 밝힙니다."),
                        WorldStateGoal.Base("flow", "흐름", SpellFamily.Wind, new Vector2(2.2f, -2.5f), new Color(0.44f, 0.72f, 0.74f), "흐름의 조각이 원을 다시 돌립니다.")
                    }
                }
            };
        }
    }

    public sealed class WorldStateGoal
    {
        public string id;
        public string title;
        public Vector2 position;
        public Color color;
        public PixelSpriteKind kind;
        public SpellFamily? requiredBase;
        public OverlayOperator? requiredOverlay;
        public SpellFamily? comboBase;
        public OverlayOperator? comboOverlay;
        public string discoveryNote;
        public bool completed;
        public float radius = 2.15f;
        public float visualScale = 1f;
        public GameObject body;
        public SpriteRenderer renderer;

        private WorldStateGoal(string id, string title, Vector2 position, Color color, PixelSpriteKind kind, string discoveryNote)
        {
            this.id = id;
            this.title = title;
            this.position = position;
            this.color = color;
            this.kind = kind;
            this.discoveryNote = discoveryNote;
        }

        public static WorldStateGoal Base(string id, string title, SpellFamily family, Vector2 position, Color color, string note)
        {
            return new WorldStateGoal(id, title, position, color, PixelSpriteKind.Target, note)
            {
                requiredBase = family,
                visualScale = 0.9f
            };
        }

        public static WorldStateGoal Overlay(string id, string title, OverlayOperator op, Vector2 position, Color color, string note)
        {
            return new WorldStateGoal(id, title, position, color, PixelSpriteKind.RuneCircle, note)
            {
                requiredOverlay = op,
                radius = 99f,
                visualScale = 0.75f
            };
        }

        public static WorldStateGoal Combo(string id, string title, SpellFamily family, OverlayOperator op, Vector2 position, Color color, string note)
        {
            return new WorldStateGoal(id, title, position, color, PixelSpriteKind.RuneCircle, note)
            {
                comboBase = family,
                comboOverlay = op,
                radius = 99f,
                visualScale = 0.85f
            };
        }

        public bool MatchesBase(SpellFamily family, Vector2 center)
        {
            return requiredBase == family && Vector2.Distance(center, position) <= radius;
        }

        public bool MatchesOverlay(CompiledSeal seal, OverlayOperator op, Vector2 center)
        {
            if (requiredOverlay == op)
            {
                return true;
            }

            return comboBase == seal.baseFamily && comboOverlay == op;
        }

        public WorldStateGoal Clone()
        {
            return new WorldStateGoal(id, title, position, color, kind, discoveryNote)
            {
                requiredBase = requiredBase,
                requiredOverlay = requiredOverlay,
                comboBase = comboBase,
                comboOverlay = comboOverlay,
                radius = radius,
                visualScale = visualScale
            };
        }
    }

    public sealed class HazardZone
    {
        public string title;
        public Vector2 position;
        public float radius;
        public Color color;
        public GameObject body;

        public HazardZone(string title, Vector2 position, float radius, Color color)
        {
            this.title = title;
            this.position = position;
            this.radius = radius;
            this.color = color;
        }

        public HazardZone Clone()
        {
            return new HazardZone(title, position, radius, color);
        }

        public void Tick(float time)
        {
            if (body == null)
            {
                return;
            }

            body.transform.localScale = Vector3.one * radius * (1f + Mathf.Sin(time * 4f) * 0.08f);
        }
    }
}
