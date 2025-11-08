using System.Runtime.InteropServices;
using UnityEngine;

public class YandexAdManager : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern void ShowYandexRewarded();

    public static YandexAdManager Instance;

    // —сылка на VolumeMenuController (дл€ управлени€ громкостью)
    public VolumeMenuController volumeController;

    private void Awake()
    {
        Instance = this;
    }

    public void ShowAd()
    {
        // ѕеред показом рекламы Ч выключаем звук
        if (volumeController != null)
            volumeController.MuteAll(true);

#if UNITY_WEBGL && !UNITY_EDITOR
        ShowYandexRewarded();
#else
        Debug.Log("Rewarded ads only work in WebGL build.");
        // Ёмул€ци€ завершени€ рекламы в редакторе
        OnAdClosed();
#endif
    }

    // Ётот метод вызываетс€ из JS после окончани€ рекламы
    public void OnAdClosed()
    {
        Debug.Log("–еклама завершена");

        // ¬озвращаем звук
        if (volumeController != null)
            volumeController.MuteAll(false);
    }

    // Ётот метод вызываетс€, когда пользователь получил награду
    public void OnRewarded()
    {
        Debug.Log("ѕользователь получил награду за рекламу!");
        ShopManager.Instance.AddCoins(50);
        if (volumeController != null)
            volumeController.MuteAll(false);
    }
}
