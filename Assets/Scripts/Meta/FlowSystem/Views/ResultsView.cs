using System;
using Core.BattleSystem.Enums;
using Meta.LocalizationSystem.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Meta.FlowSystem.Views
{
    /// <summary>
    /// The simple results of layer 1 (stage 1 spec 2.6): «Victory» or «Defeat» and «Again», which starts the level anew.
    /// The look is a placeholder of Editor/MainSceneBuilder until the builder of the interface (borderbite-ui, screens/results.md).
    /// </summary>
    public sealed class ResultsView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Button retry;
        [SerializeField] private RectTransform retryRect;

        private Action _onRetry;
        private BattleResult _result;

        public bool Shown => group.alpha > 0f;

        private void Awake()
        {
            retry.onClick.AddListener(() => _onRetry?.Invoke());
            Hide();
        }

        private void OnEnable() => Loc.Changed += Draw;

        private void OnDisable() => Loc.Changed -= Draw;

        public void Show(BattleResult result, Action onRetry)
        {
            _result = result;
            _onRetry = onRetry;
            Draw();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        /// <summary>«Again» at the bottom in the middle on a tall screen, at the bottom right on a wide one (screens/results.md).</summary>
        private void Update()
        {
            var x = Screen.width >= Screen.height ? 1f : 0.5f;
            if (Mathf.Approximately(retryRect.anchorMin.x, x))
                return;

            retryRect.anchorMin = retryRect.anchorMax = new Vector2(x, 0f);
            retryRect.pivot = new Vector2(x, 0f);
            retryRect.anchoredPosition = new Vector2(x > 0.5f ? -retryRect.anchoredPosition.y : 0f, retryRect.anchoredPosition.y);
        }

        public void Hide()
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private void Draw() => title.text = Loc.Get(_result == BattleResult.Victory ? "results.victory" : "results.defeat");
    }
}
