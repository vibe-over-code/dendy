using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Localizator : MonoBehaviour
{
    [System.Serializable]
    public class TextEntry
    {
        public string key;
        public Text uiText;
        public TextMeshProUGUI tmpText;
    }

    [Header("Файл JSON с локализациями")]
    public TextAsset langsFile;

    [Tooltip("Список текстов и ключей локализации")]
    public List<TextEntry> texts = new();

    [Header("Тестовый язык для редактора")]
    public TestLang selectedTestLang = TestLang.en;

    public enum TestLang { en, ru, de, fr, es }

    private static bool initialized = false;
    private static string currentLang = "en";
    private static Dictionary<string, string> localizedTexts = new();

    // -------------------------------------------------------------
    // 🔹 WebGL jslib вызов
    // -------------------------------------------------------------
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern string GetYandexLanguage();
#endif

    void Start()
    {
        Initialize();
        UpdateTexts();
    }

    public void Initialize(bool forceReload = false)
    {
        if (initialized && !forceReload) return;

        string lang = DetectLanguage();
        currentLang = lang;
        localizedTexts.Clear();

        if (langsFile == null)
        {
            Debug.LogError("[Localization] Не указан JSON-файл локализации!");
            return;
        }

        try
        {
            var root = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(langsFile.text);
            if (root != null && root.ContainsKey(lang))
            {
                localizedTexts = root[lang];
                Debug.Log($"[Localization] Загружен язык: {lang}");
            }
            else if (root != null && root.ContainsKey("en"))
            {
                localizedTexts = root["en"];
                Debug.LogWarning($"[Localization] Язык '{lang}' не найден. Используется 'en'.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Localization] Ошибка загрузки JSON: {ex.Message}");
        }

        initialized = true;
    }

    private string DetectLanguage()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string yandexLang = GetYandexLanguage();
        if (string.IsNullOrEmpty(yandexLang))
            yandexLang = "en";
        return yandexLang;
#else
        return selectedTestLang.ToString();
#endif
    }

    public void UpdateTexts()
    {
        foreach (var t in texts)
        {
            string value = Get(t.key);
            if (t.uiText != null)
                t.uiText.text = value;
            if (t.tmpText != null)
                t.tmpText.text = value;
        }
    }

    public static string Get(string key)
    {
        if (!initialized)
            return $"#{key}";

        if (localizedTexts.TryGetValue(key, out string value))
            return value;

        return $"#{key}";
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (langsFile != null)
        {
            Initialize(true);
            UpdateTexts();
        }
    }
#endif
}
