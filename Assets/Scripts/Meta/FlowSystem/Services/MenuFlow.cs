using System;
using Core.InputManagement.Interfaces;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Services;
using Meta.FlowSystem.Views;
using Meta.MapEditorSystem.Interfaces;
using Meta.MapEditorSystem.Views;

namespace Meta.FlowSystem.Services
{
    /// <summary>
    /// The main menu and the map editor round the battle (owner, 5 November): after the boot the menu; «Play» starts the test level
    /// on its map, «Editor» opens the map editor (only in the Unity editor, where the map files can be written). «Play» of the
    /// editor starts a battle on the map open there, saved or not (layer 5 spec 2.6); the pause of such a battle leads back to the
    /// editor on the same map, the pause of the test level — to the menu.
    /// </summary>
    public sealed class MenuFlow : IDisposable
    {
        private readonly MainMenuView _menu;
        private readonly MapEditorView _editor;
        private readonly IMapLibrary _library;
        private readonly BattleFlow _battle;
        private readonly IBattleDriver _driver;
        private readonly BattleDebug _debug;
        private readonly BattleScreen _screen;
        private readonly IMoveInput _input;
        private bool _fromEditor;

        public MenuFlow(MainMenuView menu, MapEditorView editor, IMapLibrary library, BattleFlow battle, IBattleDriver driver,
            BattleDebug debug, BattleScreen screen, IMoveInput input)
        {
            _menu = menu;
            _editor = editor;
            _library = library;
            _battle = battle;
            _driver = driver;
            _debug = debug;
            _screen = screen;
            _input = input;
            _battle.Began += HideAll;
            _battle.Left += Back;
        }

        /// <summary>The battle started from the editor: its pause leads back there.</summary>
        public bool FromEditor => _fromEditor;

        public void ShowMenu()
        {
            Leave();
            _fromEditor = false;
            _battle.LeaveKey = "pause.menu";
            _menu.Show(Play, _library.Available ? OpenEditor : null);
        }

        /// <summary>«Play» of the menu: the test level on its own map.</summary>
        public void Play()
        {
            _fromEditor = false;
            _battle.LeaveKey = "pause.menu";
            _debug.PlayMap(null);
            _battle.Begin();
        }

        public void OpenEditor()
        {
            Leave();
            _editor.Show(ShowMenu, PlayFromEditor);
        }

        public void Dispose()
        {
            _battle.Began -= HideAll;
            _battle.Left -= Back;
        }

        private void PlayFromEditor(BattleMapAsset map)
        {
            _fromEditor = true;
            _battle.LeaveKey = "pause.editor";
            _debug.PlayMap(map);
            _battle.Begin();
        }

        private void Back()
        {
            if (_fromEditor)
                OpenEditor();
            else
                ShowMenu();
        }

        /// <summary>No battle and nothing of it on the screen: the menu and the editor cover the whole window.</summary>
        private void Leave()
        {
            _driver.StopBattle();
            _battle.HideScreens();
            _input.Enabled = false;
            _screen.Covered = true;
            _screen.Between = true;
            _menu.Hide();
            _editor.Hide();
        }

        private void HideAll()
        {
            _menu.Hide();
            _editor.Hide();
        }
    }
}
