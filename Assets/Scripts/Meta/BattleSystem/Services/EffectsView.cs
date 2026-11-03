using System.Collections.Generic;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Views;
using UnityEngine;
using VContainer.Unity;

namespace Meta.BattleSystem.Services
{
    /// <summary>
    /// Short effects of the battle, copies of templates kept for reuse: the trace of a shot from the muzzle to the target and the
    /// cubes a fallen soldier, hero or barracks breaks into (a cartoon war without blood).
    /// </summary>
    public sealed class EffectsView : ILateTickable
    {
        private const float Gravity = 9f;
        private const float MuzzleHeight = 0.6f;

        private readonly BattleViewConfig _view;
        private readonly BattleWorld _world;
        private readonly ViewMaterials _materials;
        private readonly List<Piece> _pieces = new List<Piece>();
        private readonly List<Piece> _shots = new List<Piece>();
        private uint _random = 0x9E3779B9u;

        public EffectsView(BattleViewConfig view, BattleWorld world, ViewMaterials materials)
        {
            _view = view;
            _world = world;
            _materials = materials;
        }

        /// <summary>Breaks something at <paramref name="position"/> into cubes of its colour that fly up and fall.</summary>
        public void Debris(Vector3 position, Color color, int count, float size)
        {
            for (var i = 0; i < count; i++)
            {
                var piece = Free(_pieces, _world.DebrisTemplate);
                piece.Transform.localPosition = position + new Vector3(Next() - 0.5f, 0.3f + Next() * 0.4f, Next() - 0.5f) * size;
                piece.Transform.localScale = Vector3.one * size * (0.18f + Next() * 0.12f);
                piece.Velocity = new Vector3(Next() * 2f - 1f, 2f + Next() * 2f, Next() * 2f - 1f) * 1.5f;
                piece.Age = 0f;
                piece.Life = _view.DebrisSeconds;
                _materials.Paint(piece.Transform, color);
            }
        }

        /// <summary>A trace from the shooter to the target.</summary>
        public void Shot(Vector3 from, Vector3 to)
        {
            var piece = Free(_shots, _world.ShotTemplate);
            from.y += MuzzleHeight;
            to.y += MuzzleHeight * 0.7f;
            var line = to - from;
            piece.Transform.localPosition = (from + to) / 2f;
            piece.Transform.localRotation = line.sqrMagnitude > 0f ? Quaternion.LookRotation(line) : Quaternion.identity;
            piece.Transform.localScale = new Vector3(0.08f, 0.08f, line.magnitude);
            piece.Age = 0f;
            piece.Life = _view.ShotSeconds;
            _materials.Paint(piece.Transform, _view.ShotColor);
        }

        public void LateTick()
        {
            var dt = Time.deltaTime;
            foreach (var piece in _pieces)
            {
                if (!piece.Transform.gameObject.activeSelf)
                    continue;

                piece.Age += dt;
                piece.Velocity.y -= Gravity * dt;
                var position = piece.Transform.localPosition + piece.Velocity * dt;
                if (position.y < 0.05f)
                {
                    position.y = 0.05f;
                    piece.Velocity *= 0.5f;
                }

                piece.Transform.localPosition = position;
                if (piece.Age > piece.Life * 0.6f)
                    piece.Transform.localScale *= 1f - Mathf.Clamp01(dt / (piece.Life * 0.4f));
                if (piece.Age >= piece.Life)
                    piece.Transform.gameObject.SetActive(false);
            }

            foreach (var shot in _shots)
            {
                if (!shot.Transform.gameObject.activeSelf)
                    continue;

                shot.Age += dt;
                var scale = shot.Transform.localScale;
                var thin = 0.08f * Mathf.Clamp01(1f - shot.Age / shot.Life);
                shot.Transform.localScale = new Vector3(thin, thin, scale.z);
                if (shot.Age >= shot.Life)
                    shot.Transform.gameObject.SetActive(false);
            }
        }

        private Piece Free(List<Piece> pool, Transform template)
        {
            foreach (var piece in pool)
            {
                if (!piece.Transform.gameObject.activeSelf)
                {
                    piece.Transform.gameObject.SetActive(true);
                    return piece;
                }
            }

            var created = new Piece { Transform = _world.Copy(template) };
            pool.Add(created);
            return created;
        }

        /// <summary>A number in [0, 1) for the look of the effects only; the battle never sees it.</summary>
        private float Next()
        {
            _random ^= _random << 13;
            _random ^= _random >> 17;
            _random ^= _random << 5;
            return (_random & 0xFFFFFF) / (float)0x1000000;
        }

        private sealed class Piece
        {
            public Transform Transform;
            public Vector3 Velocity;
            public float Age;
            public float Life;
        }
    }
}
