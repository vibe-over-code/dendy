using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;
using YG; // поддержка YandexGame

[System.Serializable]
public class KeyBlock
{
    public List<KeyCode> keys = new List<KeyCode>();
}

[System.Serializable]
public class MobileStep
{
    [Tooltip("Ключ локализации (например, edu_move)")]
    public string localizationKey;

    [TextArea(2, 3)]
    [Tooltip("Текст по умолчанию, если перевода нет")]
    public string fallbackText = "";

    [Tooltip("Длительность показа шага (сек)")]
    public float duration = 3f;

    [Header("Смещение надписи (в пикселях, от центра экрана)")]
    public Vector2 offset = new Vector2(0, 0);
}

public class Education : MonoBehaviour
{
    [Header("PC: Настройка блоков клавиш")]
    public List<KeyBlock> keyBlocks = new List<KeyBlock>();

    [Header("Mobile: Настройка шагов обучения")]
    public List<MobileStep> mobileSteps = new List<MobileStep>();

    [Header("Общие настройки UI")]
    public TMP_FontAsset fontAsset;
    public int fontSize = 60;
    public Color fontColor = Color.yellow;
    public float pulseSpeed = 2f;
    public float pulseAmount = 0.2f;
    public float bottomMargin = 150f;
    public float letterSpacing = 1.3f;

    [Header("Mobile настройки отображения")]
    [Tooltip("Принудительно включить мобильный режим (для теста в редакторе)")]
    public bool mobileTestMode = false;

    [Tooltip("Глобальное смещение всех мобильных надписей (в пикселях)")]
    public Vector2 mobileTextGlobalOffset = new Vector2(0, 150f);

    private Canvas canvas;
    private RectTransform panel;
    private Dictionary<KeyCode, TMP_Text> currentKeyTexts = new Dictionary<KeyCode, TMP_Text>();
    private int currentBlockIndex = 0;
    private float pulseTimer = 0f;

    // --- мобильное обучение ---
    private TMP_Text mobileText;
    private int currentMobileStep = 0;
    private float mobileTimer = 0f;

    private const string PREFS_KEY = "EducationComplete";

    void Awake()
    {
        bool isMobile = mobileTestMode || Application.isMobilePlatform;

        // Проверка — обучение уже завершено?
        if (PlayerPrefs.GetInt(PREFS_KEY, 0) == 1)
        {
            CleanupAndDestroy();
            return;
        }

        // Локализация
        Localizator loc = FindObjectOfType<Localizator>();
        if (loc != null)
            loc.Initialize();

        // Создание UI
        CreateUI();

        if (isMobile)
        {
            Debug.Log("[Education] Запущено мобильное обучение.");
            ShowMobileStep(0);
        }
        else
        {
            Debug.Log("[Education] Запущено ПК обучение.");
            ShowCurrentBlock();
        }
    }

    void Update()
    {
        bool isMobile = mobileTestMode || Application.isMobilePlatform;

        if (isMobile)
            UpdateMobileEducation();
        else
            UpdatePCEducation();
    }

