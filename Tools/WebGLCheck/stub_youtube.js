// A fake ytgame for the check of a Playables build outside YouTube (the real SDK does nothing when served locally).
// Records the order of the calls, plays pause, resume, mute and a save, and prints [Stub] lines for cdp_check.mjs.
(function () {
  var calls = [];
  var pauseCb, resumeCb, audioCb;
  var note = function (name) { calls.push(name); console.log('[Stub] ' + name); };
  var frames = function () { return window.__frames || 0; };
  (function count() { window.__frames = frames() + 1; requestAnimationFrame(count); })();

  var script = function () {
    setTimeout(function () { audioCb(false); note('sent audio off'); }, 500);
    setTimeout(function () { audioCb(true); note('sent audio on'); }, 1000);
    setTimeout(function () {
      pauseCb();
      note('sent pause, main loop paused');
    }, 1500);
    setTimeout(function () {
      resumeCb();
      note('sent resume');
      window.ytgame.game.saveData('{"stub":1}').then(function () { note('saveData resolved'); });
    }, 3000);
    setTimeout(function () { console.log('[Stub] done, calls: ' + calls.join(', ')); }, 3500);
  };

  window.ytgame = {
    IN_PLAYABLES_ENV: true,
    SDK_VERSION: 'stub',
    game: {
      firstFrameReady: function () { note('firstFrameReady'); },
      gameReady: function () { note('gameReady'); script(); },
      loadData: function () { note('loadData'); return Promise.resolve(''); },
      saveData: function () { note('saveData'); return Promise.resolve(); },
    },
    system: {
      getLanguage: function () { note('getLanguage'); return Promise.resolve('ru-RU'); },
      isAudioEnabled: function () { note('isAudioEnabled'); return true; },
      onAudioEnabledChange: function (cb) { audioCb = cb; return function () {}; },
      onPause: function (cb) { pauseCb = cb; return function () {}; },
      onResume: function (cb) { resumeCb = cb; return function () {}; },
    },
    ads: {
      requestInterstitialAd: function () { note('requestInterstitialAd'); return Promise.resolve(); },
      requestRewardedAd: function () { note('requestRewardedAd'); return Promise.resolve(true); },
    },
    engagement: { sendScore: function () { return Promise.resolve(); } },
    health: { logError: function () {}, logWarning: function () {} },
  };
})();
