using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;
using YG; // 👈 добавляем поддержку YandexGame

[System.Serializable]
public class KeyBlock
{
    public List<KeyCode> keys = new List<KeyCode>();
}

public class Education : MonoBehaviour
{
    [Header("Настройка блоков клавиш")]
    public List<KeyBlock> keyBlocks = new List<KeyBlock>();

    [Header("Настройка UI")]
    public TMP_FontAsset fontAsset; // шрифт TextMeshPro
    public int fontSize = 60;
    public Color fontColor = Color.yellow;
    public float pulseSpeed = 2f;
    public float pulseAmount = 0.2f;
    public float bottomMargin = 150f;
    public float letterSpacing = 1.3f;

    [Header("Mobile Settings")]
    [Tooltip("Принудительно отключить обучение, если тестируем мобильную версию.")]
    public bool mobileTestMode = false;

    private Canvas canvas;
    private RectTransform panel;
    private Dictionary<KeyCode, TMP_Text> currentKeyTexts = new Dictionary<KeyCode, TMP_Text>();
    private int currentBlockIndex = 0;
    private float pulseTimer = 0f;

    private const string PREFS_KEY = "EducationComplete";

    void Awake()
    {
        // --- Проверка устройства ---
        bool isMobile = mobileTestMode || Application.isMobilePlatform;
        if (isMobile)
        {
            Debug.Log("[Education] Обучение отключено — мобильное устройство.");
            Destroy(gameObject);
            return;
        }

        // --- Проверка, проходилось ли обучение ранее ---
        if (PlayerPrefs.GetInt(PREFS_KEY, 0) == 1)
        {
            Destroy(gameObject);
            return;
        }

        // --- Локализация ---
        Localizator loc = FindObjectOfType<Localizator>();
        if (loc != null)
            loc.Initialize();

        // --- Запуск обучения ---
        CreateUI();
        ShowCurrentBlock();
    }

    void Update()
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
            {
                PlayerPrefs.SetInt(PREFS_KEY, 1);
                PlayerPrefs.Save();
                Destroy(gameObject);
            }
        }
    }

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

        for (int i = 0; i < block.Count; i++)
        {
            KeyCode key = block[i];

            GameObject textGO = new GameObject("Key_" + key);
            textGO.transform.SetParent(panel, false);

            TMP_Text text = textGO.AddComponent<TextMeshProUGUI>();
            text.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.color = fontColor;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            string locKey = key.ToString();
            string localized = Localizator.Get(locKey);
            text.text = string.IsNullOrEmpty(localized) ? key.ToString().ToUpper() : localized;

            RectTransform rt = text.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(key == KeyCode.Space ? extraSpaceWidth : keyBlockWidth, fontSize);
            rt.anchoredPosition = new Vector2(startX + rt.sizeDelta.x / 2f, 0);

            currentKeyTexts.Add(key, text);
            startX += rt.sizeDelta.x;
        }
    }
}
