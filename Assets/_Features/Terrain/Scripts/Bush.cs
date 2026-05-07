using System.Collections.Generic;

using UnityEngine;

using Uraty.Shared.Team;
using Uraty.Shared.Hit;
using Uraty.Shared.Visibility;

namespace Uraty.Features.Terrain
{
    public sealed class Bush : MonoBehaviour, IRevealTarget, IBulletHittable
    {
        private readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private readonly int ColorId = Shader.PropertyToID("_Color");
        private readonly int ModeId = Shader.PropertyToID("_Mode");
        private readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

        [SerializeField, Range(0.0f, 1.0f)]
        private float _normalAlpha = 1.0f;

        [SerializeField, Range(0.0f, 1.0f)]
        private float _revealedAlpha = 0.35f;

        private readonly HashSet<object> _revealSources = new();
        private readonly List<MaterialColorState> _materialColorStates = new();

        private bool _isRevealed;

        private void Awake()
        {
            BuildMaterialColorStates();
            ApplyAlpha(_normalAlpha);
        }

        public void AddRevealSource(object source)
        {
            if (source == null)
            {
                return;
            }

            if (_revealSources.Add(source))
            {
                RefreshRevealState();
            }
        }

        public void RemoveRevealSource(object source)
        {
            if (source == null)
            {
                return;
            }

            if (_revealSources.Remove(source))
            {
                RefreshRevealState();
            }
        }

        private void RefreshRevealState()
        {
            bool isRevealed = _revealSources.Count > 0;

            if (_isRevealed == isRevealed)
            {
                return;
            }

            _isRevealed = isRevealed;

            ApplyAlpha(_isRevealed ? _revealedAlpha : _normalAlpha);
        }

        private void BuildMaterialColorStates()
        {
            _materialColorStates.Clear();

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.materials;

                foreach (Material material in materials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    ConfigureMaterialAsFade(material);

                    int colorPropertyId;

                    if (material.HasProperty(BaseColorId))
                    {
                        colorPropertyId = BaseColorId;
                    }
                    else if (material.HasProperty(ColorId))
                    {
                        colorPropertyId = ColorId;
                    }
                    else
                    {
                        continue;
                    }

                    _materialColorStates.Add(new MaterialColorState(
                        material,
                        colorPropertyId,
                        material.GetColor(colorPropertyId)));
                }
            }
        }

        private void ConfigureMaterialAsFade(Material material)
        {
            if (material.HasProperty(ModeId))
            {
                material.SetFloat(ModeId, 2.0f);
            }

            if (material.HasProperty(SrcBlendId))
            {
                material.SetInt(
                    SrcBlendId,
                    (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (material.HasProperty(DstBlendId))
            {
                material.SetInt(
                    DstBlendId,
                    (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty(ZWriteId))
            {
                material.SetInt(ZWriteId, 0);
            }

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void ApplyAlpha(float alpha)
        {
            foreach (MaterialColorState state in _materialColorStates)
            {
                Color color = state.OriginalColor;
                color.a = alpha;

                state.Material.SetColor(
                    state.ColorPropertyId,
                    color);
            }
        }

        private sealed class MaterialColorState
        {
            public Material Material
            {
                get;
            }

            public int ColorPropertyId
            {
                get;
            }

            public Color OriginalColor
            {
                get;
            }

            public MaterialColorState(
                Material material,
                int colorPropertyId,
                Color originalColor)
            {
                Material = material;
                ColorPropertyId = colorPropertyId;
                OriginalColor = originalColor;
            }
        }

        public bool ReceiveBulletHit(GameObject owner, TeamId teamId, float damage, bool isPiercing)
        {
            // 弾は壊さない
            return false;
        }
    }
}
