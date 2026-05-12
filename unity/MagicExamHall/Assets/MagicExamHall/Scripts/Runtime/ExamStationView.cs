using UnityEngine;

namespace MagicExamHall
{
    public sealed class ExamStationView : MonoBehaviour
    {
        public SpellFamily family;
        public string title = "";
        [TextArea] public string prompt = "";
        public Color color = Color.white;
        public SpriteRenderer stationRenderer = null!;
        public SpriteRenderer targetRenderer = null!;
        public Transform targetTransform = null!;
        public int attempts;
        public int failureCount;
        public bool completed;

        private Vector3 baseScale;
        private Vector3 targetBaseScale;

        public void InitializeSprites()
        {
            stationRenderer ??= GetComponent<SpriteRenderer>();
            targetTransform ??= transform.Find("Target");
            if (targetTransform != null)
            {
                targetRenderer ??= targetTransform.GetComponent<SpriteRenderer>();
            }

            if (stationRenderer != null)
            {
                stationRenderer.sprite = PixelArtFactory.CreateSprite($"{family} Station", color, Color.white, PixelSpriteKind.Station);
                stationRenderer.color = Color.white;
            }

            if (targetRenderer != null)
            {
                targetRenderer.sprite = PixelArtFactory.CreateSprite($"{family} Target", color, new Color(0.45f, 0.48f, 0.54f), PixelSpriteKind.Target);
                targetRenderer.color = Color.white;
            }

            baseScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
            targetBaseScale = targetTransform != null ? targetTransform.localScale : Vector3.one;
        }

        public void TickIdle(float time)
        {
            if (completed)
            {
                return;
            }

            var pulse = 1f + Mathf.Sin(time * 2.2f + (int)family) * 0.035f;
            transform.localScale = baseScale * pulse;
        }

        public void ApplySuccessEffect()
        {
            completed = true;
            failureCount = 0;

            if (targetRenderer != null)
            {
                targetRenderer.sprite = PixelArtFactory.CreateSprite($"{family} Success Target", Color.white, color, PixelSpriteKind.Target);
            }

            if (targetTransform == null)
            {
                return;
            }

            switch (family)
            {
                case SpellFamily.Fire:
                    targetTransform.localScale = targetBaseScale * 1.35f;
                    break;
                case SpellFamily.Water:
                    targetTransform.rotation = Quaternion.Euler(0f, 0f, 20f);
                    break;
                case SpellFamily.Wind:
                    targetTransform.position += Vector3.right * 0.85f;
                    break;
                case SpellFamily.Earth:
                    targetTransform.localScale = new Vector3(targetBaseScale.x * 2.2f, targetBaseScale.y * 0.7f, 1f);
                    break;
                case SpellFamily.Life:
                    targetTransform.localScale = new Vector3(targetBaseScale.x * 0.9f, targetBaseScale.y * 2.1f, 1f);
                    break;
            }
        }
    }
}
