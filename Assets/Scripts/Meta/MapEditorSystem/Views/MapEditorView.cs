using System;
using System.Collections.Generic;
using Core.BattleSystem.Enums;
using Meta.BattleSystem.Configuration;
using Meta.MapEditorSystem.Interfaces;
using Meta.MapEditorSystem.Services;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace Meta.MapEditorSystem.Views
{
    /// <summary>
    /// The map editor (layer 5, tasks 5.2–5.4; screens/map-editor.md), opened from the main menu by «Редактор». On the left the
    /// list of maps with «new 48 / 64 / 80», «copy», «rename», «delete», then the brushes of land and water of 1, 3 or 5 cells, the
    /// grid, «save», «undo», «redo», «play» and «menu»; in the middle the map from above, a texel block per cell: grass, water,
    /// the start areas in the colours of the sides, the bases, the places and the piles; below a line of the size, the share of
    /// land, the places and the distance between the bases. The wheel zooms round the pointer, the middle or the right button
    /// moves the view, the left one paints. Ctrl+Z / Ctrl+Y undo and redo, Ctrl+S saves. Leaving a map with changes asks
    /// «Save?». Only the owner uses it, in the Unity editor: the words are Russian and not in the tables of the languages.
    /// </summary>
    public sealed class MapEditorView : MonoBehaviour
    {
        private const int CellPixels = 8;
        private const float MinZoom = 0.5f;
        private const float MaxZoom = 10f;
        private const float MessageSeconds = 4f;
        private static readonly Color32 Grass = new Color32(106, 150, 74, 255);
        private static readonly Color32 Sea = new Color32(46, 132, 150, 255);
        private static readonly Color32 Stone = new Color32(178, 176, 168, 255);
        private static readonly Color32 StoneEdge = new Color32(96, 94, 90, 255);
        private static readonly Color32 Gold = new Color32(250, 205, 60, 255);
        private static readonly Color32 Wood = new Color32(130, 82, 44, 255);
        private static readonly Color32 Rock = new Color32(120, 122, 126, 255);
        // not yellow: yellow is gold, a signal of the game (borderbite-ui STYLE §3)
        private static readonly Color Selected = new Color(0.62f, 0.86f, 0.62f, 1f);
        private static readonly Color Normal = new Color(0.92f, 0.93f, 0.95f, 1f);

        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RawImage map;
        [SerializeField] private MapViewportInput input;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private Button itemTemplate;
        [SerializeField] private TMP_InputField nameField;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text message;

        [Header("Buttons")]
        [SerializeField] private Button create48;
        [SerializeField] private Button create64;
        [SerializeField] private Button create80;
        [SerializeField] private Button copy;
        [SerializeField] private Button rename;
        [SerializeField] private Button remove;
        [SerializeField] private Button land;
        [SerializeField] private Button water;
        [SerializeField] private Button brush1;
        [SerializeField] private Button brush3;
        [SerializeField] private Button brush5;
        [SerializeField] private Button grid;
        [SerializeField] private Button save;
        [SerializeField] private Button undo;
        [SerializeField] private Button redo;
        [SerializeField] private Button play;
        [SerializeField] private Button exit;

        [Header("Dialog")]
        [SerializeField] private CanvasGroup dialog;
        [SerializeField] private TMP_Text dialogText;
        [SerializeField] private Button dialogYes;
        [SerializeField] private TMP_Text dialogYesCaption;
        [SerializeField] private Button dialogNo;
        [SerializeField] private TMP_Text dialogNoCaption;
        [SerializeField] private Button dialogCancel;

        private readonly List<Button> _items = new List<Button>();
        private IMapLibrary _library;
        private MapEditing _editing;
        private BattleConfig _rules;
        private BattleViewConfig _view;
        private Action _onExit;
        private Action<BattleMapAsset> _onPlay;
        private Action _dialogYes;
        private Action _dialogNo;
        private Texture2D _texture;
        private Color32[] _pixels;
        private bool _water;
        private int _brush = 3;
        private bool _grid = true;
        private float _zoom = 1f;
        private Vector2 _pan;
        private bool _painting;
        private bool _strokeChanged;
        private Vector2Int _last;
        private Vector2Int _hover = new Vector2Int(-1, -1);
        private float _messageUntil;
        private string _lastName;

        public bool Shown => group.alpha > 0f;

        [Inject]
        public void Construct(IMapLibrary library, MapEditing editing, BattleConfig rules, BattleViewConfig view)
        {
            _library = library;
            _editing = editing;
            _rules = rules;
            _view = view;
        }

        private void Awake()
        {
            itemTemplate.gameObject.SetActive(false);
            create48.onClick.AddListener(() => Guard(() => Create(48)));
            create64.onClick.AddListener(() => Guard(() => Create(64)));
            create80.onClick.AddListener(() => Guard(() => Create(80)));
            copy.onClick.AddListener(() => Guard(Copy));
            rename.onClick.AddListener(Rename);
            remove.onClick.AddListener(Remove);
            land.onClick.AddListener(() => SetTool(false, _brush));
            water.onClick.AddListener(() => SetTool(true, _brush));
            brush1.onClick.AddListener(() => SetTool(_water, 1));
            brush3.onClick.AddListener(() => SetTool(_water, 3));
            brush5.onClick.AddListener(() => SetTool(_water, 5));
            grid.onClick.AddListener(() =>
            {
                _grid = !_grid;
                Redraw();
                DrawTools();
            });
            save.onClick.AddListener(Save);
            undo.onClick.AddListener(Undo);
            redo.onClick.AddListener(Redo);
            play.onClick.AddListener(Play);
            exit.onClick.AddListener(() => Guard(Exit));
            dialogYes.onClick.AddListener(() => CloseDialog(_dialogYes));
            dialogNo.onClick.AddListener(() => CloseDialog(_dialogNo));
            dialogCancel.onClick.AddListener(() => CloseDialog(null));
            input.Pressed += Press;
            input.Dragged += Drag;
            input.Released += Release;
            input.Scrolled += Scroll;
            input.Moved += Move;
            Hide();
            HideDialog();
        }

        private void OnDestroy()
        {
            if (_texture != null)
                Destroy(_texture);
        }

        /// <summary>Opens the editor on the map it had, or on the map of the test level the first time.</summary>
        public void Show(Action onExit, Action<BattleMapAsset> onPlay)
        {
            _onExit = onExit;
            _onPlay = onPlay;
            if (_editing.Map == null)
            {
                // the map it had before the menu, else that of the test level
                var names = _library.Names();
                var first = _lastName != null && _library.Load(_lastName) != null ? _lastName
                    : _library.TestMap ?? (names.Count > 0 ? names[0] : _library.Create(64));
                Open(first);
            }

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            RefreshList();
            DrawTools();
            Redraw();
        }

        public void Hide()
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            _painting = false;
        }

        private void Update()
        {
            if (!Shown)
                return;

            Layout();
            if (message.text.Length > 0 && Time.unscaledTime > _messageUntil)
                message.text = string.Empty;

            var keyboard = Keyboard.current;
            if (keyboard == null || nameField.isFocused || dialog.alpha > 0f)
                return;

            var control = keyboard.ctrlKey.isPressed;
            if (control && keyboard.zKey.wasPressedThisFrame)
            {
                if (keyboard.shiftKey.isPressed)
                    Redo();
                else
                    Undo();
            }
            else if (control && keyboard.yKey.wasPressedThisFrame)
                Redo();
            else if (control && keyboard.sKey.wasPressedThisFrame)
                Save();
        }

        // ---- maps

        /// <summary>Runs an action that leaves the open map; with changes not saved it asks first.</summary>
        private void Guard(Action then)
        {
            if (!_editing.Dirty)
            {
                then();
                return;
            }

            Ask($"Сохранить изменения в «{_editing.Name}»?", "Сохранить", "Не сохранять", () =>
            {
                Save();
                then();
            }, then);
        }

        private void Open(string name)
        {
            var file = _library.Load(name);
            if (file == null)
                return;

            _editing.Open(name, file);
            nameField.SetTextWithoutNotify(name);
            _zoom = 1f;
            _pan = Vector2.zero;
            RefreshList();
            DrawTools();
            Redraw();
        }

        private void Create(int cells) => Open(_library.Create(cells));

        /// <summary>To the main menu: changes not saved are dropped (the question came before), the map opens again next time.</summary>
        private void Exit()
        {
            _lastName = _editing.Name;
            _editing.Close();
            _onExit?.Invoke();
        }

        private void Copy() => Open(_library.Copy(_editing.Name));

        private void Rename()
        {
            var error = _library.Rename(_editing.Name, nameField.text);
            if (error != null)
            {
                Say(error);
                nameField.SetTextWithoutNotify(_editing.Name);
                return;
            }

            _editing.Renamed(nameField.text.Trim());
            RefreshList();
            DrawTools();
        }

        private void Remove()
        {
            var name = _editing.Name;
            Ask($"Удалить карту «{name}»? Файл пропадёт из проекта.", "Удалить", null, () =>
            {
                var error = _library.Delete(name);
                if (error != null)
                {
                    Say(error);
                    return;
                }

                _editing.Close();
                var names = _library.Names();
                Open(_library.TestMap ?? (names.Count > 0 ? names[0] : _library.Create(64)));
            }, null);
        }

        private void Save()
        {
            if (_editing.Map == null)
                return;

            _library.Save(_editing.Name, _editing.Map);
            _editing.Saved();
            Say($"«{_editing.Name}» сохранена");
            DrawTools();
        }

        private void Undo()
        {
            if (_editing.Undo())
                Changed();
        }

        private void Redo()
        {
            if (_editing.Redo())
                Changed();
        }

        private void Play()
        {
            if (_editing.Map != null)
                _onPlay?.Invoke(_editing.Map);
        }

        private void Changed()
        {
            Redraw();
            DrawTools();
        }

        private void RefreshList()
        {
            foreach (var item in _items)
                Destroy(item.gameObject);
            _items.Clear();
            foreach (var name in _library.Names())
            {
                var item = Instantiate(itemTemplate, listContent);
                item.gameObject.SetActive(true);
                var label = item.GetComponentInChildren<TMP_Text>();
                label.text = name == _library.TestMap ? name + "  (тестовый уровень)" : name;
                ((Image)item.targetGraphic).color = name == _editing.Name ? Selected : Normal;
                var picked = name;
                item.onClick.AddListener(() =>
                {
                    if (picked != _editing.Name)
                        Guard(() => Open(picked));
                });
                _items.Add(item);
            }
        }

        // ---- tools

        private void SetTool(bool wet, int brush)
        {
            _water = wet;
            _brush = brush;
            DrawTools();
        }

        private void DrawTools()
        {
            Mark(land, !_water);
            Mark(water, _water);
            Mark(brush1, _brush == 1);
            Mark(brush3, _brush == 3);
            Mark(brush5, _brush == 5);
            Mark(grid, _grid);
            undo.interactable = _editing.CanUndo;
            redo.interactable = _editing.CanRedo;
            save.interactable = _editing.Dirty;
            title.text = _editing.Name == null ? string.Empty : _editing.Name + (_editing.Dirty ? " *" : string.Empty);
            DrawStatus();
        }

        private static void Mark(Button button, bool on) => ((Image)button.targetGraphic).color = on ? Selected : Normal;

        private void DrawStatus()
        {
            var m = _editing.Map;
            if (m == null)
            {
                status.text = string.Empty;
                return;
            }

            var total = m.Size * m.Size;
            var landShare = Mathf.RoundToInt(m.LandCells() * 100f / total);
            var a = m.PlayerBase.corner;
            var b = m.EnemyBase.corner;
            var meters = Vector2Int.Distance(a, b) * _rules.CellSize;
            // no «×» and «·» in the font: words and bars
            var hover = _hover.x >= 0 ? $"   |   клетка {_hover.x}, {_hover.y}" : string.Empty;
            status.text = $"{m.Size} на {m.Size} клеток   |   суша {landShare}%   |   мест под постройку {m.Sites.Count}   |   " +
                          $"между штабами {meters:0.#} м{hover}";
        }

        // ---- the map

        private void Redraw()
        {
            var m = _editing.Map;
            if (m == null)
                return;

            var side = m.Size * CellPixels;
            if (_texture == null || _texture.width != side)
            {
                if (_texture != null)
                    Destroy(_texture);
                _texture = new Texture2D(side, side, TextureFormat.RGBA32, false)
                {
                    name = "EditorMap",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                _pixels = new Color32[side * side];
                map.texture = _texture;
            }

            var player = (Color32)_view.PlayerColor;
            var enemy = (Color32)_view.EnemyColor;
            for (var y = 0; y < m.Size; y++)
            for (var x = 0; x < m.Size; x++)
            {
                Color32 color = Grass;
                if (m.IsWater(x, y))
                    color = Sea;
                else if (InStart(m.PlayerBase, x, y))
                    color = Color32.Lerp(Grass, player, 0.45f);
                else if (InStart(m.EnemyBase, x, y))
                    color = Color32.Lerp(Grass, enemy, 0.45f);
                Fill(x, y, 0, color, side);
            }

            foreach (var site in m.Sites)
            {
                var tint = site.barracks == Side.Player ? player : site.barracks == Side.Enemy ? enemy : Stone;
                for (var y = site.cell.y - 1; y <= site.cell.y + 1; y++)
                for (var x = site.cell.x - 1; x <= site.cell.x + 1; x++)
                    Fill(x, y, 0, StoneEdge, side);
                Block(site.cell.x - 1, site.cell.y - 1, 3, 2, Color32.Lerp(Stone, tint, 0.5f), side);
            }

            Base(m.PlayerBase, player, side);
            Base(m.EnemyBase, enemy, side);
            foreach (var pile in m.Piles)
                Fill(pile.cell.x, pile.cell.y, 2, pile.kind == ResourceKind.Gold ? Gold : Wood, side);
            foreach (var rock in m.Rocks)
                Fill(rock.cell.x, rock.cell.y, 1 + (2 - (int)rock.size) / 2, Rock, side);

            if (_grid)
                Grid(m.Size, side);

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        private static bool InStart(BattleMapAsset.BaseSpot spot, int x, int y)
        {
            var dx = 2 * x + 1 - 2 * spot.corner.x;
            var dy = 2 * y + 1 - 2 * spot.corner.y;
            return dx * dx + dy * dy <= 4 * spot.startRadius * spot.startRadius;
        }

        /// <summary>A base of 6 × 6 cells round its corner: the colour of its side with a dark rim.</summary>
        private void Base(BattleMapAsset.BaseSpot spot, Color32 color, int side)
        {
            var dark = Color32.Lerp(color, new Color32(0, 0, 0, 255), 0.55f);
            Block(spot.corner.x - 3, spot.corner.y - 3, 6, 0, dark, side);
            Block(spot.corner.x - 3, spot.corner.y - 3, 6, 3, color, side);
        }

        /// <summary>A square of <paramref name="cells"/> cells from a corner cell, less <paramref name="inset"/> pixels at its rim.</summary>
        private void Block(int x0, int y0, int cells, int inset, Color32 color, int side)
        {
            for (var py = y0 * CellPixels + inset; py < (y0 + cells) * CellPixels - inset; py++)
            for (var px = x0 * CellPixels + inset; px < (x0 + cells) * CellPixels - inset; px++)
            {
                if (px >= 0 && py >= 0 && px < side && py < side)
                    _pixels[py * side + px] = color;
            }
        }

        private void Fill(int x, int y, int inset, Color32 color, int side) => Block(x, y, 1, inset, color, side);

        /// <summary>A line at the edge of every cell, darker every 8 cells (4 m).</summary>
        private void Grid(int cells, int side)
        {
            for (var py = 0; py < side; py++)
            for (var px = 0; px < side; px++)
            {
                var onX = px % CellPixels == 0;
                var onY = py % CellPixels == 0;
                if (!onX && !onY)
                    continue;

                var major = (onX && px / CellPixels % 8 == 0) || (onY && py / CellPixels % 8 == 0);
                var i = py * side + px;
                _pixels[i] = Color32.Lerp(_pixels[i], new Color32(0, 0, 0, 255), major ? 0.35f : 0.14f);
            }
        }

        /// <summary>The map fills the shorter side of the window at zoom 1; the zoom and the move keep while the editor is open.</summary>
        private void Layout()
        {
            var m = _editing.Map;
            if (m == null)
                return;

            var area = viewport.rect.size;
            var fit = Mathf.Min(area.x, area.y) * 0.96f;
            var rect = map.rectTransform;
            rect.sizeDelta = Vector2.one * (fit * _zoom);
            rect.anchoredPosition = _pan;
        }

        private bool CellAt(PointerEventData data, out Vector2Int cell)
        {
            cell = default;
            var m = _editing.Map;
            var rect = map.rectTransform;
            if (m == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out var local))
                return false;

            var u = local.x / rect.rect.width + rect.pivot.x;
            var v = local.y / rect.rect.height + rect.pivot.y;
            cell = new Vector2Int(Mathf.FloorToInt(u * m.Size), Mathf.FloorToInt(v * m.Size));
            return u >= 0f && v >= 0f && u < 1f && v < 1f;
        }

        private void Press(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !CellAt(data, out var cell))
                return;

            _painting = true;
            _last = cell;
            _strokeChanged = _editing.Paint(cell.x, cell.y, _brush, _water);
            if (_strokeChanged)
                Redraw();
        }

        private void Drag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left)
            {
                _pan += data.delta / Scale();
                return;
            }

            if (!_painting)
                return;

            CellAt(data, out var cell);
            if (_editing.PaintLine(_last.x, _last.y, cell.x, cell.y, _brush, _water))
            {
                _strokeChanged = true;
                Redraw();
            }

            _last = cell;
            Move(data);
        }

        private void Release(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !_painting)
                return;

            _painting = false;
            if (!_strokeChanged)
                return;

            _editing.Commit();
            DrawTools();
        }

        /// <summary>The wheel zooms round the point under the pointer: that point stays under it.</summary>
        private void Scroll(PointerEventData data)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, data.position, data.pressEventCamera, out var local))
                return;

            local -= viewport.rect.center;
            var zoom = Mathf.Clamp(_zoom * Mathf.Pow(1.15f, Mathf.Sign(data.scrollDelta.y)), MinZoom, MaxZoom);
            _pan = local - (local - _pan) * (zoom / _zoom);
            _zoom = zoom;
            Layout();
        }

        private void Move(PointerEventData data)
        {
            var hover = CellAt(data, out var cell) ? cell : new Vector2Int(-1, -1);
            if (hover == _hover)
                return;

            _hover = hover;
            DrawStatus();
        }

        private float Scale()
        {
            var canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.scaleFactor : 1f;
        }

        // ---- messages and the dialog

        private void Say(string text)
        {
            message.text = text;
            _messageUntil = Time.unscaledTime + MessageSeconds;
        }

        /// <summary>A question with «yes», an optional «no» and «cancel»; cancel does nothing.</summary>
        private void Ask(string text, string yes, string no, Action onYes, Action onNo)
        {
            dialogText.text = text;
            dialogYesCaption.text = yes;
            dialogNo.gameObject.SetActive(no != null);
            if (no != null)
                dialogNoCaption.text = no;
            _dialogYes = onYes;
            _dialogNo = onNo;
            dialog.alpha = 1f;
            dialog.interactable = true;
            dialog.blocksRaycasts = true;
        }

        private void CloseDialog(Action then)
        {
            HideDialog();
            then?.Invoke();
        }

        private void HideDialog()
        {
            dialog.alpha = 0f;
            dialog.interactable = false;
            dialog.blocksRaycasts = false;
        }
    }
}
