using System.Runtime.InteropServices;
using UnityEngine;

public class YandexAdManager : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern void ShowYandexRewarded();

    public static YandexAdManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    public void ShowAd()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ShowYandexRewarded();
#else
        Debug.Log("Rewarded ads only work in WebGL build.");
        OnRewarded();
#endif
    }

    public void OnRewarded()
    {
        Debug.Log("ѕользователь получил награду за рекламу!");
        ShopManager.Instance.AddCoins(50);
    }
}


