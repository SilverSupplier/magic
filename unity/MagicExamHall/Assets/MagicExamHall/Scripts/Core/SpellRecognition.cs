using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MagicExamHall
{
    public enum SpellFamily
    {
        Wind,
        Earth,
        Fire,
        Water,
        Life
    }

    public enum RecognitionStatus
    {
        Recognized,
        Ambiguous,
        Incomplete,
        Invalid
    }

    [Serializable]
    public struct StrokeSample
    {
        public Vector2 position;
        public float time;

        public StrokeSample(Vector2 position, float time)
        {
            this.position = position;
            this.time = time;
        }
    }

    [Serializable]
    public struct QualityVector
    {
        public float closure;
        public float smoothness;
        public float tempo;
        public float stability;
        public float rotationBias;

        public float Average()
        {
            return (closure + smoothness + tempo + stability + (1f - rotationBias)) / 5f;
        }
    }

    [Serializable]
    public sealed class SpellResult
    {
        public RecognitionStatus status;
        public SpellFamily? recognizedFamily;
        public SpellFamily targetFamily;
        public float confidence;
        public QualityVector quality;
        public string feedbackReason;
        public string nextHint;
        public bool success;

        public string RecognizedFamilyText => recognizedFamily.HasValue ? SpellLabels.English(recognizedFamily.Value) : "none";
    }

    internal sealed class SpellTemplate
    {
        public SpellFamily family;
        public int minStrokes;
        public int maxStrokes;
        public List<List<Vector2>> strokes = new();
        public List<Vector2> normalizedCloud = new();
    }

    public static class GestureRecognizer
    {
        private const int CloudPointCount = 64;
        private static readonly IReadOnlyList<SpellTemplate> Templates = BuildTemplates();

        public static SpellResult Recognize(IReadOnlyList<IReadOnlyList<StrokeSample>> rawStrokes, SpellFamily targetFamily)
        {
            var drawable = rawStrokes
                .Select(stroke => stroke.Where(point => IsFinite(point.position)).ToList())
                .Where(stroke => stroke.Count >= 2)
                .ToList();

            if (drawable.Count == 0)
            {
                return new SpellResult
                {
                    status = RecognitionStatus.Invalid,
                    targetFamily = targetFamily,
                    confidence = 0f,
                    quality = new QualityVector(),
                    feedbackReason = "No stroke was captured.",
                    nextHint = "Hold the mouse button and draw inside the casting panel."
                };
            }

            var quality = QualityAnalyzer.Calculate(drawable);
            var normalized = NormalizeStrokes(drawable.Select(stroke => stroke.Select(sample => sample.position).ToList()).ToList());
            var scored = Templates
                .Select(template => ScoreTemplate(template, normalized, drawable, quality))
                .OrderByDescending(candidate => candidate.score)
                .ToList();

            var top = scored[0];
            var second = scored.Count > 1 ? scored[1] : top;
            var margin = top.score - second.score;
            var status = ResolveStatus(top, margin, quality, drawable.Count);
            if (targetFamily == SpellFamily.Wind && drawable.Count < 3)
            {
                status = RecognitionStatus.Incomplete;
            }
            else if (RequiresClosure(targetFamily) && quality.closure < 0.62f)
            {
                status = RecognitionStatus.Incomplete;
            }
            SpellFamily? recognized = status == RecognitionStatus.Recognized ? top.template.family : null;
            var success = recognized.HasValue && recognized.Value == targetFamily;

            return new SpellResult
            {
                status = status,
                targetFamily = targetFamily,
                recognizedFamily = recognized,
                confidence = Mathf.Clamp01(top.score),
                quality = quality,
                success = success,
                feedbackReason = BuildReason(targetFamily, top, second, status, quality),
                nextHint = BuildHint(targetFamily, top, status, quality, drawable.Count)
            };
        }

        public static List<List<StrokeSample>> CreateCanonicalSamples(SpellFamily family, float scale = 420f, float timeStep = 0.04f)
        {
            var template = Templates.First(item => item.family == family);
            var result = new List<List<StrokeSample>>();
            var time = 0f;

            foreach (var stroke in template.strokes)
            {
                var mapped = new List<StrokeSample>();
                foreach (var point in stroke)
                {
                    mapped.Add(new StrokeSample(new Vector2(point.x * scale + scale * 0.5f, point.y * scale + scale * 0.5f), time));
                    time += timeStep;
                }
                result.Add(mapped);
                time += timeStep * 4f;
            }

            return result;
        }

        private static RecognitionStatus ResolveStatus(
            (SpellTemplate template, float score, float distance) top,
            float margin,
            QualityVector quality,
            int strokeCount)
        {
            if (top.template.family == SpellFamily.Wind && strokeCount < 3 && top.score >= 0.42f)
            {
                return RecognitionStatus.Incomplete;
            }

            if (RequiresClosure(top.template.family) && quality.closure < 0.62f && top.score >= 0.42f)
            {
                return RecognitionStatus.Incomplete;
            }

            if (top.score >= 0.70f && margin >= 0.08f)
            {
                return RecognitionStatus.Recognized;
            }

            if (top.score >= 0.54f)
            {
                return RecognitionStatus.Ambiguous;
            }

            return RecognitionStatus.Invalid;
        }

        private static (SpellTemplate template, float score, float distance) ScoreTemplate(
            SpellTemplate template,
            NormalizedGesture normalized,
            List<List<StrokeSample>> strokes,
            QualityVector quality)
        {
            var distance = PointCloudDistance(normalized.cloud, template.normalizedCloud);
            var templateScore = Mathf.Clamp01(1f - distance / 0.66f);
            var strokeScore = RangeScore(strokes.Count, template.minStrokes, template.maxStrokes);
            var openness = 1f - quality.closure;
            var cornerCount = CountDominantCorners(strokes);
            var corners = ExpectedCornerScore(cornerCount, ExpectedCorners(template.family));
            var circularity = EstimateCircularity(normalized.cloud);
            var parallel = EstimateParallelism(strokes);
            var fill = EstimateFillRatio(strokes);
            var score = templateScore;

            switch (template.family)
            {
                case SpellFamily.Wind:
                    score = templateScore * 0.42f + parallel * 0.30f + strokeScore * 0.20f + openness * 0.08f;
                    if (strokes.Count < 3)
                    {
                        score *= 0.55f;
                    }
                    break;
                case SpellFamily.Earth:
                    score = templateScore * 0.40f + quality.closure * 0.23f + corners * 0.20f + Closeness(fill, 0.68f, 0.24f) * 0.09f + strokeScore * 0.08f;
                    break;
                case SpellFamily.Fire:
                    score = templateScore * 0.42f + quality.closure * 0.24f + corners * 0.21f + Closeness(fill, 0.5f, 0.18f) * 0.07f + strokeScore * 0.06f;
                    break;
                case SpellFamily.Water:
                    score = templateScore * 0.48f + quality.closure * 0.19f + circularity * 0.22f + quality.smoothness * 0.11f;
                    break;
                case SpellFamily.Life:
                    score = templateScore * 0.36f + ExpectedEndpointScore(strokes) * 0.28f + openness * 0.18f + strokeScore * 0.10f + Closeness(fill, 0.16f, 0.20f) * 0.08f;
                    break;
            }

            return (template, Mathf.Clamp01(score), distance);
        }

        private static string BuildReason(
            SpellFamily target,
            (SpellTemplate template, float score, float distance) top,
            (SpellTemplate template, float score, float distance) second,
            RecognitionStatus status,
            QualityVector quality)
        {
            if (RequiresClosure(target) && quality.closure < 0.62f)
            {
                return "닫힌 문양의 끝점이 충분히 맞닿지 않아 미완성으로 남았습니다.";
            }

            if (target == SpellFamily.Wind && status == RecognitionStatus.Incomplete)
            {
                return "바람 문양은 평행한 선 3개가 필요합니다.";
            }

            if (status == RecognitionStatus.Recognized && top.template.family == target)
            {
                return $"{SpellLabels.Korean(target)} 문양으로 안정적으로 읽혔습니다.";
            }

            if (status == RecognitionStatus.Recognized)
            {
                return $"{SpellLabels.Korean(top.template.family)} 문양에 더 가까워 목표 문양과 다르게 처리되었습니다.";
            }

            if (status == RecognitionStatus.Incomplete && RequiresClosure(top.template.family) && quality.closure < 0.62f)
            {
                return "닫힌 문양의 끝점이 충분히 맞닿지 않아 미완성으로 남았습니다.";
            }

            if (status == RecognitionStatus.Incomplete && top.template.family == SpellFamily.Wind)
            {
                return "바람 문양은 평행한 선 3개가 필요합니다.";
            }

            if (status == RecognitionStatus.Ambiguous)
            {
                return $"{SpellLabels.Korean(top.template.family)}와 {SpellLabels.Korean(second.template.family)} 점수가 가까워 확정하지 않았습니다.";
            }

            return "기준 문양과의 거리가 커서 마법을 확정하지 않았습니다.";
        }

        private static string BuildHint(
            SpellFamily target,
            (SpellTemplate template, float score, float distance) top,
            RecognitionStatus status,
            QualityVector quality,
            int strokeCount)
        {
            if (target == SpellFamily.Wind && strokeCount < 3)
            {
                return "짧은 평행선을 3획으로 나누어 그려 보세요.";
            }

            if (RequiresClosure(target) && quality.closure < 0.72f)
            {
                return "마지막 점을 시작점 근처로 가져와 닫힌 모양을 만들어 보세요.";
            }

            if (quality.rotationBias > 0.55f)
            {
                return "문양을 조금 더 정면 방향으로 세워 그리면 안정도가 올라갑니다.";
            }

            if (status == RecognitionStatus.Recognized && top.template.family == target)
            {
                return "좋습니다. 같은 문양을 유지하면 다음 시험으로 넘어갈 수 있습니다.";
            }

            return $"{SpellLabels.Korean(target)}의 큰 실루엣을 먼저 맞추고 세부 속도는 나중에 조정하세요.";
        }

        private static IReadOnlyList<SpellTemplate> BuildTemplates()
        {
            var templates = new List<SpellTemplate>
            {
                new()
                {
                    family = SpellFamily.Wind,
                    minStrokes = 3,
                    maxStrokes = 3,
                    strokes = new List<List<Vector2>>
                    {
                        Line(-0.45f, -0.20f, 0.45f, -0.23f, 16),
                        Line(-0.45f, 0.00f, 0.45f, -0.03f, 16),
                        Line(-0.45f, 0.20f, 0.45f, 0.17f, 16)
                    }
                },
                new()
                {
                    family = SpellFamily.Earth,
                    minStrokes = 1,
                    maxStrokes = 2,
                    strokes = new List<List<Vector2>>
                    {
                        Poly(new Vector2(-0.25f, -0.38f), new Vector2(0.25f, -0.38f), new Vector2(0.46f, 0.34f), new Vector2(-0.46f, 0.34f), new Vector2(-0.25f, -0.38f))
                    }
                },
                new()
                {
                    family = SpellFamily.Fire,
                    minStrokes = 1,
                    maxStrokes = 2,
                    strokes = new List<List<Vector2>>
                    {
                        Poly(new Vector2(0f, -0.46f), new Vector2(0.46f, 0.42f), new Vector2(-0.46f, 0.42f), new Vector2(0f, -0.46f))
                    }
                },
                new()
                {
                    family = SpellFamily.Water,
                    minStrokes = 1,
                    maxStrokes = 1,
                    strokes = new List<List<Vector2>> { Ellipse(0.44f, 0.39f, 72) }
                },
                new()
                {
                    family = SpellFamily.Life,
                    minStrokes = 1,
                    maxStrokes = 3,
                    strokes = new List<List<Vector2>>
                    {
                        Poly(new Vector2(0f, 0.46f), new Vector2(0f, 0.08f), new Vector2(-0.34f, -0.30f)),
                        Line(0f, 0.08f, 0.34f, -0.30f, 12)
                    }
                }
            };

            foreach (var template in templates)
            {
                template.normalizedCloud = NormalizeStrokes(template.strokes).cloud;
            }

            return templates;
        }

        private static bool RequiresClosure(SpellFamily family)
        {
            return family == SpellFamily.Earth || family == SpellFamily.Fire || family == SpellFamily.Water;
        }

        private static int ExpectedCorners(SpellFamily family)
        {
            return family switch
            {
                SpellFamily.Fire => 3,
                SpellFamily.Earth => 4,
                SpellFamily.Life => 4,
                _ => 1
            };
        }

        private static NormalizedGesture NormalizeStrokes(IReadOnlyList<IReadOnlyList<Vector2>> strokes)
        {
            var points = strokes.SelectMany(stroke => stroke).ToList();
            if (points.Count == 0)
            {
                return new NormalizedGesture(new List<Vector2>(), 1f, Vector2.zero);
            }

            var min = new Vector2(points.Min(point => point.x), points.Min(point => point.y));
            var max = new Vector2(points.Max(point => point.x), points.Max(point => point.y));
            var center = (min + max) * 0.5f;
            var scale = Mathf.Max(max.x - min.x, max.y - min.y, 0.001f);
            var normalizedStrokes = strokes
                .Select(stroke => stroke.Select(point => (point - center) / scale).ToList())
                .ToList();
            var cloud = Resample(normalizedStrokes.SelectMany(stroke => stroke).ToList(), CloudPointCount);
            return new NormalizedGesture(cloud, scale, center);
        }

        private static List<Vector2> Resample(IReadOnlyList<Vector2> points, int count)
        {
            if (points.Count == 0)
            {
                return new List<Vector2>();
            }

            if (points.Count == 1)
            {
                return Enumerable.Repeat(points[0], count).ToList();
            }

            var total = PathLength(points);
            var interval = total / Mathf.Max(count - 1, 1);
            var output = new List<Vector2> { points[0] };
            var distanceSinceLast = 0f;
            var previous = points[0];

            for (var index = 1; index < points.Count; index++)
            {
                var current = points[index];
                var segment = Vector2.Distance(previous, current);
                if (segment <= 0.0001f)
                {
                    continue;
                }

                while (distanceSinceLast + segment >= interval && output.Count < count)
                {
                    var t = (interval - distanceSinceLast) / segment;
                    var inserted = Vector2.Lerp(previous, current, t);
                    output.Add(inserted);
                    segment -= interval - distanceSinceLast;
                    previous = inserted;
                    distanceSinceLast = 0f;
                }

                distanceSinceLast += segment;
                previous = current;
            }

            while (output.Count < count)
            {
                output.Add(points[^1]);
            }

            return output;
        }

        private static float PointCloudDistance(IReadOnlyList<Vector2> left, IReadOnlyList<Vector2> right)
        {
            if (left.Count == 0 || right.Count == 0)
            {
                return 1f;
            }

            var count = Mathf.Min(left.Count, right.Count);
            var forward = 0f;
            var reverse = 0f;

            for (var index = 0; index < count; index++)
            {
                forward += Vector2.Distance(left[index], right[index]);
                reverse += Vector2.Distance(left[index], right[count - 1 - index]);
            }

            return Mathf.Min(forward, reverse) / count;
        }

        private static float EstimateParallelism(List<List<StrokeSample>> strokes)
        {
            var angles = strokes
                .Where(stroke => stroke.Count >= 2)
                .Select(stroke => Mathf.Atan2(stroke[^1].position.y - stroke[0].position.y, stroke[^1].position.x - stroke[0].position.x))
                .ToList();

            if (angles.Count == 0)
            {
                return 0f;
            }

            var x = angles.Sum(angle => Mathf.Cos(angle * 2f));
            var y = angles.Sum(angle => Mathf.Sin(angle * 2f));
            var mean = Mathf.Atan2(y, x) * 0.5f;
            var deviation = angles.Average(angle => Mathf.Abs(NormalizeHalfPi(angle - mean)));
            return Mathf.Clamp01(1f - deviation / (Mathf.PI / 6f));
        }

        private static int CountDominantCorners(List<List<StrokeSample>> strokes)
        {
            var dominant = strokes.OrderByDescending(stroke => StrokePathLength(stroke)).FirstOrDefault();
            if (dominant == null || dominant.Count < 3)
            {
                return 0;
            }

            var corners = 0;
            for (var index = 1; index < dominant.Count - 1; index++)
            {
                var a = (dominant[index].position - dominant[index - 1].position).normalized;
                var b = (dominant[index + 1].position - dominant[index].position).normalized;
                if (Vector2.Angle(a, b) > 38f)
                {
                    corners++;
                }
            }

            return Mathf.Clamp(corners, 0, 6);
        }

        private static float ExpectedCornerScore(int actual, int expected)
        {
            return Mathf.Clamp01(1f - Mathf.Abs(actual - expected) / Mathf.Max(expected, 1f));
        }

        private static float ExpectedEndpointScore(List<List<StrokeSample>> strokes)
        {
            var endpoints = strokes.SelectMany(stroke => new[] { stroke[0].position, stroke[^1].position }).ToList();
            if (endpoints.Count < 3)
            {
                return 0.35f;
            }

            return Mathf.Clamp01(1f - Mathf.Abs(endpoints.Count - 4) / 5f);
        }

        private static float EstimateFillRatio(List<List<StrokeSample>> strokes)
        {
            var points = strokes.SelectMany(stroke => stroke.Select(sample => sample.position)).ToList();
            if (points.Count < 3)
            {
                return 0f;
            }

            var minX = points.Min(point => point.x);
            var maxX = points.Max(point => point.x);
            var minY = points.Min(point => point.y);
            var maxY = points.Max(point => point.y);
            var boxArea = Mathf.Max((maxX - minX) * (maxY - minY), 1f);
            var dominant = strokes.OrderByDescending(stroke => StrokePathLength(stroke)).First();
            var area = Mathf.Abs(PolygonArea(dominant.Select(sample => sample.position).ToList()));
            return Mathf.Clamp01(area / boxArea);
        }

        private static float EstimateCircularity(IReadOnlyList<Vector2> cloud)
        {
            if (cloud.Count == 0)
            {
                return 0f;
            }

            var radii = cloud.Select(point => point.magnitude).ToList();
            var mean = radii.Average();
            var variance = radii.Average(radius => Mathf.Pow(radius - mean, 2f));
            return Mathf.Clamp01(1f - Mathf.Sqrt(variance) / Mathf.Max(mean * 0.45f, 0.0001f));
        }

        private static float RangeScore(int value, int min, int max)
        {
            if (value >= min && value <= max)
            {
                return 1f;
            }

            var distance = value < min ? min - value : value - max;
            return Mathf.Clamp01(1f - distance * 0.35f);
        }

        private static float Closeness(float value, float expected, float tolerance)
        {
            return Mathf.Clamp01(1f - Mathf.Abs(value - expected) / Mathf.Max(tolerance, 0.001f));
        }

        private static float StrokePathLength(IReadOnlyList<StrokeSample> stroke)
        {
            var points = stroke.Select(sample => sample.position).ToList();
            return PathLength(points);
        }

        private static float PathLength(IReadOnlyList<Vector2> points)
        {
            var length = 0f;
            for (var index = 1; index < points.Count; index++)
            {
                length += Vector2.Distance(points[index - 1], points[index]);
            }

            return length;
        }

        private static float PolygonArea(IReadOnlyList<Vector2> points)
        {
            if (points.Count < 3)
            {
                return 0f;
            }

            var sum = 0f;
            for (var index = 0; index < points.Count; index++)
            {
                var current = points[index];
                var next = points[(index + 1) % points.Count];
                sum += current.x * next.y - next.x * current.y;
            }

            return sum * 0.5f;
        }

        private static float NormalizeHalfPi(float angle)
        {
            while (angle > Mathf.PI / 2f)
            {
                angle -= Mathf.PI;
            }

            while (angle < -Mathf.PI / 2f)
            {
                angle += Mathf.PI;
            }

            return angle;
        }

        private static List<Vector2> Line(float x1, float y1, float x2, float y2, int count)
        {
            return Enumerable.Range(0, count)
                .Select(index => Vector2.Lerp(new Vector2(x1, y1), new Vector2(x2, y2), index / Mathf.Max(count - 1f, 1f)))
                .ToList();
        }

        private static List<Vector2> Poly(params Vector2[] anchors)
        {
            var points = new List<Vector2>();
            for (var index = 1; index < anchors.Length; index++)
            {
                var line = Line(anchors[index - 1].x, anchors[index - 1].y, anchors[index].x, anchors[index].y, 16);
                if (points.Count > 0)
                {
                    line.RemoveAt(0);
                }

                points.AddRange(line);
            }

            return points;
        }

        private static List<Vector2> Ellipse(float radiusX, float radiusY, int count)
        {
            return Enumerable.Range(0, count)
                .Select(index =>
                {
                    var angle = (index / (float)(count - 1)) * Mathf.PI * 2f;
                    return new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
                })
                .ToList();
        }

        private static bool IsFinite(Vector2 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y);
        }

        private readonly struct NormalizedGesture
        {
            public readonly List<Vector2> cloud;
            public readonly float scale;
            public readonly Vector2 center;

            public NormalizedGesture(List<Vector2> cloud, float scale, Vector2 center)
            {
                this.cloud = cloud;
                this.scale = scale;
                this.center = center;
            }
        }
    }

    public static class QualityAnalyzer
    {
        public static QualityVector Calculate(IReadOnlyList<IReadOnlyList<StrokeSample>> strokes)
        {
            var all = strokes.SelectMany(stroke => stroke).ToList();
            if (all.Count == 0)
            {
                return new QualityVector();
            }

            var min = new Vector2(all.Min(sample => sample.position.x), all.Min(sample => sample.position.y));
            var max = new Vector2(all.Max(sample => sample.position.x), all.Max(sample => sample.position.y));
            var diagonal = Mathf.Max(Vector2.Distance(min, max), 1f);
            var longest = strokes.OrderByDescending(PathLength).First();
            var gap = Vector2.Distance(longest[0].position, longest[^1].position);
            var duration = Mathf.Max(all.Max(sample => sample.time) - all.Min(sample => sample.time), 0.001f);

            return new QualityVector
            {
                closure = Mathf.Clamp01(1f - gap / (diagonal * 0.36f)),
                smoothness = CalculateSmoothness(strokes, diagonal),
                tempo = Mathf.Clamp01(1f - duration / 1.55f),
                stability = CalculateStability(strokes),
                rotationBias = CalculateRotationBias(all.Select(sample => sample.position).ToList())
            };
        }

        private static float CalculateSmoothness(IReadOnlyList<IReadOnlyList<StrokeSample>> strokes, float diagonal)
        {
            var penalties = new List<float>();
            foreach (var stroke in strokes.Where(stroke => stroke.Count >= 3))
            {
                for (var index = 1; index < stroke.Count - 1; index++)
                {
                    var a = Mathf.Atan2(stroke[index].position.y - stroke[index - 1].position.y, stroke[index].position.x - stroke[index - 1].position.x);
                    var b = Mathf.Atan2(stroke[index + 1].position.y - stroke[index].position.y, stroke[index + 1].position.x - stroke[index].position.x);
                    penalties.Add(Mathf.Abs(NormalizeHalfPi(b - a)) / Mathf.PI);
                }
            }

            if (penalties.Count == 0)
            {
                return 0.5f;
            }

            return Mathf.Clamp01(1f - penalties.Average() - diagonal * 0.0005f);
        }

        private static float CalculateStability(IReadOnlyList<IReadOnlyList<StrokeSample>> strokes)
        {
            var speeds = new List<float>();
            var pauses = 0;
            var segments = 0;

            foreach (var stroke in strokes)
            {
                for (var index = 1; index < stroke.Count; index++)
                {
                    var dt = Mathf.Max(stroke[index].time - stroke[index - 1].time, 0.001f);
                    speeds.Add(Vector2.Distance(stroke[index - 1].position, stroke[index].position) / dt);
                    if (dt > 0.35f)
                    {
                        pauses++;
                    }

                    segments++;
                }
            }

            if (speeds.Count == 0)
            {
                return 0f;
            }

            var mean = speeds.Average();
            var variance = speeds.Average(speed => Mathf.Pow(speed - mean, 2f));
            var coefficient = mean > 0f ? Mathf.Sqrt(variance) / mean : 1f;
            var pauseRatio = segments > 0 ? pauses / (float)segments : 0f;
            return Mathf.Clamp01(1f - Mathf.Clamp01(coefficient * 0.55f + pauseRatio * 0.45f));
        }

        private static float CalculateRotationBias(IReadOnlyList<Vector2> points)
        {
            if (points.Count < 2)
            {
                return 0f;
            }

            var center = new Vector2(points.Average(point => point.x), points.Average(point => point.y));
            var xx = 0f;
            var yy = 0f;
            var xy = 0f;

            foreach (var point in points)
            {
                var delta = point - center;
                xx += delta.x * delta.x;
                yy += delta.y * delta.y;
                xy += delta.x * delta.y;
            }

            var angle = 0.5f * Mathf.Atan2(2f * xy, xx - yy);
            return Mathf.Clamp01(Mathf.Abs(NormalizeHalfPi(angle)) / (Mathf.PI / 2f));
        }

        private static float PathLength(IReadOnlyList<StrokeSample> stroke)
        {
            var length = 0f;
            for (var index = 1; index < stroke.Count; index++)
            {
                length += Vector2.Distance(stroke[index - 1].position, stroke[index].position);
            }

            return length;
        }

        private static float NormalizeHalfPi(float angle)
        {
            while (angle > Mathf.PI / 2f)
            {
                angle -= Mathf.PI;
            }

            while (angle < -Mathf.PI / 2f)
            {
                angle += Mathf.PI;
            }

            return angle;
        }
    }

    public static class SpellLabels
    {
        public static string Korean(SpellFamily family)
        {
            return family switch
            {
                SpellFamily.Wind => "바람",
                SpellFamily.Earth => "땅",
                SpellFamily.Fire => "불꽃",
                SpellFamily.Water => "물",
                SpellFamily.Life => "생명",
                _ => family.ToString()
            };
        }

        public static string English(SpellFamily family)
        {
            return family.ToString().ToLowerInvariant();
        }
    }
}
