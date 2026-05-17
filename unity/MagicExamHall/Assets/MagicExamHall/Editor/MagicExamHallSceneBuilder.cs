using MagicExamHall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MagicExamHall.Editor
{
    public static class MagicExamHallSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MagicExamHall.unity";
        private const string PrefabFolder = "Assets/MagicExamHall/Prefabs";
        private const string ResourcesFolder = "Assets/MagicExamHall/Resources";
        private const string MaterialFolder = "Assets/MagicExamHall/Resources/MagicExamHallMaterials";

        [MenuItem("Magic Exam Hall/Rebuild Demo Scene")]
        public static void BuildAll()
        {
            EnsureFolders();
            EnsureMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MagicExamHall";

            var camera = CreateCamera();
            CreateFloor();
            var player = CreatePlayer();
            var canvas = CreateCanvas();
            CreateEventSystem();
            CreateController(camera, player.transform, canvas);
            SavePrefabs(player);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Magic Exam Hall scene rebuilt at {ScenePath}");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            if (!AssetDatabase.IsValidFolder("Assets/MagicExamHall"))
            {
                AssetDatabase.CreateFolder("Assets", "MagicExamHall");
            }

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/MagicExamHall", "Prefabs");
            }

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets/MagicExamHall", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder(ResourcesFolder, "MagicExamHallMaterials");
            }
        }

        private static void EnsureMaterials()
        {
            CreateOrUpdateMaterial($"{MaterialFolder}/PixelSpriteDefault.mat", "Sprites/Default");
            CreateOrUpdateMaterial($"{MaterialFolder}/PixelUIDefault.mat", "UI/Default");
        }

        private static void CreateOrUpdateMaterial(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"Could not find shader {shaderName}; material {path} was not generated.");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.2f;
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.11f);
            return camera;
        }

        private static void CreateFloor()
        {
            CreatePixelObject("Stone Tile Floor", Vector2.zero, Vector3.one, PixelSpriteKind.FloorTile,
                new Color(0.16f, 0.18f, 0.23f), new Color(0.10f, 0.12f, 0.16f), -7, true, new Vector2(16.4f, 10f));
            CreatePixelObject("North Carved Wall", new Vector2(0f, 4.95f), Vector3.one, PixelSpriteKind.WallTrim,
                new Color(0.22f, 0.20f, 0.27f), new Color(0.63f, 0.50f, 0.23f), -4, true, new Vector2(16.4f, 1.15f));
            CreatePixelObject("South Carved Wall", new Vector2(0f, -4.95f), Vector3.one, PixelSpriteKind.WallTrim,
                new Color(0.18f, 0.17f, 0.22f), new Color(0.50f, 0.40f, 0.20f), -4, true, new Vector2(16.4f, 0.8f));
            CreatePixelObject("Center Runner", new Vector2(0f, 0.15f), Vector3.one, PixelSpriteKind.Rug,
                new Color(0.55f, 0.10f, 0.17f), new Color(0.95f, 0.69f, 0.26f), -5, true, new Vector2(2.2f, 7.6f));
            CreatePixelObject("West Runner", new Vector2(-4.25f, 0f), Vector3.one, PixelSpriteKind.Rug,
                new Color(0.14f, 0.34f, 0.44f), new Color(0.80f, 0.65f, 0.32f), -5, true, new Vector2(1.45f, 4.8f));
            CreatePixelObject("East Runner", new Vector2(4.25f, 0f), Vector3.one, PixelSpriteKind.Rug,
                new Color(0.14f, 0.34f, 0.44f), new Color(0.80f, 0.65f, 0.32f), -5, true, new Vector2(1.45f, 4.8f));

            CreateProp("West Bookcase", new Vector2(-7.25f, 1.25f), new Vector3(1.25f, 1.25f, 1f), PixelSpriteKind.Bookshelf,
                new Color(0.42f, 0.23f, 0.12f), new Color(0.42f, 0.80f, 0.88f), -1);
            CreateProp("East Bookcase", new Vector2(7.25f, 1.25f), new Vector3(1.25f, 1.25f, 1f), PixelSpriteKind.Bookshelf,
                new Color(0.42f, 0.23f, 0.12f), new Color(0.68f, 0.36f, 0.86f), -1);
            CreateProp("Northwest Candelabra", new Vector2(-6.85f, 3.65f), Vector3.one * 0.9f, PixelSpriteKind.Candle,
                new Color(0.63f, 0.57f, 0.44f), new Color(1f, 0.56f, 0.15f), 2);
            CreateProp("Northeast Candelabra", new Vector2(6.85f, 3.65f), Vector3.one * 0.9f, PixelSpriteKind.Candle,
                new Color(0.63f, 0.57f, 0.44f), new Color(1f, 0.56f, 0.15f), 2);
        }

        private static GameObject CreateProp(string name, Vector2 position, Vector3 scale, PixelSpriteKind kind, Color primary, Color secondary, int sortingOrder)
        {
            return CreatePixelObject(name, position, scale, kind, primary, secondary, sortingOrder, false, Vector2.one);
        }

        private static GameObject CreatePixelObject(string name, Vector2 position, Vector3 scale, PixelSpriteKind kind, Color primary, Color secondary, int sortingOrder, bool tiled, Vector2 tiledSize)
        {
            var body = new GameObject(name);
            body.transform.position = position;
            body.transform.localScale = scale;
            body.AddComponent<SpriteRenderer>();
            var sprite = body.AddComponent<PixelSpriteView>();
            sprite.kind = kind;
            sprite.primary = primary;
            sprite.secondary = secondary;
            sprite.sortingOrder = sortingOrder;
            sprite.tiled = tiled;
            sprite.tiledSize = tiledSize;
            return body;
        }

        private static GameObject CreatePlayer()
        {
            var player = new GameObject("Apprentice");
            player.transform.position = new Vector3(0f, -4.1f, 0f);
            player.transform.localScale = Vector3.one * 0.78f;
            player.AddComponent<SpriteRenderer>();
            var sprite = player.AddComponent<PixelSpriteView>();
            sprite.kind = PixelSpriteKind.Player;
            sprite.primary = new Color(0.95f, 0.92f, 0.78f);
            sprite.secondary = new Color(0.28f, 0.62f, 0.96f);
            sprite.sortingOrder = 4;
            return player;
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("Exam Canvas");
            canvasObject.AddComponent<RectTransform>();
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void CreateEventSystem()
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static void CreateController(Camera camera, Transform player, Canvas canvas)
        {
            var controllerObject = new GameObject("Exam Game Controller");
            var controller = controllerObject.AddComponent<ExamGameController>();
            controller.mainCamera = camera;
            controller.player = player;
            controller.canvas = canvas;
            var drawing = controllerObject.AddComponent<WorldDrawingController>();
            drawing.mainCamera = camera;
            drawing.ApplyPlayableDefaults();
        }

        private static void SavePrefabs(GameObject player)
        {
            PrefabUtility.SaveAsPrefabAsset(player, $"{PrefabFolder}/Apprentice.prefab");
        }
    }
}
