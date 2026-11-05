using System.Collections.Generic;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Services;
using Meta.BattleSystem.Utils;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// Own land on the screen (task 4.33; battle-marks card «Своя земля: щит, блок, пыль»; spec 2.4a; art request 14):
    /// - the shield of the player's hero, 40 units at the left end of its health bar, overlapping it by 6: it appears (0.28 s,
    ///   scale 0 → 1.25 → 1, a white flash, a cream ring 20 → 52, 6 voxels) when the hero has stood 0.25 s on its own land, and
    ///   breaks (0.45 s, a flash, then 4 shards by the cross fly out from their places — the upper ones up and aside, the lower
    ///   ones down and aside — turning about their centres of mass) when it has left it as long; a change sooner than 1 s after the last is
    ///   quiet (0.15 s, scale or melt, no ring, flash or shards). The rules count the shield by the cell at once, only the mark waits;
    /// - a blocked blow (EventKind.Shielded): instead of the red flash and «−1», a blue shield in front of the unit at its chest,
    ///   46 for the hero, 34 for a soldier, growing 0.6 → 1.1 and fading in 0.3 s, a light ring 16 → 46 and 5 voxels; the shield
    ///   of the hero at its bar jumps to 1.3 for 0.24 s and flashes white.
    /// Lives right under the canvas, anchored to its lower left corner; canvas units are pixels / (short side / 720).
    /// </summary>
    public sealed class OwnLandView : MonoBehaviour
    {
        private const float HeroBarHeight = 2.15f;
        private const float HeroBarHalf = 0.7f;
        private const float ChestHeight = 1f;
        private const float IconSize = 40f;
        private const float Overlap = 6f;
        private const float Settle = 0.25f;
        private const float Calm = 1f;
        private const float AppearSeconds = 0.28f;
        private const float BreakSeconds = 0.45f;
        private const float QuietSeconds = 0.15f;
        private const float BumpSeconds = 0.24f;
        private const float BlockSeconds = 0.3f;
        private static readonly Color Cream = new Color(0.96f, 0.95f, 0.88f, 1f);
        private static readonly Color LightBlue = new Color(0.72f, 0.84f, 1f, 1f);

        [Tooltip("The shield of the hero at its bar: Root (scaled), Flash (white over it), Ring, Shards (4 children) and Voxels (6).")]
        [SerializeField] private RectTransform icon;
        [Tooltip("A blocked blow: Shield (scaled, with Flash), Ring and Voxels (5); copied as many as are needed.")]
        [SerializeField] private RectTransform blockTemplate;

        private readonly List<Block> _blocks = new List<Block>();
        private IBattleDriver _battle;
        private Camera _camera;
        private HeroView _heroes;
        private SoldiersView _soldiers;
        private EventCursor _events;
        private object _simulation;

        private RectTransform _shield;
        private Image _flash;
        private Image _ring;
        private readonly List<RectTransform> _shards = new List<RectTransform>();
        private readonly List<Vector2> _shardRests = new List<Vector2>();
        private readonly List<RectTransform> _voxels = new List<RectTransform>();
        private bool _shown;
        private bool _candidate;
        private float _candidateSince;
        private float _lastChange = -10f;
        private float _animationAt = -10f;
        private bool _appearing;
        private bool _quiet;
        private float _bumpAt = -10f;

        [Inject]
        public void Construct(IBattleDriver battle, Camera mainCamera, HeroView heroes, SoldiersView soldiers)
        {
            _battle = battle;
            _camera = mainCamera;
            _heroes = heroes;
            _soldiers = soldiers;
        }

        private void Awake()
        {
            _shield = (RectTransform)icon.Find("Shield");
            _flash = _shield.Find("Flash").GetComponent<Image>();
            _ring = icon.Find("Ring").GetComponent<Image>();
            foreach (Transform shard in icon.Find("Shards"))
            {
                _shards.Add((RectTransform)shard);
                _shardRests.Add(((RectTransform)shard).anchoredPosition);
            }
            foreach (Transform voxel in icon.Find("Voxels"))
                _voxels.Add((RectTransform)voxel);
            blockTemplate.gameObject.SetActive(false);
            icon.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            var simulation = _battle?.Current?.Simulation;
            if (simulation == null)
            {
                icon.gameObject.SetActive(false);
                return;
            }

            var now = Time.time;
            if (_simulation != simulation)
            {
                _simulation = simulation;
                _shown = false;
                _candidate = false;
                _lastChange = -10f;
                _animationAt = -10f;
                foreach (var block in _blocks)
                    block.Root.gameObject.SetActive(false);
            }

            var scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height)) / 720f;
            _events.Read(simulation, e =>
            {
                if (e.Kind == EventKind.Shielded)
                    Blocked(e, now);
            });

            var index = simulation.Heroes.Count > 0 ? IndexOfPlayer(simulation) : -1;
            var hero = index >= 0 ? simulation.Heroes[index] : default;
            var own = index >= 0 && hero.Alive && simulation.Grid.Inside(hero.X / BattleSettings.UnitsPerCell, hero.Y / BattleSettings.UnitsPerCell) &&
                      simulation.Grid.At(hero.X / BattleSettings.UnitsPerCell, hero.Y / BattleSettings.UnitsPerCell) == Side.Player;
            Follow(own, index >= 0 && hero.Alive, now);
            DrawIcon(index >= 0 && hero.Alive, scale, now);
            DrawBlocks(scale, now);
        }

        private static int IndexOfPlayer(Core.BattleSystem.Services.BattleSimulation simulation)
        {
            for (var i = 0; i < simulation.Heroes.Count; i++)
            {
                if (simulation.Heroes[i].Side == Side.Player)
                    return i;
            }

            return -1;
        }

        /// <summary>The mark follows the land under the hero once it has stayed so 0.25 s; a dead hero has none at once.</summary>
        private void Follow(bool own, bool alive, float now)
        {
            if (!alive)
            {
                _shown = false;
                _candidate = false;
                return;
            }

            if (own != _candidate)
            {
                _candidate = own;
                _candidateSince = now;
            }

            if (_candidate == _shown || now - _candidateSince < Settle)
                return;

            _quiet = now - _lastChange < Calm;
            _shown = _candidate;
            _appearing = _shown;
            _lastChange = now;
            _animationAt = now;
            if (!_quiet)
                Scatter(_appearing);
        }

        private void DrawIcon(bool alive, float scale, float now)
        {
            var age = now - _animationAt;
            var duration = _quiet ? QuietSeconds : _appearing ? AppearSeconds : BreakSeconds;
            var animating = age < duration;
            if (!alive || (!_shown && !animating))
            {
                icon.gameObject.SetActive(false);
                return;
            }

            icon.gameObject.SetActive(true);
            // at the left end of the bar, overlapping it by 6, at the middle of its height
            var right = _camera.transform.right;
            var end = _heroes.PositionOf(Side.Player) + Vector3.up * HeroBarHeight - right * HeroBarHalf;
            var point = (Vector2)_camera.WorldToScreenPoint(end) / scale;
            icon.anchoredPosition = point + new Vector2(-IconSize / 2f + Overlap, 0f);

            var size = 1f;
            var alpha = 1f;
            var flash = 0f;
            if (animating && _appearing)
            {
                var t = age / duration;
                size = _quiet ? Mathf.Lerp(0.6f, 1f, t) : t < 0.6f ? Mathf.Lerp(0f, 1.25f, t / 0.6f) : Mathf.Lerp(1.25f, 1f, (t - 0.6f) / 0.4f);
                flash = !_quiet && age < 0.08f ? 1f : 0f;
            }
            else if (animating)
            {
                // the break: a flash, then the shards carry it away; the quiet one melts
                alpha = _quiet ? 1f - age / duration : age < 0.05f ? 1f : 0f;
                flash = !_quiet && age < 0.05f ? 1f : 0f;
            }

            var bump = now - _bumpAt;
            if (bump < BumpSeconds)
            {
                size *= 1f + 0.3f * Mathf.Sin(bump / BumpSeconds * Mathf.PI);
                flash = Mathf.Max(flash, 1f - bump / BumpSeconds);
            }

            _shield.localScale = Vector3.one * size;
            SetAlpha(_shield, alpha);
            _flash.color = new Color(1f, 1f, 1f, flash * alpha);

            // the ring of the appearance, the voxels of it, the shards of the break
            var ringT = animating && !_quiet && _appearing ? age / duration : 1f;
            _ring.enabled = ringT < 1f;
            _ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(20f, 52f, ringT);
            _ring.color = new Color(Cream.r, Cream.g, Cream.b, 1f - ringT);
            var piecesT = animating && !_quiet ? age / duration : 1f;
            foreach (var voxel in _voxels)
                Fly(voxel, piecesT, _appearing);
            for (var i = 0; i < _shards.Count; i++)
                Shatter(_shards[i], _shardRests[i], animating && !_quiet && !_appearing ? age : -1f, duration);
        }

        /// <summary>
        /// A shard of the break: hidden during the flash (the whole shield shows it), then out from its place along the line from
        /// the middle of the shield, turning outward and falling a little, fading over the second half.
        /// </summary>
        private static void Shatter(RectTransform shard, Vector2 rest, float age, float duration)
        {
            var active = age >= 0.05f && age < duration;
            shard.gameObject.SetActive(active);
            if (!active)
                return;

            var t = age / duration;
            var away = 1f - (1f - t) * (1f - t);
            var direction = rest.sqrMagnitude > 0.01f ? rest.normalized : Vector2.up;
            shard.anchoredPosition = rest + direction * (34f * away) + Vector2.down * (14f * t * t);
            shard.localEulerAngles = new Vector3(0f, 0f, (rest.x < 0f ? 1f : -1f) * 50f * away);
            var image = shard.GetComponent<Image>();
            var color = image.color;
            color.a = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f;
            image.color = color;
        }

        /// <summary>The voxels of an appearance start from the middle of the shield, each along its own turn.</summary>
        private void Scatter(bool appearing)
        {
            if (!appearing)
                return;

            for (var i = 0; i < _voxels.Count; i++)
                _voxels[i].localEulerAngles = new Vector3(0f, 0f, i * 60f + 30f);
        }

        /// <summary>A voxel out from the middle along its turn, down with time, fading.</summary>
        private static void Fly(RectTransform piece, float t, bool active)
        {
            piece.gameObject.SetActive(active && t < 1f);
            if (!piece.gameObject.activeSelf)
                return;

            var angle = piece.localEulerAngles.z * Mathf.Deg2Rad;
            var out_ = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (8f + 30f * t);
            piece.anchoredPosition = out_ + Vector2.down * (18f * t * t);
            var image = piece.GetComponent<Image>();
            var color = image.color;
            color.a = 1f - t;
            image.color = color;
        }

        private static void SetAlpha(RectTransform root, float alpha)
        {
            var group = root.GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = alpha;
        }

        // ---- blocked blows

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>The developer shows a block at the player's hero (id −1) or a soldier, for a screenshot of the look.</summary>
        public void ShowBlock(int id) =>
            Blocked(new BattleEvent(0, EventKind.Shielded, Side.Player, id, 0, 0, 0), Time.time);
