using UnityEngine;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// The objects of the battle in the scene, placed by Editor/MainSceneBuilder; the services of the battle size and colour
    /// them from the configs. The map lies on the ground plane: x of the map is world x, y of the map is world z. The templates
    /// are inactive placeholders the views copy: soldiers, piles, places, barracks, debris, shots.
    /// </summary>
    public sealed class BattleWorld : MonoBehaviour
    {
        [SerializeField] private MeshRenderer surface;
        [SerializeField] private MeshRenderer island;
        [SerializeField] private MeshRenderer water;
        [SerializeField] private Transform hero;
        [SerializeField] private MeshRenderer heroBody;
        [SerializeField] private MeshRenderer heroNose;
        [SerializeField] private Transform soldierTemplate;
        [SerializeField] private Transform goldTemplate;
        [SerializeField] private Transform woodTemplate;
        [SerializeField] private Transform siteTemplate;
        [SerializeField] private Transform barracksTemplate;
        [SerializeField] private Transform debrisTemplate;
        [SerializeField] private Transform shotTemplate;

        public MeshRenderer Surface => surface;
        public MeshRenderer Island => island;
        public MeshRenderer Water => water;
        public Transform Hero => hero;
        public MeshRenderer HeroBody => heroBody;
        public MeshRenderer HeroNose => heroNose;
        public Transform SoldierTemplate => soldierTemplate;
        public Transform GoldTemplate => goldTemplate;
        public Transform WoodTemplate => woodTemplate;
        public Transform SiteTemplate => siteTemplate;
        public Transform BarracksTemplate => barracksTemplate;
        public Transform DebrisTemplate => debrisTemplate;
        public Transform ShotTemplate => shotTemplate;

        /// <summary>A live copy of a template under the world.</summary>
        public Transform Copy(Transform template)
        {
            var copy = Instantiate(template, transform);
            copy.gameObject.SetActive(true);
            return copy;
        }
    }
}
