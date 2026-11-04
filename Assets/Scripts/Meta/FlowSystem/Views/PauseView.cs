using System;
using Meta.LocalizationSystem.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Meta.FlowSystem.Views
{
    /// <summary>
    /// The pause (stage 1 spec 5.4; screens/pause.md, «for layer 4»): «Pause», «Again» above and apart, «Go on» at the bottom of
    /// the window. «Again» asks once more: the first tap turns it into «Really again?» for 3 s, the second starts the level anew
    /// (without crowns). The look is a placeholder of Editor/MainSceneBuilder until the kit of the interface.
    /// </summary>
    public sealed class PauseView : MonoBehaviour
    {
        private const float ConfirmSeconds = 3f;

        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Button resume;
        [SerializeField] private TMP_Text resumeCaption;
        [SerializeField] private Button restart;
        [SerializeField] private TMP_Text restartCaption;
        [Tooltip("«Menu»: leaves the battle for the main menu, or for the editor when the battle came from there.")]
        [SerializeField] private Button leave;
        [SerializeField] private TMP_Text leaveCaption;
        [Tooltip("The light rim of «Really again?»: the second tap must not look like the first (UI review).")]
        [SerializeField] private Outline restartRim;

        private Action _onResume;
        private Action _onRestart;
        private Action _onLeave;
        private string _leaveKey = "pause.menu";
        private float _confirmUntil = -1f;

        public bool Shown => group.alpha > 0f;

        private void Awake()
        {
            resume.onClick.AddListener(() => _onResume?.Invoke());
            restart.onClick.AddListener(Restart);
            leave.onClick.AddListener(() => _onLeave?.Invoke());
            Hide();
        }

        private void OnEnable() => Loc.Changed += Draw;

        private void OnDisable() => Loc.Changed -= Draw;

        public void Show(Action onResume, Action onRestart, Action onLeave, string leaveKey)
        {
            _onResume = onResume;
            _onRestart = onRestart;
            _onLeave = onLeave;
            _leaveKey = leaveKey;
            _confirmUntil = -1f;
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
        }

        // the game is frozen meanwhile: real time
        private void Update()
        {
            if (_confirmUntil > 0f && Time.unscaledTime > _confirmUntil)
            {
                _confirmUntil = -1f;
                Draw();
            }
        }

        private void Restart()
        {
            if (_confirmUntil > 0f)
            {
                _confirmUntil = -1f;
                _onRestart?.Invoke();
                return;
            }

            _confirmUntil = Time.unscaledTime + ConfirmSeconds;
            Draw();
        }

        private void Draw()
        {
            title.text = Loc.Get("pause.title");
            resumeCaption.text = Loc.Get("pause.resume");
            restartCaption.text = Loc.Get(_confirmUntil > 0f ? "pause.restart_confirm" : "pause.restart");
            leaveCaption.text = Loc.Get(_leaveKey);
            if (restartRim != null)
                restartRim.enabled = _confirmUntil > 0f;
        }
    }
}
