// Browser helpers for Chai Dhaba Tycoon: localStorage wrapped in try/catch (incognito-safe), phone
// detection, tab visibility and no page scrolling on game keys. The CrazyGames SDK calls are in ../crazySDK.jslib
// (the SDK's own plugin). The localStorage calls are only a fallback: saves normally go to the SDK's Data module.
mergeInto(LibraryManager.library, {

  Chai_StorageGet: function (keyPtr) {
    var value = null;
    try { value = window.localStorage.getItem(UTF8ToString(keyPtr)); } catch (e) { value = null; }
    if (value === null || value === undefined) value = "";
    var size = lengthBytesUTF8(value) + 1;
    var buf = _malloc(size);
    stringToUTF8(value, buf, size);
    return buf;
  },

  Chai_StorageSet: function (keyPtr, valuePtr) {
    try {
      var k = UTF8ToString(keyPtr), v = UTF8ToString(valuePtr);
      if (v === "") window.localStorage.removeItem(k); else window.localStorage.setItem(k, v);
      return 1;
    } catch (e) { return 0; }
  },

  Chai_IsMobile: function () {
    try { return (/Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent) || (navigator.maxTouchPoints > 1 && /Macintosh/.test(navigator.userAgent))) ? 1 : 0; }
    catch (e) { return 0; }
  },

  // 1 when CrazyGames has ads off for this game. The SDK has no flag for it, but prefetchAd throws at once then
  // (and otherwise just gets a rewarded ad ready).
  Chai_AdsDisabled: function () {
    try { window.CrazyGames.SDK.ad.prefetchAd("rewarded"); return 0; }
    catch (e) { return e && e.code && String(e.code).indexOf("adsDisabled") === 0 ? 1 : 0; }
  },

  Chai_Init: function (goPtr) {
    window.__chaiGo = UTF8ToString(goPtr);
    try {
      // Block the page from scrolling on arrow keys / space (CrazyGames embeds games in a scrolling page).
      window.addEventListener("keydown", function (ev) {
        if ([32, 37, 38, 39, 40].indexOf(ev.keyCode) > -1) ev.preventDefault();
      }, { passive: false });
    } catch (e) {}
    try {
      // No selection menu or magnifier on a long press (a CrazyGames mobile requirement).
      var body = document.body.style;
      body.userSelect = body.webkitUserSelect = body.webkitTouchCallout = "none";
    } catch (e) {}
    try {
      // Tab hidden or phone locked: pause the simulation, mute.
      document.addEventListener("visibilitychange", function () {
        try { SendMessage(window.__chaiGo, "OnVisibility", document.hidden ? "1" : "0"); } catch (e) {}
      });
    } catch (e) {}
  }
});
