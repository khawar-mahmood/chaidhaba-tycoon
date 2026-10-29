// Browser helpers for Chai Dhaba Tycoon: localStorage wrapped in try/catch (incognito-safe), phone
// detection, tab visibility and no page scrolling on game keys. The Poki SDK calls are in PokiSDKBridge.jslib
// (Poki's own plugin, driven by Scripts/Poki/PokiUnitySDK.cs).
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

  Chai_Init: function (goPtr) {
    window.__chaiGo = UTF8ToString(goPtr);
    try {
      // Block the page from scrolling on arrow keys / space (Poki embeds games in a scrolling page).
      window.addEventListener("keydown", function (ev) {
        if ([32, 37, 38, 39, 40].indexOf(ev.keyCode) > -1) ev.preventDefault();
      }, { passive: false });
    } catch (e) {}
    try {
      // Tab hidden or phone locked: pause the simulation, gameplayStop, mute.
      document.addEventListener("visibilitychange", function () {
        try { SendMessage(window.__chaiGo, "OnVisibility", document.hidden ? "1" : "0"); } catch (e) {}
      });
    } catch (e) {}
  }
});
