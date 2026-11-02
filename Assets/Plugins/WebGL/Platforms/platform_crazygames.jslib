// The bridge to the CrazyGames SDK v3. GamePlatformCall(op, id, arg): the answer comes to PlatformReceiver.Result as JSON
// {id, ok, value, error}; events come to PlatformReceiver.Event. A build links exactly one jslib of this folder.
// The page loads the SDK in its head.
mergeInto(LibraryManager.library, {
  GamePlatformCall: function (opPtr, idPtr, argPtr) {
    var op = UTF8ToString(opPtr);
    var id = UTF8ToString(idPtr);
    var arg = UTF8ToString(argPtr);
    var receiver = 'PlatformReceiver';
    var saveKey = 'save';

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
      if (!window.CrazyGames || !window.CrazyGames.SDK) return Promise.reject(new Error('no CrazyGames SDK in the page'));
      var sdk = window.CrazyGames.SDK;
      return sdk.init().then(function () {
        try { sdk.game.loadingStart(); } catch (e) {}
        var settings = sdk.game.settings || {};
        sdk.game.addSettingsChangeListener(function (next) { emit(next.muteAudio ? 'audioOff' : 'audioOn'); });
        watchFocus();
        return JSON.stringify({ lang: (sdk.user.systemInfo && sdk.user.systemInfo.locale) || '', audio: !settings.muteAudio });
      });
    };

    var requestAd = function (kind, finished) {
      return new Promise(function (resolve) {
        var started = false;
        window.CrazyGames.SDK.ad.requestAd(kind, {
          adStarted: function () { started = true; emit('adStart'); },
          adFinished: function () { emit('adEnd'); resolve(finished); },
          adError: function () { if (started) emit('adEnd'); resolve('closed'); }
        });
      });
    };

    var call = function () {
      var sdk = window.CrazyGames && window.CrazyGames.SDK;
      switch (op) {
        case 'init':
          return init();
        case 'firstFrame':
          return '';
        case 'ready':
          sdk.game.loadingStop();
          return '';
        case 'gameplayStart':
          sdk.game.gameplayStart();
          return '';
        case 'gameplayStop':
          sdk.game.gameplayStop();
          return '';
        case 'interstitial':
          return requestAd('midgame', 'shown');
        case 'rewarded':
          return requestAd('rewarded', 'rewarded');
        case 'load':
          return sdk.data.getItem(saveKey) || '';
        case 'save':
          sdk.data.setItem(saveKey, arg);
          return 'ok';
        default:
          throw new Error('unknown platform call ' + op);
      }
    };

    Promise.resolve().then(call).then(
      function (value) { send(true, value); },
      function (error) { send(false, null, (error && error.message) || String(error)); });
  }
});
