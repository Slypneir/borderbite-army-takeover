using System;
using Meta.LocalizationSystem.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Meta.FlowSystem.Views
{
    /// <summary>
    /// The main menu (screens/main-menu.md; owner, 5 November): the name of the game and «Play», which starts the test level, and
    /// «Editor», which opens the map editor — only where the map files can be written (the Unity editor). The look is a
    /// placeholder of Editor/MainSceneBuilder until the kit of the interface.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Button play;
        [SerializeField] private TMP_Text playCaption;
        [SerializeField] private Button editor;
        [SerializeField] private TMP_Text editorCaption;

        private Action _onPlay;
        private Action _onEditor;

        public bool Shown => group.alpha > 0f;

        private void Awake()
        {
            play.onClick.AddListener(() => _onPlay?.Invoke());
            editor.onClick.AddListener(() => _onEditor?.Invoke());
            Hide();
        }

        private void OnEnable() => Loc.Changed += Draw;

        private void OnDisable() => Loc.Changed -= Draw;

        /// <summary>Shows the menu; with no <paramref name="onEditor"/> there is no «Editor».</summary>
        public void Show(Action onPlay, Action onEditor)
        {
            _onPlay = onPlay;
            _onEditor = onEditor;
            editor.gameObject.SetActive(onEditor != null);
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

        private void Draw()
        {
            title.text = Loc.Get("boot.title");
            playCaption.text = Loc.Get("menu.play");
            editorCaption.text = Loc.Get("menu.editor");
        }
    }
}
