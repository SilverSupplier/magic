using UnityEngine;
using UnityEngine.UI;

namespace MagicExamHall
{
    public static class PixelMaterialProvider
    {
        private const string SpriteMaterialPath = "MagicExamHallMaterials/PixelSpriteDefault";
        private const string UiMaterialPath = "MagicExamHallMaterials/PixelUIDefault";
        private static Material spriteMaterial;
        private static Material uiMaterial;

        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial == null)
                {
                    spriteMaterial = Resources.Load<Material>(SpriteMaterialPath) ?? CreateFallback("Sprites/Default");
                }

                return spriteMaterial;
            }
        }

        public static Material UiMaterial
        {
            get
            {
                if (uiMaterial == null)
                {
                    uiMaterial = Resources.Load<Material>(UiMaterialPath) ?? Graphic.defaultGraphicMaterial ?? CreateFallback("UI/Default");
                }

                return uiMaterial;
            }
        }

        private static Material CreateFallback(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            return shader == null ? null : new Material(shader);
        }
    }
}
