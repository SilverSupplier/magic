# Magic Exam Hall

Unity 6.3 LTS 2D top-down HCI mini-game prototype for the Magic Recognizer term project.

## How to Run

1. Open `unity/MagicExamHall` in Unity 6.3 LTS.
2. Open `Assets/Scenes/MagicExamHall.unity`.
3. Press Play.

The scene now contains a real camera, player, five station objects, Canvas UGUI, EventSystem, and an `ExamGameController`. If the scene ever needs to be regenerated, use Unity's menu:

```text
Magic Exam Hall/Rebuild Demo Scene
```

## Controls

- Move: WASD or arrow keys
- Start station: E or Space near the active station
- Draw symbol: hold left mouse button inside the casting panel
- Cast: `마법 시전`
- Retry: `다시 그리기`

## Demo Polish

- Runtime code lives under `Assets/MagicExamHall/Scripts/`.
- Recognition and quality analysis stay scene-independent in `Scripts/Core`.
- UGUI panels cover HUD, station prompt, drawing, result feedback, and survey.
- JRPG-style 32x32 pixel sprites are generated locally at runtime with point filtering.
- The exam hall scene includes tiled stone flooring, carved wall trim, rugs, bookshelves, candelabra, and rune circles under each station.
- Failed attempts escalate assistance: first failure shows one hint, second shows a checklist, third and later attempts show a stronger ghost trace.

## Test Flow

The player completes five base-symbol stations:

1. Fire: closed triangle
2. Water: closed loop
3. Wind: three parallel open lines
4. Earth: closed trapezoid
5. Life: rooted Y

After all five stations, the in-game survey records clarity, fairness, feedback helpfulness, control feeling, immersion, and a free-text comment.

## Logs

Runtime logs are saved under:

```text
Application.persistentDataPath/MagicExamHallLogs/<sessionId>/
```

Each session writes:

- `attempts.jsonl`
- `attempts.csv`
- `survey.jsonl`
- `survey.csv`

## Verification

EditMode tests cover canonical recognition, incomplete/false-positive cases, tempo quality differences, hint escalation, and local log output. PlayMode smoke tests load the scene, verify the five-station UGUI setup, and check first-failure hint behavior.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.com' -batchmode -quit -projectPath 'C:\Users\silve\source\repos\magic\unity\MagicExamHall' -executeMethod MagicExamHall.Editor.MagicExamHallSceneBuilder.BuildAll
& 'C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.com' -batchmode -projectPath 'C:\Users\silve\source\repos\magic\unity\MagicExamHall' -runTests -testPlatform editmode -testResults 'C:\Users\silve\source\repos\magic\unity\MagicExamHall\TestResults.xml'
& 'C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.com' -batchmode -projectPath 'C:\Users\silve\source\repos\magic\unity\MagicExamHall' -runTests -testPlatform playmode -testResults 'C:\Users\silve\source\repos\magic\unity\MagicExamHall\PlayModeTestResults.xml'
```
