using UnityEngine;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// The objects of the battle in the scene, placed by Editor/MainSceneBuilder; the services of the battle size and colour
    /// them from the configs. The map lies on the ground plane: x of the map is world x, y of the map is world z. The templates
    /// are inactive copies of the models of the 3D artist (Editor/ModelLibrary), or placeholders of boxes where a model is
    /// missing; the views copy them: soldiers, piles, places, barracks, the shore and rocks, debris, shots.
    /// </summary>
    public sealed class BattleWorld : MonoBehaviour
    {
        [SerializeField] private MeshRenderer surface;
        [SerializeField] private MeshRenderer island;
        [SerializeField] private MeshRenderer water;
        [SerializeField] private Transform hero;
        [SerializeField] private Transform soldierTemplate;
        [SerializeField] private Transform goldTemplate;
        [SerializeField] private Transform woodTemplate;
        [SerializeField] private Transform siteTemplate;
        [SerializeField] private Transform barracksTemplate;
        [SerializeField] private Transform baseTemplate;
        [SerializeField] private Transform shoreTemplate;
        [SerializeField] private Transform[] shoreVariants;
        [SerializeField] private Transform shoreCornerTemplate;
        [SerializeField] private Transform[] rockTemplates;
        [SerializeField] private Transform debrisTemplate;
        [SerializeField] private Transform shotTemplate;

        public MeshRenderer Surface => surface;
        public MeshRenderer Island => island;
        public MeshRenderer Water => water;
        public Transform Hero => hero;
        public Transform SoldierTemplate => soldierTemplate;
        public Transform GoldTemplate => goldTemplate;
        public Transform WoodTemplate => woodTemplate;
        public Transform SiteTemplate => siteTemplate;
        public Transform BarracksTemplate => barracksTemplate;
        public Transform BaseTemplate => baseTemplate;
        public Transform ShoreTemplate => shoreTemplate;
        /// <summary>The straight shore and its variants of the same size and joints, mixed along the edges.</summary>
        public Transform[] ShoreVariants => shoreVariants;
        public Transform ShoreCornerTemplate => shoreCornerTemplate;
        public Transform[] RockTemplates => rockTemplates;
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