    // ---------------- PC обучение ----------------
    void UpdatePCEducation()
    {
        if (currentBlockIndex >= keyBlocks.Count) return;

        pulseTimer += Time.deltaTime * pulseSpeed;

        // Эффект пульсации
        foreach (var kvp in currentKeyTexts)
        {
            TMP_Text t = kvp.Value;
            if (t != null && t.gameObject.activeSelf)
            {
                float scale = 1f + Mathf.Sin(pulseTimer) * pulseAmount;
                t.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        // Проверка нажатий клавиш
        for (int i = keyBlocks[currentBlockIndex].keys.Count - 1; i >= 0; i--)
        {
            KeyCode key = keyBlocks[currentBlockIndex].keys[i];
            if (Input.GetKeyDown(key))
            {
                if (currentKeyTexts.ContainsKey(key))
                    currentKeyTexts[key].gameObject.SetActive(false);

                keyBlocks[currentBlockIndex].keys.RemoveAt(i);
            }
        }

        // Переход к следующему блоку
        if (keyBlocks[currentBlockIndex].keys.Count == 0)
        {
            currentBlockIndex++;
            if (currentBlockIndex < keyBlocks.Count)
                ShowCurrentBlock();
            else
                CompleteEducation();
        }
    }

    // ---------------- Mobile обучение ----------------
    void UpdateMobileEducation()
    {
        if (currentMobileStep >= mobileSteps.Count) return;

        mobileTimer += Time.deltaTime;
        if (mobileTimer >= mobileSteps[currentMobileStep].duration)
        {
            currentMobileStep++;
            if (currentMobileStep < mobileSteps.Count)
                ShowMobileStep(currentMobileStep);
            else
                CompleteEducation();
        }

        // Пульсация текста
        if (mobileText != null)
        {
            float scale = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            mobileText.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    // ---------------- Завершение обучения ----------------
    void CompleteEducation()
    {
        PlayerPrefs.SetInt(PREFS_KEY, 1);
        PlayerPrefs.Save();
        CleanupAndDestroy();
    }

    // ---------------- Очистка Canvas ----------------
    void CleanupAndDestroy()
    {
        if (canvas != null)
        {
            Destroy(canvas.gameObject);
        }
        Destroy(gameObject);
    }

    // ---------------- Создание UI ----------------
    void CreateUI()
    {
        GameObject canvasGO = new GameObject("EducationCanvas");
        canvasGO.layer = LayerMask.NameToLayer("UI");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject panelGO = new GameObject("KeysPanel");
        panelGO.transform.SetParent(canvas.transform, false);
        panel = panelGO.AddComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0);
        panel.anchorMax = new Vector2(0.5f, 0);
        panel.pivot = new Vector2(0.5f, 0);
        panel.anchoredPosition = new Vector2(0, bottomMargin);
        panel.sizeDelta = new Vector2(1920, 200);
    }

    // ---------------- ПК блок ----------------
    void ShowCurrentBlock()
    {
        currentKeyTexts.Clear();
        if (currentBlockIndex >= keyBlocks.Count) return;

        List<KeyCode> block = keyBlocks[currentBlockIndex].keys;
        float keyBlockWidth = fontSize * letterSpacing;
        float extraSpaceWidth = keyBlockWidth * 4f;
        float totalWidth = 0f;

        foreach (KeyCode k in block)
            totalWidth += (k == KeyCode.Space ? extraSpaceWidth : keyBlockWidth);

        float startX = -totalWidth / 2f;

        foreach (KeyCode key in block)
        {
            GameObject textGO = new GameObject("Key_" + key);
            textGO.transform.SetParent(panel, false);

            TMP_Text text = textGO.AddComponent<TextMeshProUGUI>();
            text.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.color = fontColor;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            string localized = Localizator.Get(key.ToString());
            text.text = string.IsNullOrEmpty(localized) ? key.ToString().ToUpper() : localized;

            RectTransform rt = text.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(key == KeyCode.Space ? extraSpaceWidth : keyBlockWidth, fontSize);
            rt.anchoredPosition = new Vector2(startX + rt.sizeDelta.x / 2f, 0);

            currentKeyTexts.Add(key, text);
            startX += rt.sizeDelta.x;
        }
    }

    // ---------------- Мобильный шаг ----------------
    void ShowMobileStep(int index)
    {
        if (mobileSteps.Count == 0) return;

        if (mobileText == null)
        {
            GameObject t = new GameObject("MobileText");
            t.transform.SetParent(canvas.transform, false);
            mobileText = t.AddComponent<TextMeshProUGUI>();
            mobileText.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            mobileText.fontSize = fontSize;
            mobileText.color = fontColor;
            mobileText.alignment = TextAlignmentOptions.Center;
            mobileText.raycastTarget = false;
        }

        MobileStep step = mobileSteps[index];

        string localized = Localizator.Get(step.localizationKey);
        if (string.IsNullOrEmpty(localized))
            localized = step.fallbackText;

        mobileText.text = localized;
        mobileText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        mobileText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        mobileText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        mobileText.rectTransform.anchoredPosition = step.offset + mobileTextGlobalOffset;

        mobileTimer = 0f;
    }
}
