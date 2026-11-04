using UnityEngine;

namespace Meta.BattleSystem.Utils
{
    /// <summary>
    /// The red flash of a unit hit (task 4.20, the 3D artist's HIT.md, owner: «каждое попадание — плавное, но быстрое
    /// покраснение»): k rises from where it is to 1 in 0.045 s by smoothstep and falls by (1 − f)³ in 0.22 s; a new hit lifts it
    /// from where it is, so quick blows never blink. The shaders Flat Lit and Flat Lit Outlined turn the surface red by
    /// <c>_Hit</c> and make it glow. A property block is set on every renderer of the unit only while k is above zero and taken
    /// off after, so the SRP batcher batches the unit again.
    /// </summary>
    public sealed class HitFlash
    {
        private const float Rise = 0.045f;
        private const float Fall = 0.22f;
        private static readonly int HitId = Shader.PropertyToID("_Hit");

        private readonly Renderer[] _renderers;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private float _k;
        private float _from;
        private float _since = 1000f;
        private bool _set;

        public HitFlash(Transform root) => _renderers = root.GetComponentsInChildren<Renderer>(true);

        public void Hit()
        {
            _from = _k;
            _since = 0f;
        }

        /// <summary>Puts the unit back as it was: a pooled body taken again, a battle started again.</summary>
        public void Clear()
        {
            _k = 0f;
            _since = 1000f;
            Apply(0f);
        }

        public void Update(float deltaTime)
        {
            if (!_set && _since >= Rise + Fall)
                return;

            _since += deltaTime;
            if (_since < Rise)
            {
                var t = _since / Rise;
                _k = Mathf.Lerp(_from, 1f, t * t * (3f - 2f * t));
            }
            else
            {
                var f = Mathf.Clamp01((_since - Rise) / Fall);
                _k = (1f - f) * (1f - f) * (1f - f);
            }

            Apply(_k);
        }

        private void Apply(float k)
        {
            if (k <= 0.001f)
            {
                if (!_set)
                    return;

                foreach (var renderer in _renderers)
                    renderer.SetPropertyBlock(null);
                _set = false;
                return;
            }

            _block.SetFloat(HitId, k);
            foreach (var renderer in _renderers)
                renderer.SetPropertyBlock(_block);
            _set = true;
        }
    }
}
