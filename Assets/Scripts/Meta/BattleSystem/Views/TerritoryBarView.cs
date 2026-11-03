using Core.BattleSystem.Enums;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using TMPro;
using UnityEngine;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// The territory bar at the top (stage 1 spec 2.6): blue share from the left, red from the right, grey between, a mark at
    /// the victory share from each side and both percents as numbers. Shares count every cell of the map.
    /// </summary>
    public sealed class TerritoryBarView : MonoBehaviour
    {
        [SerializeField] private RectTransform playerFill;
        [SerializeField] private RectTransform enemyFill;
        [SerializeField] private RectTransform playerMark;
        [SerializeField] private RectTransform enemyMark;
        [SerializeField] private TMP_Text playerLabel;
        [SerializeField] private TMP_Text enemyLabel;

        private IBattleDriver _battle;
        private BattleConfig _rules;
        private int _player = -1;
        private int _enemy = -1;

        [Inject]
        public void Construct(IBattleDriver battle, BattleConfig rules)
        {
            _battle = battle;
            _rules = rules;
        }

        private void LateUpdate()
        {
            var grid = _battle?.Current?.Simulation.Grid;
            if (grid == null)
                return;

            var mark = _rules.VictoryShare / 100f;
            playerMark.anchorMin = playerMark.anchorMax = new Vector2(mark, 0.5f);
            enemyMark.anchorMin = enemyMark.anchorMax = new Vector2(1f - mark, 0.5f);

            var player = grid.Count(Side.Player);
            var enemy = grid.Count(Side.Enemy);
            if (player == _player && enemy == _enemy)
                return;

            _player = player;
            _enemy = enemy;
            var playerShare = (float)player / grid.Total;
            var enemyShare = (float)enemy / grid.Total;
            playerFill.anchorMax = new Vector2(playerShare, 1f);
            enemyFill.anchorMin = new Vector2(1f - enemyShare, 0f);
            playerLabel.text = Mathf.FloorToInt(playerShare * 100f) + "%";
            enemyLabel.text = Mathf.FloorToInt(enemyShare * 100f) + "%";
        }
    }
}
