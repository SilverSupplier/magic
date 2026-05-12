using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace MagicExamHall
{
    [Serializable]
    public sealed class AttemptLog
    {
        public string sessionId = "";
        public string trialId = "";
        public string targetFamily = "";
        public string recognizedFamily = "";
        public string status = "";
        public float confidence;
        public float closure;
        public float smoothness;
        public float tempo;
        public float stability;
        public float rotationBias;
        public int attemptIndex;
        public int elapsedMs;
        public bool feedbackViewed;
        public bool success;
        public bool hintShown;
        public int assistLevel;
        public bool assisted;
    }

    [Serializable]
    public sealed class SurveyLog
    {
        public string sessionId = "";
        public int clarity;
        public int fairness;
        public int feedbackHelpfulness;
        public int controlFeeling;
        public int immersion;
        public string comment = "";
        public int completedTrials;
        public int totalAttempts;
    }

    public sealed class ExamLogger
    {
        public string OutputDirectory { get; }
        private readonly string attemptsJsonPath;
        private readonly string attemptsCsvPath;
        private readonly string surveyJsonPath;
        private readonly string surveyCsvPath;

        public ExamLogger(string sessionId)
        {
            OutputDirectory = Path.Combine(Application.persistentDataPath, "MagicExamHallLogs", sessionId);
            Directory.CreateDirectory(OutputDirectory);
            attemptsJsonPath = Path.Combine(OutputDirectory, "attempts.jsonl");
            attemptsCsvPath = Path.Combine(OutputDirectory, "attempts.csv");
            surveyJsonPath = Path.Combine(OutputDirectory, "survey.jsonl");
            surveyCsvPath = Path.Combine(OutputDirectory, "survey.csv");
            EnsureAttemptHeader();
            EnsureSurveyHeader();
        }

        public void LogAttempt(AttemptLog log)
        {
            File.AppendAllText(attemptsJsonPath, JsonUtility.ToJson(log) + Environment.NewLine, Encoding.UTF8);
            File.AppendAllText(attemptsCsvPath, string.Join(",",
                Csv(log.sessionId),
                Csv(log.trialId),
                Csv(log.targetFamily),
                Csv(log.recognizedFamily),
                Csv(log.status),
                Float(log.confidence),
                Float(log.closure),
                Float(log.smoothness),
                Float(log.tempo),
                Float(log.stability),
                Float(log.rotationBias),
                log.attemptIndex,
                log.elapsedMs,
                Bool(log.feedbackViewed),
                Bool(log.success),
                Bool(log.hintShown),
                log.assistLevel,
                Bool(log.assisted)) + Environment.NewLine, Encoding.UTF8);
        }

        public void LogSurvey(SurveyLog log)
        {
            File.AppendAllText(surveyJsonPath, JsonUtility.ToJson(log) + Environment.NewLine, Encoding.UTF8);
            File.AppendAllText(surveyCsvPath, string.Join(",",
                Csv(log.sessionId),
                log.clarity,
                log.fairness,
                log.feedbackHelpfulness,
                log.controlFeeling,
                log.immersion,
                Csv(log.comment),
                log.completedTrials,
                log.totalAttempts) + Environment.NewLine, Encoding.UTF8);
        }

        private void EnsureAttemptHeader()
        {
            if (!File.Exists(attemptsCsvPath))
            {
                File.WriteAllText(
                    attemptsCsvPath,
                    "sessionId,trialId,targetFamily,recognizedFamily,status,confidence,closure,smoothness,tempo,stability,rotationBias,attemptIndex,elapsedMs,feedbackViewed,success,hintShown,assistLevel,assisted" + Environment.NewLine,
                    Encoding.UTF8);
            }
        }

        private void EnsureSurveyHeader()
        {
            if (!File.Exists(surveyCsvPath))
            {
                File.WriteAllText(surveyCsvPath, "sessionId,clarity,fairness,feedbackHelpfulness,controlFeeling,immersion,comment,completedTrials,totalAttempts" + Environment.NewLine, Encoding.UTF8);
            }
        }

        private static string Csv(string value)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string Float(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
