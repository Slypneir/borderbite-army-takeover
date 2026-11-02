// A fake YaGames for the check of a Yandex build outside the site (/sdk.js exists only there).
// Records the order of the calls, plays pause, resume, a save and both adverts through the page, prints [Stub] lines for cdp_check.mjs.
(function () {
  var calls = [];
  var handlers = {};
  var data = {};
  var note = function (name) { calls.push(name); console.log('[Stub] ' + name); };
  var ysdk = {
    environment: { i18n: { lang: 'ru' } },
    features: {
      LoadingAPI: {
        ready: function () {
          note('LoadingAPI.ready');
          setTimeout(function () { handlers.game_api_pause(); note('sent game_api_pause'); }, 500);
          setTimeout(function () { handlers.game_api_resume(); note('sent game_api_resume'); }, 1000);
          setTimeout(function () { window.dispatchEvent(new Event('blur')); note('sent blur'); }, 1500);
          setTimeout(function () { window.dispatchEvent(new Event('focus')); note('sent focus'); }, 2000);
          setTimeout(function () { console.log('[Stub] done, calls: ' + calls.join(', ')); }, 2500);
        },
      },
      GameplayAPI: { start: function () { note('GameplayAPI.start'); }, stop: function () { note('GameplayAPI.stop'); } },
    },
    on: function (name, cb) { handlers[name] = cb; },
    off: function () {},
    getPlayer: function () {
      note('getPlayer');
      return Promise.resolve({
        getData: function () { note('getData'); return Promise.resolve(data); },
        setData: function (value) { note('setData'); data = value; return Promise.resolve(); },
      });
    },
    adv: {
      showFullscreenAdv: function (o) { note('showFullscreenAdv'); o.callbacks.onOpen(); o.callbacks.onClose(true); },
      showRewardedVideo: function (o) { note('showRewardedVideo'); o.callbacks.onOpen(); o.callbacks.onRewarded(); o.callbacks.onClose(); },
    },
  };
  window.YaGames = { init: function () { note('YaGames.init'); return Promise.resolve(ysdk); } };
})();
