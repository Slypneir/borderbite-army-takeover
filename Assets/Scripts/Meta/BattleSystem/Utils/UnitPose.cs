using UnityEngine;

namespace Meta.BattleSystem.Utils
{
    /// <summary>
    /// The moving parts of a hero or soldier of the 3D artist (Docs/ART_REQUESTS.md): `leg_l`, `leg_r` turn at the hip, `arm_l`,
    /// `arm_r` at the shoulder, all around x; an arm turned forward by 90° points what it holds forward. Legs swing while it
    /// walks, the right arm swings a blow, the left arm holds the gun forward for a moment after a shot. A placeholder without
    /// these parts simply does not move them.
    /// </summary>
    public sealed class UnitPose
    {
        private const float StepsPerMeter = 1.6f;
        private const float LegSwing = 35f;
        private const float BlowSeconds = 0.25f;
        private const float AimSeconds = 0.45f;

        private readonly Transform _legL;
        private readonly Transform _legR;
        private readonly Transform _armL;
        private readonly Transform _armR;
        private float _phase;
        private float _blowAge = BlowSeconds;
        private float _aimAge = AimSeconds;

        public UnitPose(Transform root)
        {
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                switch (node.name)
                {
                    case "leg_l": _legL = node; break;
                    case "leg_r": _legR = node; break;
                    case "arm_l": _armL = node; break;
                    case "arm_r": _armR = node; break;
                }
            }

            Muzzle = Find(root, "muzzle");
        }

        /// <summary>The end of the gun, or null when the model has none.</summary>
        public Transform Muzzle { get; }

        public void Blow() => _blowAge = 0f;

        public void Aim() => _aimAge = 0f;

        /// <summary>Poses the parts for this frame; <paramref name="meters"/> is how far the unit went since the last frame.</summary>
        public void Update(float meters, float deltaTime)
        {
            _phase += meters * StepsPerMeter * Mathf.PI;
            var walking = meters > 0.0001f;
            var swing = walking ? Mathf.Sin(_phase) * LegSwing : 0f;
            Turn(_legL, swing);
            Turn(_legR, -swing);

            _blowAge += deltaTime;
            _aimAge += deltaTime;
            // the blow: up and forward fast, back slower
            var blow = _blowAge < BlowSeconds ? Mathf.Sin(_blowAge / BlowSeconds * Mathf.PI) * -110f : -swing * 0.6f;
            Turn(_armR, blow);
            Turn(_armL, _aimAge < AimSeconds ? -90f : swing * 0.6f);
        }

        private static void Turn(Transform part, float degrees)
        {
            if (part != null)
                part.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                if (node.name == name)
                    return node;
            }

            return null;
        }
    }
}
