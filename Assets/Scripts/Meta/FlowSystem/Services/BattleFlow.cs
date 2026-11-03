using System;
using System.Threading;
using Core.BattleSystem.Enums;
using Core.InputManagement.Interfaces;
using Cysharp.Threading.Tasks;
using Meta.BattleSystem.Interfaces;
using Meta.FlowSystem.Views;
using UnityEngine;

namespace Meta.FlowSystem.Services
{
    /// <summary>
    /// Straight into the battle after the boot, no main menu; the end stops the battle and 1 s later shows the results, whose
    /// «Again» starts the level anew (stage 1 spec 2.6). The input is off while the results are shown.
    /// </summary>
    public sealed class BattleFlow : IDisposable
    {
        private const float ResultsDelaySeconds = 1f;
        private const uint Seed = 1;

        private readonly IBattleDriver _battle;
        private readonly IMoveInput _input;
        private readonly ResultsView _results;
        private readonly CancellationTokenSource _life = new CancellationTokenSource();

        public BattleFlow(IBattleDriver battle, IMoveInput input, ResultsView results)
        {
            _battle = battle;
            _input = input;
            _results = results;
            _battle.OnFinished += Finish;
        }

        public void Begin()
        {
            _results.Hide();
            _input.Enabled = true;
            _battle.StartBattle(Seed);
        }

        public void Dispose()
        {
            _battle.OnFinished -= Finish;
            _life.Cancel();
            _life.Dispose();
        }

        private void Finish(BattleResult result)
        {
            _input.Enabled = false;
            ShowResultsAsync(result, _life.Token).Forget();
        }

        private async UniTaskVoid ShowResultsAsync(BattleResult result, CancellationToken ct)
        {
            // game time: a pause of the platform holds the results back too
            await UniTask.Delay(TimeSpan.FromSeconds(ResultsDelaySeconds), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
            Debug.Log($"[BattleFlow] results: {result}");
            _results.Show(result, Begin);
        }
    }
}
