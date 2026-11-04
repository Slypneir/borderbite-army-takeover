using System;
using Meta.LocalizationSystem.Services;
using Meta.ProgressSystem.Enums;
using Meta.ProgressSystem.Services;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace Meta.FlowSystem.Views
{
    /// <summary>
    /// The lasting upgrades between levels (stage 1 spec 5.2; screens/upgrades.md, «for layer 4»): the crowns on top, four cards
    /// — four in a row when the width allows 4 × 280 + 3 × 32 = 1216 units, else 2 × 2 — and the main button below, «Next»
    /// after a victory, «Again» after a defeat. A card is one target: a tap buys the next step (it swells to 1.05 for 0.15 s);
    /// short of crowns it is dull, says how many are missing and shakes; at the top it says «Max». Keys 1–4 buy, Enter or Space
    /// is the main button. The look is a placeholder of Editor/MainSceneBuilder until the kit of the interface.
    /// </summary>
    public sealed class UpgradesView : MonoBehaviour
    {
        private const float CardWidth = 280f;
        private const float CardHeight = 300f;
        private const float Gap = 32f;
        private const float WideRow = 4 * CardWidth + 3 * Gap;
        private const float DullAlpha = 0.5f;
        private const float PulseSeconds = 0.15f;
        private const float ShakeSeconds = 0.3f;

        private static readonly string[] NameKeys = { "upgrades.hero_power", "upgrades.hero_brush", "upgrades.army_power", "upgrades.start_gold" };
        private static readonly string[] EffectKeys = { "upgrades.effect.percent", "upgrades.effect.cells", "upgrades.effect.percent", "upgrades.effect.gold" };

        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform cards;
        [SerializeField] private RectTransform cardTemplate;
        [SerializeField] private Sprite[] icons = new Sprite[4];
        [SerializeField] private TMP_Text balance;
        [SerializeField] private Button play;
        [SerializeField] private RectTransform playRect;
        [SerializeField] private TMP_Text playCaption;

        private readonly Card[] _cards = new Card[4];
        private ProgressService _progress;
        private Action _onPlay;
        private bool _victory;

        public bool Shown => group.alpha > 0f;

        [Inject]
        public void Construct(ProgressService progress) => _progress = progress;

        private void Awake()
        {
            cardTemplate.gameObject.SetActive(false);
            for (var i = 0; i < _cards.Length; i++)
            {
                var root = Instantiate(cardTemplate, cards);
                root.gameObject.SetActive(true);
                root.name = ((Upgrade)i).ToString();
                var card = new Card
                {
                    Root = root,
                    Group = root.GetComponent<CanvasGroup>(),
                    Icon = root.Find("Icon").GetComponent<Image>(),
                    Name = root.Find("Name").GetComponent<TMP_Text>(),
                    Effect = root.Find("Effect").GetComponent<TMP_Text>(),
                    Step = root.Find("Step").GetComponent<TMP_Text>(),
                    Ticks = root.Find("Ticks").GetComponentsInChildren<Image>(true),
                    Price = root.Find("Price").GetComponent<TMP_Text>(),
                    PriceIcon = root.Find("PriceIcon").gameObject,
                };
                if (i < icons.Length && icons[i] != null)
                    card.Icon.sprite = icons[i];
                var upgrade = (Upgrade)i;
                root.GetComponent<Button>().onClick.AddListener(() => Tap(upgrade));
                _cards[i] = card;
            }

            play.onClick.AddListener(Play);
            Hide();
        }

        private void OnEnable() => Loc.Changed += Draw;

        private void OnDisable() => Loc.Changed -= Draw;

        public void Show(bool victory, Action onPlay)
        {
            _victory = victory;
            _onPlay = onPlay;
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
            _onPlay = null;
        }

        private void Update()
        {
            if (!Shown)
                return;

            Layout();
            Animate();
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            for (var i = 0; i < _cards.Length; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                    Tap((Upgrade)i);
            }

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                Play();
        }

        private void Play() => _onPlay?.Invoke();

        private void Tap(Upgrade upgrade)
        {
            var card = _cards[(int)upgrade];
            if (_progress.Maxed(upgrade))
                return;

            if (_progress.Buy(upgrade))
                card.PulseAt = Time.unscaledTime;
            else
                card.ShakeAt = Time.unscaledTime;
            Draw();
        }

        /// <summary>Four in a row when the canvas is wide enough, else two by two; the main button in the middle or on the right.</summary>
        private void Layout()
        {
            var width = cards.rect.width;
            var wide = width >= WideRow;
            var columns = wide ? 4 : 2;
            var rows = wide ? 1 : 2;
            var totalWidth = columns * CardWidth + (columns - 1) * Gap;
            var totalHeight = rows * CardHeight + (rows - 1) * Gap;
            for (var i = 0; i < _cards.Length; i++)
            {
                var column = i % columns;
                var row = i / columns;
                var x = -totalWidth / 2f + CardWidth / 2f + column * (CardWidth + Gap);
                var y = totalHeight / 2f - CardHeight / 2f - row * (CardHeight + Gap);
                _cards[i].Home = new Vector2(x, y);
            }

            var right = Screen.width >= Screen.height ? 1f : 0.5f;
            if (!Mathf.Approximately(playRect.anchorMin.x, right))
            {
                playRect.anchorMin = playRect.anchorMax = new Vector2(right, 0f);
                playRect.pivot = new Vector2(right, 0f);
                playRect.anchoredPosition = new Vector2(right > 0.5f ? -playRect.anchoredPosition.y : 0f, playRect.anchoredPosition.y);
            }
        }

        private void Animate()
        {
            var now = Time.unscaledTime;
            foreach (var card in _cards)
            {
                var pulse = now - card.PulseAt;
                var scale = pulse < PulseSeconds ? 1f + 0.05f * Mathf.Sin(pulse / PulseSeconds * Mathf.PI) : 1f;
                card.Root.localScale = Vector3.one * scale;
                var shake = now - card.ShakeAt;
                var offset = shake < ShakeSeconds ? Mathf.Sin(shake * 60f) * 8f * (1f - shake / ShakeSeconds) : 0f;
                card.Root.anchoredPosition = card.Home + new Vector2(offset, 0f);
            }
        }

        private void Draw()
        {
            if (_progress == null)
                return;

            balance.text = _progress.Data.Crowns.ToString();
            playCaption.text = Loc.Get(_victory ? "upgrades.next" : "upgrades.retry");
            var config = _progress.Config;
            for (var i = 0; i < _cards.Length; i++)
            {
                var upgrade = (Upgrade)i;
                var card = _cards[i];
                var step = _progress.StepOf(upgrade);
                card.Name.text = Loc.Get(NameKeys[i]);
                var amount = config.StepOf(upgrade);
                card.Effect.text = string.Format(Loc.Get(EffectKeys[i]),
                    upgrade == Upgrade.HeroPower || upgrade == Upgrade.ArmyPower ? Mathf.RoundToInt(amount * 100f).ToString() : amount.ToString("0.##"));
                card.Step.text = $"{step}/{config.Steps}";
                for (var t = 0; t < card.Ticks.Length; t++)
                    card.Ticks[t].color = t < step ? new Color(0.96f, 0.95f, 0.88f) : new Color(1f, 1f, 1f, 0.2f);

                if (_progress.Maxed(upgrade))
                {
                    card.Price.text = Loc.Get("upgrades.max");
                    card.PriceIcon.SetActive(false);
                    card.Group.alpha = 1f;
                }
                else if (_progress.CanBuy(upgrade))
                {
                    card.Price.text = _progress.PriceOf(upgrade).ToString();
                    card.PriceIcon.SetActive(true);
                    card.Group.alpha = 1f;
                }
                else
                {
                    card.Price.text = string.Format(Loc.Get("upgrades.need"), _progress.PriceOf(upgrade) - _progress.Data.Crowns);
                    card.PriceIcon.SetActive(false);
                    card.Group.alpha = DullAlpha;
                }
            }
        }

        private sealed class Card
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Icon;
            public TMP_Text Name;
            public TMP_Text Effect;
            public TMP_Text Step;
            public Image[] Ticks;
            public TMP_Text Price;
            public GameObject PriceIcon;
            public Vector2 Home;
            public float PulseAt = -10f;
            public float ShakeAt = -10f;
        }
    }
}
