using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;

#if UNITY_WEBGL && !UNITY_EDITOR
using YG; // SDK Яндекс.Игр
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Localizator : MonoBehaviour
{
    [System.Serializable]
    public class TextEntry
    {
        public string key;
        public Text uiText;               // стандартный UI Text
        public TextMeshProUGUI tmpText;   // TMP поддержка
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

    void Start()
    {
        Initialize();
        UpdateTexts();
    }

    // -------------------------------------------------------------
    // 🔹 Инициализация локализации
    // -------------------------------------------------------------
    public void Initialize(bool forceReload = false)
    {
        if (initialized && !forceReload) return;

        string lang = DetectLanguage();
        currentLang = lang;
        localizedTexts.Clear();

        if (langsFile == null)
        {
            Debug.LogError("[Localizator] Не указан JSON-файл локализации!");
            return;
        }

        try
        {
            var root = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(langsFile.text);
            if (root != null && root.ContainsKey(lang))
            {
                localizedTexts = root[lang];
                Debug.Log($"[Localizator] Загружен язык: {lang}");
            }
            else if (root != null && root.ContainsKey("en"))
            {
                localizedTexts = root["en"];
                Debug.LogWarning($"[Localizator] Язык '{lang}' не найден. Используется 'en'.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Localizator] Ошибка загрузки JSON: {ex.Message}");
        }

        initialized = true;
    }

    // -------------------------------------------------------------
    // 🔹 Обновление всех текстов на сцене
    // -------------------------------------------------------------
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

    // -------------------------------------------------------------
    // 🔹 Определение языка
    // -------------------------------------------------------------
    private string DetectLanguage()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Используем YG2.lang (автоопределение SDK)
        string yandexLang = YG2.lang;
        if (string.IsNullOrEmpty(yandexLang))
            yandexLang = "en"; // fallback
        return yandexLang;
#else
        // Режим предпросмотра в редакторе
        return selectedTestLang.ToString();
#endif
    }

    // -------------------------------------------------------------
    // 🔹 Получить текст по ключу
    // -------------------------------------------------------------
    public static string Get(string key)
    {
        if (!initialized)
            return $"#{key}";

        if (localizedTexts.TryGetValue(key, out string value))
        {
            // 💡 Исправляем комбинированные Unicode-глифы
            if (!string.IsNullOrEmpty(value))
                value = value.Normalize(System.Text.NormalizationForm.FormC);
            return value;
        }

        return $"#{key}";
    }


#if UNITY_EDITOR
    // -------------------------------------------------------------
    // 🔹 Обновление текста в редакторе при изменении инспектора
    // -------------------------------------------------------------
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
