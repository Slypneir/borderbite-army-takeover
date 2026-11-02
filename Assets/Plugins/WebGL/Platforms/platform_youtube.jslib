// The bridge to the Playables SDK. GamePlatformCall(op, id, arg): the answer comes to PlatformReceiver.Result as JSON
// {id, ok, value, error}; events come to PlatformReceiver.Event. A build links exactly one jslib of this folder.
// The page loads the SDK before the game code; nothing is loaded from anywhere else.
mergeInto(LibraryManager.library, {
  GamePlatformCall: function (opPtr, idPtr, argPtr) {
    var op = UTF8ToString(opPtr);
    var id = UTF8ToString(idPtr);
    var arg = UTF8ToString(argPtr);
    var receiver = 'PlatformReceiver';

    var send = function (ok, value, error) {
      SendMessage(receiver, 'Result', JSON.stringify({
        id: id, ok: ok, value: value === undefined || value === null ? '' : String(value), error: error || ''
      }));
    };
    var emit = function (type) { SendMessage(receiver, 'Event', type); };

    var init = function () {
      var yt = window.ytgame;
      if (!yt || !yt.IN_PLAYABLES_ENV) return Promise.reject(new Error('not inside YouTube Playables'));
      // pause and resume only from the SDK: Playables forbids the Page Visibility API; on pause all execution stops
      yt.system.onPause(function () {
        emit('pause');
        if (Module['pauseMainLoop']) Module['pauseMainLoop']();
      });
      yt.system.onResume(function () {
        if (Module['resumeMainLoop']) Module['resumeMainLoop']();
        emit('resume');
      });
      yt.system.onAudioEnabledChange(function (enabled) { emit(enabled ? 'audioOn' : 'audioOff'); });
      return Promise.resolve(yt.system.getLanguage()).then(function (lang) {
        return JSON.stringify({ lang: lang || '', audio: yt.system.isAudioEnabled() });
      });
    };

    var call = function () {
      var yt = window.ytgame;
      switch (op) {
        case 'init':
          return init();
        case 'firstFrame':
          yt.game.firstFrameReady();
          return '';
        case 'ready':
          yt.game.gameReady();
          return '';
        case 'gameplayStart':
        case 'gameplayStop':
          return '';
        case 'interstitial':
          // resolves on a successful request; YouTube pauses the game through onPause while the advert plays
          return yt.ads.requestInterstitialAd().then(function () { return 'shown'; }, function () { return 'closed'; });
        case 'rewarded':
          return yt.ads.requestRewardedAd(arg).then(function (earned) { return earned ? 'rewarded' : 'closed'; }, function () { return 'closed'; });
        case 'load':
          return yt.game.loadData().then(function (data) { return data || ''; });
        case 'save':
          return yt.game.saveData(arg).then(function () { return 'ok'; });
        default:
          throw new Error('unknown platform call ' + op);
      }
    };

    Promise.resolve().then(call).then(
      function (value) { send(true, value); },
      function (error) { send(false, null, (error && error.message) || String(error)); });
  }
});
