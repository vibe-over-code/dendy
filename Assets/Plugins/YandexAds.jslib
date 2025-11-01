mergeInto(LibraryManager.library, {
    ShowYandexRewarded: function () {
        if (typeof ysdk === 'undefined') {
            console.log("YSDK not ready — did you call YaGames.init()?");
            return;
        }

        console.log("Trying to show rewarded ad...");

        ysdk.adv.showRewardedVideo({
            callbacks: {
                onOpen: () => {
                    console.log("Ad opened");
                },
                onRewarded: () => {
                    console.log("User rewarded!");
                    // Сообщаем в Unity, что реклама просмотрена
                    SendMessage("YandexAdManager", "OnRewarded", "");
                },
                onClose: () => {
                    console.log("Ad closed");
                },
                onError: (err) => {
                    console.error("Ad error:", err);
                }
            }
        });
    }
});
