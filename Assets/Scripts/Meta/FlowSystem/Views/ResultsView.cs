using System;
using Core.BattleSystem.Enums;
using Meta.LocalizationSystem.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Meta.FlowSystem.Views
{
    /// <summary>
    /// The results of a level (stage 1 spec 5.2; screens/results.md, «for layer 4»): from the top, «Victory» or «Defeat», the
    /// reason in a line (70% of the land, or the base fell), the share of blue land, the crowns earned large with the crown, and
    /// the main button — «Next» after a victory, «Again» after a defeat — which goes to the upgrades. The crowns count up in at
    /// most 1 s; the button works at once. The look is a placeholder of Editor/MainSceneBuilder until the kit of the interface.
    /// </summary>
    public sealed class ResultsView : MonoBehaviour
    {
        private const float CountSeconds = 0.8f;

        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text reason;
        [SerializeField] private TMP_Text share;
        [SerializeField] private TMP_Text crowns;
        [SerializeField] private TMP_Text caption;
        [SerializeField] private Button retry;
        [SerializeField] private RectTransform retryRect;

        private Action _onNext;
        private BattleResult _result;
        private bool _baseFell;
        private int _share;
        private int _winnerShare;
        private int _crowns;
        private float _shownAt;

        public bool Shown => group.alpha > 0f;

        private void Awake()
        {
            retry.onClick.AddListener(() => _onNext?.Invoke());
            Hide();
        }

        private void OnEnable() => Loc.Changed += Draw;

        private void OnDisable() => Loc.Changed -= Draw;

        /// <summary>Shows a result: whether a base fell, the blue share and the winner's share (the reason), percent; the crowns earned.</summary>
        public void Show(BattleResult result, bool baseFell, int sharePercent, int winnerShare, int crownsEarned, Action onNext)
        {
            _result = result;
            _baseFell = baseFell;
            _share = sharePercent;
            _winnerShare = winnerShare;
            _crowns = crownsEarned;
            _onNext = onNext;
            _shownAt = Time.unscaledTime;
            Draw();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        public void Hide()
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            _onNext = null;
        }

        /// <summary>The main button at the bottom in the middle on a tall screen, at the bottom right on a wide one; the count of crowns.</summary>
        private void Update()
        {
            if (Shown && crowns != null)
            {
                var t = Mathf.Clamp01((Time.unscaledTime - _shownAt) / CountSeconds);
                crowns.text = "+" + Mathf.RoundToInt(_crowns * t);
            }

            var x = Screen.width >= Screen.height ? 1f : 0.5f;
            if (Mathf.Approximately(retryRect.anchorMin.x, x))
                return;

            retryRect.anchorMin = retryRect.anchorMax = new Vector2(x, 0f);
            retryRect.pivot = new Vector2(x, 0f);
            retryRect.anchoredPosition = new Vector2(x > 0.5f ? -retryRect.anchoredPosition.y : 0f, retryRect.anchoredPosition.y);
        }

        private void Draw()
        {
            var victory = _result == BattleResult.Victory;
            title.text = Loc.Get(victory ? "results.victory" : "results.defeat");
            if (reason != null)
                reason.text = _baseFell ? Loc.Get("results.reason.base") : string.Format(Loc.Get("results.reason.share"), _winnerShare);
            if (share != null)
                share.text = string.Format(Loc.Get("results.share"), _share);
            if (caption != null)
                caption.text = Loc.Get(victory ? "results.next" : "results.retry");
        }
    }
}
