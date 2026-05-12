using System;
using System.Collections.Generic;

namespace MagicExamHall
{
    public enum AssistLevel
    {
        None = 0,
        ReasonHint = 1,
        Checklist = 2,
        GhostTrace = 3
    }

    [Serializable]
    public sealed class HintState
    {
        public SpellFamily family;
        public int failureCount;
        public AssistLevel currentLevel;
        public bool hintShown;
        public bool assisted;
        public string title = "";
        public string body = "";

        public int AssistLevelNumber => (int)currentLevel;
    }

    public static class HintAssistance
    {
        public static HintState PreviewFor(SpellFamily family, int priorFailures, SpellResult result = null)
        {
            var level = (AssistLevel)Math.Min(Math.Max(priorFailures, 0), 3);
            return CreateState(family, priorFailures, level, priorFailures > 0, result);
        }

        public static HintState ForAttempt(SpellFamily family, int priorFailures, bool success, SpellResult result)
        {
            var level = ResolveLevel(priorFailures, success);
            return CreateState(family, priorFailures, level, priorFailures > 0, result);
        }

        public static AssistLevel ResolveLevel(int priorFailures, bool success)
        {
            if (success)
            {
                if (priorFailures <= 0)
                {
                    return AssistLevel.None;
                }

                return (AssistLevel)Math.Min(priorFailures, 3);
            }

            return (AssistLevel)Math.Min(Math.Max(priorFailures + 1, 1), 3);
        }

        public static IReadOnlyList<string> ChecklistFor(SpellFamily family)
        {
            return family switch
            {
                SpellFamily.Fire => new[] { "꼭짓점 3개가 보이게 그리기", "마지막 점을 시작점 근처로 닫기", "삼각형을 위쪽으로 세우기" },
                SpellFamily.Water => new[] { "한 획으로 둥글게 이어 그리기", "끝점을 시작점 근처로 닫기", "찌그러짐보다 큰 원형 흐름 유지" },
                SpellFamily.Wind => new[] { "짧은 선 3개를 따로 그리기", "세 선을 비슷한 방향으로 놓기", "너무 닫힌 도형처럼 만들지 않기" },
                SpellFamily.Earth => new[] { "사다리꼴 네 모서리 만들기", "아래 변을 더 넓게 그리기", "끝점을 닫아 안정감 만들기" },
                SpellFamily.Life => new[] { "아래 줄기에서 위로 올라가기", "상단에서 좌우 가지 만들기", "닫힌 도형보다 열린 Y 형태 유지" },
                _ => Array.Empty<string>()
            };
        }

        private static string TitleFor(AssistLevel level)
        {
            return level switch
            {
                AssistLevel.None => "자율 입력",
                AssistLevel.ReasonHint => "짧은 힌트",
                AssistLevel.Checklist => "체크리스트",
                AssistLevel.GhostTrace => "강한 보조선",
                _ => "힌트"
            };
        }

        private static string BodyFor(SpellFamily family, AssistLevel level, SpellResult result)
        {
            if (level == AssistLevel.None)
            {
                return "안내선을 참고해 먼저 스스로 시도해 보세요.";
            }

            if (level == AssistLevel.ReasonHint)
            {
                return result == null ? "결과 이유를 보고 한 가지만 고쳐 다시 시도하세요." : result.nextHint;
            }

            if (level == AssistLevel.Checklist)
            {
                return string.Join("\n", ChecklistFor(family));
            }

            return "밝아진 보조선을 따라 큰 실루엣을 먼저 맞춘 뒤 다시 시전하세요.";
        }

        private static HintState CreateState(SpellFamily family, int priorFailures, AssistLevel level, bool assisted, SpellResult result)
        {
            return new HintState
            {
                family = family,
                failureCount = Math.Max(priorFailures, 0),
                currentLevel = level,
                hintShown = level != AssistLevel.None,
                assisted = assisted,
                title = TitleFor(level),
                body = BodyFor(family, level, result)
            };
        }
    }
}
