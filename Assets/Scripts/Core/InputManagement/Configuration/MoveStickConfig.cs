using UnityEngine;

namespace Core.InputManagement.Configuration
{
    /// <summary>
    /// The floating stick (stage 1 spec 2.2; borderbite-ui STYLE §2, screens/move-stick.md). Sizes are units of the 720 × 720
    /// Expand canvas, the one source of these numbers for the stick and its view.
    /// </summary>
    [CreateAssetMenu(fileName = "MoveStickConfig", menuName = "Borderbite/Input/Move Stick")]
    public sealed class MoveStickConfig : ScriptableObject
    {
        [Tooltip("Diameter of the base, canvas units. Starting value: 240.")]
        [Min(1f)] [SerializeField] private float baseDiameter = 240f;

        [Tooltip("Diameter of the knob, canvas units. Starting value: 104.")]
        [Min(1f)] [SerializeField] private float knobDiameter = 104f;

        [Tooltip("How far the knob goes from the centre; further the base follows the finger, canvas units. Starting value: 120.")]
        [Min(1f)] [SerializeField] private float travel = 120f;

        [Tooltip("Dead zone, share of the radius of the base: closer to the centre the hero stands. Starting value: 0.08.")]
        [Range(0f, 0.9f)] [SerializeField] private float deadZone = 0.08f;

        [Tooltip("A touch this close to the edge of the safe area does not start the stick, canvas units. Starting value: 48.")]
        [Min(0f)] [SerializeField] private float edgeGuard = 48f;

        [Tooltip("Opacity of the stick while it is shown, 0..1. Starting value: 0.45.")]
        [Range(0f, 1f)] [SerializeField] private float opacity = 0.45f;

        public float BaseDiameter => baseDiameter;
        public float KnobDiameter => knobDiameter;
        public float Travel => travel;
        public float DeadZone => deadZone;
        public float EdgeGuard => edgeGuard;
        public float Opacity => opacity;
    }
}
