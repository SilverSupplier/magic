using System.Collections;
using UnityEngine;

namespace MagicExamHall
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PixelSpriteView : MonoBehaviour
    {
        public PixelSpriteKind kind = PixelSpriteKind.Target;
        public Color primary = Color.white;
        public Color secondary = Color.gray;
        public int sortingOrder;
        public bool tiled;
        public Vector2 tiledSize = Vector2.one;

        private IEnumerator Start()
        {
            yield return null;
            Apply();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                Apply();
            }
        }

        public void Apply()
        {
            var spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = PixelArtFactory.CreateSprite(name, primary, secondary, kind);
            spriteRenderer.sharedMaterial = PixelMaterialProvider.SpriteMaterial;
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.drawMode = tiled ? SpriteDrawMode.Tiled : SpriteDrawMode.Simple;
            if (tiled)
            {
                spriteRenderer.size = tiledSize;
            }
        }
    }
}
