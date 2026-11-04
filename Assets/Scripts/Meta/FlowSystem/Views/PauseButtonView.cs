using Meta.FlowSystem.Services;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Meta.FlowSystem.Views
{
    /// <summary>The pause button of the HUD (spec T10): a system button, not an action of the battle, so pillar 1 stands.</summary>
    public sealed class PauseButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;

        private BattleFlow _flow;

        [Inject]
        public void Construct(BattleFlow flow) => _flow = flow;

        private void Awake() => button.onClick.AddListener(() => _flow?.OpenPause());
    }
}
