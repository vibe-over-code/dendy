using System.Runtime.InteropServices;
using UnityEngine;

public static class YandexLanguage
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern string GetYandexLanguage();
#else
    private static string GetYandexLanguage() { return "en"; }
#endif

    public static string Language()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return GetYandexLanguage();
#else
        return "en"; // по умолчанию в редакторе
#endif
    }
}
