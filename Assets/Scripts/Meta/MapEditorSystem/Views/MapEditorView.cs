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
    /// list of maps (a cross on a row deletes that map), «New» — a sea of 80 by 80 cells, the island is painted on it (owner,
    /// 5 November) — «copy» and the name; the brushes of land and water and the eraser, square or round, 1–15 cells by a slider;
    /// the objects put by a click: gold, wood, a place to build, the base of either side; the mirror through the centre; the grid,
    /// «save», «undo», «redo», «play» and «menu». In the middle the map from above, a block of texels per cell, and under the
    /// pointer what the tool would touch; below a line of the share of land, the places, the piles and the distance between the
    /// bases. The wheel zooms round the pointer, the middle or the right button moves the view. Ctrl+Z / Ctrl+Y undo and redo,
    /// Ctrl+S saves. Leaving a map with changes asks «Save?». Only the owner uses it, in the Unity editor: the words are Russian
    /// and not in the tables of the languages.
    /// </summary>
    public sealed class MapEditorView : MonoBehaviour
    {
        public const int NewMapCells = 80;
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
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        // not yellow: yellow is gold, a signal of the game (borderbite-ui STYLE §3)
        private static readonly Color Selected = new Color(0.62f, 0.86f, 0.62f, 1f);
        private static readonly Color Normal = new Color(0.92f, 0.93f, 0.95f, 1f);

        private enum Tool
        {
            Land,
            Water,
            Eraser,
            Gold,
            Wood,
            Site,
            PlayerBase,
            EnemyBase,
        }

        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RawImage map;
        [SerializeField] private MapViewportInput input;
        [SerializeField] private RectTransform listContent;
        [Tooltip("A row of the list: the button of the map with its «Label» and a small button «Delete» that deletes it.")]
        [SerializeField] private Button itemTemplate;
        [SerializeField] private TMP_InputField nameField;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text message;

        [Header("Maps")]
        [SerializeField] private Button create;
        [SerializeField] private Button copy;
        [SerializeField] private Button rename;

        [Header("Tools")]
        [SerializeField] private Button land;
        [SerializeField] private Button water;
        [SerializeField] private Button eraser;
        [SerializeField] private Button square;
        [SerializeField] private Button circle;
        [SerializeField] private Slider brushSize;
        [SerializeField] private TMP_Text brushLabel;
        [SerializeField] private Button gold;
        [SerializeField] private Button wood;
        [SerializeField] private Button site;
        [SerializeField] private Button playerBase;
        [SerializeField] private Button enemyBase;
        [SerializeField] private Button mirror;
        [SerializeField] private Button grid;

        [Header("Actions")]
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
        private Tool _tool = Tool.Land;
        private MapEditing.Shape _shape = MapEditing.Shape.Circle;
        private int _brush = 5;
        private bool _mirror;
        private bool _grid = true;
        private float _zoom = 1f;
        private Vector2 _pan;
        private bool _painting;
        private bool _strokeChanged;
        private Vector2Int _last;
        private Vector2Int _hover = new Vector2Int(-1, -1);
        private Vector2Int _hoverCorner;
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
            create.onClick.AddListener(() => Guard(() => Open(_library.Create(NewMapCells))));
            copy.onClick.AddListener(() => Guard(() => Open(_library.Copy(_editing.Name))));
            rename.onClick.AddListener(Rename);
            land.onClick.AddListener(() => SetTool(Tool.Land));
            water.onClick.AddListener(() => SetTool(Tool.Water));
            eraser.onClick.AddListener(() => SetTool(Tool.Eraser));
            gold.onClick.AddListener(() => SetTool(Tool.Gold));
            wood.onClick.AddListener(() => SetTool(Tool.Wood));
            site.onClick.AddListener(() => SetTool(Tool.Site));
            playerBase.onClick.AddListener(() => SetTool(Tool.PlayerBase));
            enemyBase.onClick.AddListener(() => SetTool(Tool.EnemyBase));
            square.onClick.AddListener(() => SetShape(MapEditing.Shape.Square));
            circle.onClick.AddListener(() => SetShape(MapEditing.Shape.Circle));
            brushSize.minValue = 1;
            brushSize.maxValue = MapEditing.MostBrush;
            brushSize.wholeNumbers = true;
            brushSize.SetValueWithoutNotify(_brush);
            brushSize.onValueChanged.AddListener(value =>
            {
                _brush = Mathf.RoundToInt(value);
                DrawTools();
                Redraw();
            });
            mirror.onClick.AddListener(() =>
            {
                _mirror = !_mirror;
                DrawTools();
                Redraw();
            });
            grid.onClick.AddListener(() =>
            {
                _grid = !_grid;
                DrawTools();
                Redraw();
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

        /// <summary>Opens the editor on the map it had before the menu, else on the map of the test level.</summary>
        public void Show(Action onExit, Action<BattleMapAsset> onPlay)
        {
            _onExit = onExit;
            _onPlay = onPlay;
            if (_editing.Map == null)
                Open(_lastName != null && _library.Load(_lastName) != null ? _lastName : AnyMap());

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

        private string AnyMap()
        {
            var names = _library.Names();
            return _library.TestMap ?? (names.Count > 0 ? names[0] : _library.Create(NewMapCells));
        }

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

        /// <summary>To the main menu: changes not saved are dropped (the question came before), the map opens again next time.</summary>
        private void Exit()
        {
            _lastName = _editing.Name;
            _editing.Close();
            _onExit?.Invoke();
        }

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

        /// <summary>The cross on a row of the list: asks, deletes the file; the open map deleted — another opens.</summary>
        private void Remove(string name)
        {
            if (name == _library.TestMap)
            {
                Say("это карта тестового уровня — её не удалить");
                return;
            }

            Ask($"Удалить карту «{name}»? Файл пропадёт из проекта.", "Удалить", null, () =>
            {
                var error = _library.Delete(name);
                if (error != null)
                {
                    Say(error);
                    return;
                }

                if (name == _editing.Name)
                {
                    _editing.Close();
                    Open(AnyMap());
                }
                else
                    RefreshList();

                Say($"«{name}» удалена");
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
                var test = name == _library.TestMap;
                item.transform.Find("Label").GetComponent<TMP_Text>().text = test ? name + "  (тестовый уровень)" : name;
                ((Image)item.targetGraphic).color = name == _editing.Name ? Selected : Normal;
                var picked = name;
                item.onClick.AddListener(() =>
                {
                    if (picked != _editing.Name)
                        Guard(() => Open(picked));
                });
                var delete = item.transform.Find("Delete").GetComponent<Button>();
                delete.gameObject.SetActive(!test);
                delete.onClick.AddListener(() => Remove(picked));
                _items.Add(item);
            }
        }

        // ---- tools

        private void SetTool(Tool tool)
        {
            _tool = tool;
            DrawTools();
            Redraw();
        }

        private void SetShape(MapEditing.Shape shape)
        {
            _shape = shape;
            DrawTools();
            Redraw();
        }

        private bool Brushed => _tool == Tool.Land || _tool == Tool.Water || _tool == Tool.Eraser;

        private void DrawTools()
        {
            Mark(land, _tool == Tool.Land);
            Mark(water, _tool == Tool.Water);
            Mark(eraser, _tool == Tool.Eraser);
            Mark(gold, _tool == Tool.Gold);
            Mark(wood, _tool == Tool.Wood);
            Mark(site, _tool == Tool.Site);
            Mark(playerBase, _tool == Tool.PlayerBase);
            Mark(enemyBase, _tool == Tool.EnemyBase);
            Mark(square, _shape == MapEditing.Shape.Square);
            Mark(circle, _shape == MapEditing.Shape.Circle);
            Mark(mirror, _mirror);
            Mark(grid, _grid);
            brushLabel.text = $"Кисть: {_brush}";
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

            var landShare = Mathf.RoundToInt(m.LandCells() * 100f / (m.Size * m.Size));
            var meters = Vector2Int.Distance(m.PlayerBase.corner, m.EnemyBase.corner) * _rules.CellSize;
            var golds = 0;
            foreach (var pile in m.Piles)
            {
                if (pile.kind == ResourceKind.Gold)
                    golds++;
            }

            // no «×» and «·» in the font: words and bars
            var hover = _hover.x >= 0 ? $"   |   клетка {_hover.x}, {_hover.y}" : string.Empty;
            status.text = $"суша {landShare}%   |   мест {m.Sites.Count}   |   золото {golds}, дерево {m.Piles.Count - golds}   |   " +
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

            foreach (var spot in m.Sites)
            {
                var tint = spot.barracks == Side.Player ? player : spot.barracks == Side.Enemy ? enemy : Stone;
                Block(spot.cell.x - 1, spot.cell.y - 1, 3, 0, StoneEdge, side);
                Block(spot.cell.x - 1, spot.cell.y - 1, 3, 2, Color32.Lerp(Stone, tint, 0.5f), side);
            }

            Base(m.PlayerBase.corner, player, side);
            Base(m.EnemyBase.corner, enemy, side);
            foreach (var pile in m.Piles)
                Fill(pile.cell.x, pile.cell.y, 1, pile.kind == ResourceKind.Gold ? Gold : Wood, side);
            foreach (var rock in m.Rocks)
                Fill(rock.cell.x, rock.cell.y, 1 + (2 - (int)rock.size) / 2, Rock, side);

            if (_grid)
                Grid(side);
            if (_hover.x >= 0 && !_painting)
                Highlight(side);

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        /// <summary>What the tool would touch under the pointer, lightened, and its mirror with the mirror on.</summary>
        private void Highlight(int side)
        {
            void Area(Vector2Int from, int cells)
            {
                for (var y = from.y; y < from.y + cells; y++)
                for (var x = from.x; x < from.x + cells; x++)
                    Light(x, y, side);
            }

            void Spot(Vector2Int cell)
            {
                switch (_tool)
                {
                    case Tool.Gold:
                    case Tool.Wood:
                        Area(cell, 1);
                        break;
                    case Tool.Site:
                        Area(cell - Vector2Int.one, 3);
                        break;
                    default:
                        foreach (var c in MapEditing.Brush(cell, _brush, _shape))
                            Light(c.x, c.y, side);
                        break;
                }
            }

            if (_tool == Tool.PlayerBase || _tool == Tool.EnemyBase)
            {
                Area(_hoverCorner - new Vector2Int(3, 3), 6);
                if (_mirror)
                    Area(new Vector2Int(_editing.Map.Size, _editing.Map.Size) - _hoverCorner - new Vector2Int(3, 3), 6);
                return;
            }

            Spot(_hover);
            if (_mirror)
                Spot(_editing.Mirror(_hover));
        }

        private void Light(int x, int y, int side)
        {
            for (var py = y * CellPixels; py < (y + 1) * CellPixels; py++)
            for (var px = x * CellPixels; px < (x + 1) * CellPixels; px++)
            {
                if (px >= 0 && py >= 0 && px < side && py < side)
                    _pixels[py * side + px] = Color32.Lerp(_pixels[py * side + px], White, 0.4f);
            }
        }

        private static bool InStart(BattleMapAsset.BaseSpot spot, int x, int y)
        {
            var dx = 2 * x + 1 - 2 * spot.corner.x;
            var dy = 2 * y + 1 - 2 * spot.corner.y;
            return dx * dx + dy * dy <= 4 * spot.startRadius * spot.startRadius;
        }

        /// <summary>A base of 6 by 6 cells round its corner: the colour of its side with a dark rim.</summary>
        private void Base(Vector2Int corner, Color32 color, int side)
        {
            var dark = Color32.Lerp(color, new Color32(0, 0, 0, 255), 0.55f);
            Block(corner.x - 3, corner.y - 3, 6, 0, dark, side);
            Block(corner.x - 3, corner.y - 3, 6, 3, color, side);
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
        private void Grid(int side)
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

        /// <summary>The map fills the shorter side of the window at zoom 1; the zoom and the move keep while the map is open.</summary>
        private void Layout()
        {
            if (_editing.Map == null)
                return;

            var area = viewport.rect.size;
            var fit = Mathf.Min(area.x, area.y) * 0.96f;
            var rect = map.rectTransform;
            rect.sizeDelta = Vector2.one * (fit * _zoom);
            rect.anchoredPosition = _pan;
        }

        /// <summary>The cell under the pointer, and the corner of four cells nearest to it (where a base goes).</summary>
        private bool CellAt(PointerEventData data, out Vector2Int cell, out Vector2Int corner)
        {
            cell = default;
            corner = default;
            var m = _editing.Map;
            var rect = map.rectTransform;
            if (m == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out var local))
                return false;

            var u = (local.x / rect.rect.width + rect.pivot.x) * m.Size;
            var v = (local.y / rect.rect.height + rect.pivot.y) * m.Size;
            cell = new Vector2Int(Mathf.FloorToInt(u), Mathf.FloorToInt(v));
            corner = new Vector2Int(Mathf.RoundToInt(u), Mathf.RoundToInt(v));
            return u >= 0f && v >= 0f && u < m.Size && v < m.Size;
        }

        private void Press(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !CellAt(data, out var cell, out var corner))
                return;

            if (!Brushed)
            {
                Put(cell, corner);
                return;
            }

            _painting = true;
            _last = cell;
            _strokeChanged = Stroke(cell, cell);
            Redraw();
        }

        /// <summary>A click of an object tool: it is put (the base moved) and the step made at once, or the reason why not is said.</summary>
        private void Put(Vector2Int cell, Vector2Int corner)
        {
            var error = _tool switch
            {
                Tool.Gold => _editing.PlacePile(ResourceKind.Gold, cell, _mirror),
                Tool.Wood => _editing.PlacePile(ResourceKind.Wood, cell, _mirror),
                Tool.Site => _editing.PlaceSite(cell, _mirror),
                Tool.PlayerBase => _editing.PlaceBase(Side.Player, corner, _mirror),
                _ => _editing.PlaceBase(Side.Enemy, corner, _mirror),
            };
            if (error != null)
            {
                Say(error);
                return;
            }

            _editing.Commit();
            Changed();
        }

        private bool Stroke(Vector2Int from, Vector2Int to) =>
            _tool == Tool.Eraser
                ? _editing.EraseLine(from, to, _brush, _shape, _mirror)
                : _editing.PaintLine(from, to, _brush, _tool == Tool.Water, _shape, _mirror);

        private void Drag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left)
            {
                _pan += data.delta / Scale();
                return;
            }

            if (!_painting)
                return;

            CellAt(data, out var cell, out _);
            if (Stroke(_last, cell))
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
            if (_strokeChanged)
                _editing.Commit();
            Changed();
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
            var inside = CellAt(data, out var cell, out var corner);
            var hover = inside ? cell : new Vector2Int(-1, -1);
            if (hover == _hover && corner == _hoverCorner)
                return;

            _hover = hover;
            _hoverCorner = corner;
            DrawStatus();
            if (!_painting)
                Redraw();
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
