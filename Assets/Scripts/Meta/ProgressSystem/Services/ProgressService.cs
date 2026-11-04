using System;
using System.Threading;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;
using Core.PlatformSystem.Interfaces;
using Cysharp.Threading.Tasks;
using Meta.ProgressSystem.Configuration;
using Meta.ProgressSystem.Data;
using Meta.ProgressSystem.Enums;
using UnityEngine;

namespace Meta.ProgressSystem.Services
{
    /// <summary>
    /// The progress of the player (stage 1 spec 5.2, 5.3): crowns for a level, buying the steps of the upgrades and what they do
    /// to the next battle, and the save through the platform right after the results and after every purchase. A save never
    /// holds the screen up: it runs in the background and tries again a few times when it fails, without a window.
    /// </summary>
    public sealed class ProgressService : IDisposable
    {
        private const int SaveTries = 3;

        private readonly IPlatform _platform;
        private readonly ProgressConfig _config;
        private readonly CancellationTokenSource _life = new CancellationTokenSource();
        private bool _saving;
        private bool _again;

        public ProgressService(IPlatform platform, ProgressConfig config)
        {
            _platform = platform;
            _config = config;
        }

        public ProgressData Data { get; private set; } = new ProgressData();

        public ProgressConfig Config => _config;

        /// <summary>Reads the save given by the platform; a missing or broken one starts anew.</summary>
        public void Load(string json)
        {
            ProgressData data = null;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    data = JsonUtility.FromJson<ProgressData>(json);
                }
                catch (ArgumentException)
                {
                    Debug.LogWarning("[Progress] broken save, starting anew");
                }
            }

            data ??= new ProgressData();
            if (data.Upgrades == null || data.Upgrades.Length != 4)
                Array.Resize(ref data.Upgrades, 4);
            data.Level = Mathf.Max(1, data.Level);
            Data = data;
            Debug.Log($"[Progress] level {Data.Level}, crowns {Data.Crowns}, upgrades {string.Join(",", Data.Upgrades)}, hint {Data.HintStep}");
        }

        /// <summary>The crowns a result gives and adds them; saved at once.</summary>
        public int Award(BattleResult result)
        {
            var crowns = _config.CrownsFor(result == BattleResult.Victory, Data.Level);
            Data.Crowns += crowns;
            Save();
            return crowns;
        }

        public int StepOf(Upgrade upgrade) => Data.Upgrades[(int)upgrade];

        public bool Maxed(Upgrade upgrade) => StepOf(upgrade) >= _config.Steps;

        /// <summary>The price of the next step, 0 when there is none.</summary>
        public int PriceOf(Upgrade upgrade) => Maxed(upgrade) ? 0 : _config.PriceOf(StepOf(upgrade) + 1);

        public bool CanBuy(Upgrade upgrade) => !Maxed(upgrade) && Data.Crowns >= PriceOf(upgrade);

        public bool Buy(Upgrade upgrade)
        {
            if (!CanBuy(upgrade))
                return false;

            Data.Crowns -= PriceOf(upgrade);
            Data.Upgrades[(int)upgrade]++;
            Save();
            return true;
        }

        public void SetHintStep(int step)
        {
            if (step <= Data.HintStep)
                return;

            Data.HintStep = step;
            Save();
        }

        /// <summary>The upgrades act from the start of a level: the player's hero, its brush, its soldiers, its start gold.</summary>
        public void Apply(BattleSettings settings)
        {
            settings.PlayerHeroBonusHealth += _config.HeroPowerStep * StepOf(Upgrade.HeroPower);
            settings.PlayerBrushBonusUnits += Mathf.RoundToInt(_config.HeroBrushStep * BattleSettings.UnitsPerCell) * StepOf(Upgrade.HeroBrush);
            settings.PlayerSoldierBonusHealth += _config.ArmyPowerStep * StepOf(Upgrade.ArmyPower);
            settings.PlayerStartGold += _config.StartGoldStep * StepOf(Upgrade.StartGold);
        }

        public void Dispose()
        {
            _life.Cancel();
            _life.Dispose();
        }

        /// <summary>Saves in the background; a save asked for while one runs follows it.</summary>
        public void Save()
        {
            if (_saving)
            {
                _again = true;
                return;
            }

            SaveAsync(_life.Token).Forget();
        }

        private async UniTaskVoid SaveAsync(CancellationToken ct)
        {
            _saving = true;
            try
            {
                do
                {
                    _again = false;
                    var json = JsonUtility.ToJson(Data);
                    for (var i = 0; i < SaveTries; i++)
                    {
                        if (await _platform.SaveAsync(json, ct))
                            break;

                        Debug.LogWarning($"[Progress] save failed, try {i + 1} of {SaveTries}");
                        await UniTask.Delay(TimeSpan.FromSeconds(1 + i), DelayType.Realtime, PlayerLoopTiming.Update, ct);
                    }
                }
                while (_again && !ct.IsCancellationRequested);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _saving = false;
            }
        }
    }
}