#endif

        private void Blocked(BattleEvent battleEvent, float now)
        {
            var hero = battleEvent.Id < 0;
            Vector3 world;
            if (hero)
            {
                world = _heroes.PositionOf(battleEvent.Side);
                _bumpAt = now;
            }
            else if (!_soldiers.TryGetPosition(battleEvent.Id, out world))
                return;

            var block = Take();
            block.World = world + Vector3.up * ChestHeight;
            block.Size = hero ? 46f : 34f;
            block.At = now;
            block.Root.gameObject.SetActive(true);
            for (var i = 0; i < block.Voxels.Count; i++)
                block.Voxels[i].localEulerAngles = new Vector3(0f, 0f, i * 72f + 18f);
        }

        private Block Take()
        {
            foreach (var free in _blocks)
            {
                if (!free.Root.gameObject.activeSelf)
                    return free;
            }

            var root = Instantiate(blockTemplate, transform);
            var block = new Block
            {
                Root = root,
                Shield = (RectTransform)root.Find("Shield"),
                Ring = root.Find("Ring").GetComponent<Image>(),
            };
            foreach (Transform voxel in root.Find("Voxels"))
                block.Voxels.Add((RectTransform)voxel);
            _blocks.Add(block);
            return block;
        }

        private void DrawBlocks(float scale, float now)
        {
            foreach (var block in _blocks)
            {
                if (!block.Root.gameObject.activeSelf)
                    continue;

                var t = (now - block.At) / BlockSeconds;
                if (t >= 1f)
                {
                    block.Root.gameObject.SetActive(false);
                    continue;
                }

                // in front of the unit: a little towards the camera, down on the screen
                block.Root.anchoredPosition = (Vector2)_camera.WorldToScreenPoint(block.World) / scale + Vector2.down * 6f;
                block.Shield.sizeDelta = Vector2.one * block.Size;
                block.Shield.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.1f, Mathf.Min(1f, t * 1.6f));
                SetAlpha(block.Shield, 1f - t * t);
                block.Ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(16f, 46f, t);
                block.Ring.color = new Color(LightBlue.r, LightBlue.g, LightBlue.b, 1f - t);
                foreach (var voxel in block.Voxels)
                    Fly(voxel, t, true);
            }
        }

        private sealed class Block
        {
            public RectTransform Root;
            public RectTransform Shield;
            public Image Ring;
            public readonly List<RectTransform> Voxels = new List<RectTransform>();
            public Vector3 World;
            public float Size;
            public float At;
        }
    }
}
