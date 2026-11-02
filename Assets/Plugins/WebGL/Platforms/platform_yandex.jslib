// The bridge to the Yandex Games SDK. GamePlatformCall(op, id, arg): the answer comes to PlatformReceiver.Result as JSON
// {id, ok, value, error}; events come to PlatformReceiver.Event. A build links exactly one jslib of this folder.
// The page loads /sdk.js in its head.
mergeInto(LibraryManager.library, {
  GamePlatformCall: function (opPtr, idPtr, argPtr) {
    var op = UTF8ToString(opPtr);
    var id = UTF8ToString(idPtr);
    var arg = UTF8ToString(argPtr);
    var receiver = 'PlatformReceiver';
    var state = window.__gamePlatform || (window.__gamePlatform = {});

    var send = function (ok, value, error) {
      SendMessage(receiver, 'Result', JSON.stringify({
        id: id, ok: ok, value: value === undefined || value === null ? '' : String(value), error: error || ''
      }));
    };
    var emit = function (type) { SendMessage(receiver, 'Event', type); };

    var watchFocus = function () {
      window.addEventListener('blur', function () { emit('focusLost'); });
      window.addEventListener('focus', function () { emit('focusBack'); });
      document.addEventListener('visibilitychange', function () { emit(document.hidden ? 'focusLost' : 'focusBack'); });
    };

    var init = function () {
      if (!window.YaGames) return Promise.reject(new Error('no Yandex SDK in the page'));
      return window.YaGames.init().then(function (ysdk) {
        state.ysdk = ysdk;
        ysdk.on('game_api_pause', function () { emit('pause'); });
        ysdk.on('game_api_resume', function () { emit('resume'); });
        watchFocus();
        return JSON.stringify({ lang: (ysdk.environment && ysdk.environment.i18n && ysdk.environment.i18n.lang) || '', audio: true });
      });
    };

    var player = function () {
      if (!state.player) state.player = state.ysdk.getPlayer();
      return state.player;
    };

    var call = function () {
      var ysdk = state.ysdk;
      switch (op) {
        case 'init':
          return init();
        case 'firstFrame':
          return '';
        case 'ready':
          ysdk.features.LoadingAPI.ready();
          return '';
        case 'gameplayStart':
          if (ysdk.features.GameplayAPI) ysdk.features.GameplayAPI.start();
          return '';
        case 'gameplayStop':
          if (ysdk.features.GameplayAPI) ysdk.features.GameplayAPI.stop();
          return '';
        case 'interstitial':
          return new Promise(function (resolve) {
            ysdk.adv.showFullscreenAdv({ callbacks: {
              onOpen: function () { emit('adStart'); },
              onClose: function (wasShown) { emit('adEnd'); resolve(wasShown ? 'shown' : 'closed'); },
              onError: function () { emit('adEnd'); resolve('closed'); }
            } });
          });
        case 'rewarded':
          return new Promise(function (resolve) {
            var rewarded = false;
            ysdk.adv.showRewardedVideo({ callbacks: {
              onOpen: function () { emit('adStart'); },
              onRewarded: function () { rewarded = true; },
              onClose: function () { emit('adEnd'); resolve(rewarded ? 'rewarded' : 'closed'); },
              onError: function () { emit('adEnd'); resolve('closed'); }
            } });
          });
        case 'load':
          return player().then(function (p) { return p.getData(['save']); }).then(function (data) { return data.save || ''; });
        case 'save':
          return player().then(function (p) { return p.setData({ save: arg }, true); }).then(function () { return 'ok'; });
        default:
          throw new Error('unknown platform call ' + op);
      }
    };

    Promise.resolve().then(call).then(
      function (value) { send(true, value); },
      function (error) { send(false, null, (error && error.message) || String(error)); });
  }
});
