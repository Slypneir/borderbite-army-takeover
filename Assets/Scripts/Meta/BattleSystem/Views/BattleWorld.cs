using UnityEngine;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// The objects of the battle in the scene, placed by Editor/MainSceneBuilder; the services of the battle size and colour
    /// them from the configs. The map lies on the ground plane: x of the map is world x, y of the map is world z.
    /// </summary>
    public sealed class BattleWorld : MonoBehaviour
    {
        [SerializeField] private MeshRenderer surface;
        [SerializeField] private MeshRenderer island;
        [SerializeField] private MeshRenderer water;
        [SerializeField] private Transform hero;
        [SerializeField] private MeshRenderer heroBody;
        [SerializeField] private MeshRenderer heroNose;

        public MeshRenderer Surface => surface;
        public MeshRenderer Island => island;
        public MeshRenderer Water => water;
        public Transform Hero => hero;
        public MeshRenderer HeroBody => heroBody;
        public MeshRenderer HeroNose => heroNose;
    }
}
