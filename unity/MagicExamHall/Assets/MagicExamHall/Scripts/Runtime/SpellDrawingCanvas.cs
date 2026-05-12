using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MagicExamHall
{
    public sealed class SpellDrawingCanvas : Graphic, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private readonly List<List<StrokeSample>> strokes = new();
        private readonly List<StrokeSample> activeStroke = new();
        private readonly UIVertex[] quad = new UIVertex[4];

        public SpellFamily guideFamily = SpellFamily.Fire;
        public AssistLevel assistLevel;
        public float inkWidth = 5f;
        public Color inkColor = new(0.15f, 0.95f, 1f, 1f);
        public Color guideColor = new(1f, 1f, 1f, 0.2f);

        public IReadOnlyList<IReadOnlyList<StrokeSample>> Strokes => strokes;
        public bool HasInput => strokes.Count > 0 || activeStroke.Count > 1;

        public void SetGuide(SpellFamily family, AssistLevel level, Color color)
        {
            guideFamily = family;
            assistLevel = level;
            guideColor = color;
            SetVerticesDirty();
        }

        public void Clear()
        {
            strokes.Clear();
            activeStroke.Clear();
            SetVerticesDirty();
        }

        public List<List<StrokeSample>> SnapshotStrokes()
        {
            if (activeStroke.Count >= 2)
            {
                strokes.Add(new List<StrokeSample>(activeStroke));
                activeStroke.Clear();
            }

            return strokes.Select(stroke => new List<StrokeSample>(stroke)).ToList();
        }

        public void SetTestStrokes(IEnumerable<IEnumerable<StrokeSample>> testStrokes)
        {
            strokes.Clear();
            activeStroke.Clear();
            foreach (var stroke in testStrokes)
            {
                strokes.Add(stroke.ToList());
            }
            SetVerticesDirty();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            activeStroke.Clear();
            AddPoint(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            AddPoint(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            AddPoint(eventData);
            if (activeStroke.Count >= 2)
            {
                strokes.Add(new List<StrokeSample>(activeStroke));
            }
            activeStroke.Clear();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            DrawGuide(vh);
            foreach (var stroke in strokes)
            {
                DrawStroke(vh, stroke, inkColor, inkWidth);
            }
            DrawStroke(vh, activeStroke, inkColor, inkWidth);
        }

        private void AddPoint(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out var local))
            {
                return;
            }

            var rect = rectTransform.rect;
            var point = new Vector2(local.x + rect.width * 0.5f, local.y + rect.height * 0.5f);
            if (point.x < 0f || point.y < 0f || point.x > rect.width || point.y > rect.height)
            {
                return;
            }

            if (activeStroke.Count > 0 && Vector2.Distance(activeStroke[^1].position, point) < 4f)
            {
                return;
            }

            activeStroke.Add(new StrokeSample(point, Time.time));
            SetVerticesDirty();
        }

        private void DrawGuide(VertexHelper vh)
        {
            var rect = rectTransform.rect;
            var scale = Mathf.Min(rect.width, rect.height) * 0.74f;
            var samples = GestureRecognizer.CreateCanonicalSamples(guideFamily, scale, 0.02f);
            var alpha = assistLevel switch
            {
                AssistLevel.GhostTrace => 0.52f,
                AssistLevel.Checklist => 0.32f,
                AssistLevel.ReasonHint => 0.22f,
                _ => 0.14f
            };
            var width = assistLevel == AssistLevel.GhostTrace ? 5.5f : 3.5f;
            var color = new Color(guideColor.r, guideColor.g, guideColor.b, alpha);
            var offset = new Vector2((rect.width - scale) * 0.5f, (rect.height - scale) * 0.5f);

            foreach (var stroke in samples)
            {
                var shifted = stroke
                    .Select(sample => new StrokeSample(sample.position + offset, sample.time))
                    .ToList();
                DrawStroke(vh, shifted, color, width);
            }
        }

        private void DrawStroke(VertexHelper vh, IReadOnlyList<StrokeSample> stroke, Color strokeColor, float width)
        {
            if (stroke == null || stroke.Count < 2)
            {
                return;
            }

            for (var index = 1; index < stroke.Count; index++)
            {
                AddLine(vh, ToLocal(stroke[index - 1].position), ToLocal(stroke[index].position), width, strokeColor);
            }
        }

        private Vector2 ToLocal(Vector2 point)
        {
            var rect = rectTransform.rect;
            return new Vector2(point.x - rect.width * 0.5f, point.y - rect.height * 0.5f);
        }

        private void AddLine(VertexHelper vh, Vector2 start, Vector2 end, float width, Color lineColor)
        {
            var direction = end - start;
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            var normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
            SetVertex(0, start - normal, lineColor);
            SetVertex(1, start + normal, lineColor);
            SetVertex(2, end + normal, lineColor);
            SetVertex(3, end - normal, lineColor);
            vh.AddUIVertexQuad(quad);
        }

        private void SetVertex(int index, Vector2 position, Color lineColor)
        {
            quad[index] = UIVertex.simpleVert;
            quad[index].position = position;
            quad[index].color = lineColor;
        }
    }
}
