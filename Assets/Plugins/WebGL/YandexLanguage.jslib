mergeInto(LibraryManager.library, {
    GetYandexLanguage: function () {
        if (typeof YandexGames !== "undefined" && YandexGames.environment) {
            var lang = YandexGames.environment.language || "en";
            // Создаём C-строку (UTF8) для Unity
            var lengthBytes = lengthBytesUTF8(lang) + 1;
            var stringOnWasmHeap = _malloc(lengthBytes);
            stringToUTF8(lang, stringOnWasmHeap, lengthBytes);
            return stringOnWasmHeap;
        } else {
            var defaultLang = "en";
            var lengthBytes = lengthBytesUTF8(defaultLang) + 1;
            var stringOnWasmHeap = _malloc(lengthBytes);
            stringToUTF8(defaultLang, stringOnWasmHeap, lengthBytes);
            return stringOnWasmHeap;
        }
    }
});
