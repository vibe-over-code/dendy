using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;

    [Header("UI Elements")]
    public TMP_Text coinsText;
    public Button adButton;
    public Transform tabButtonContainer;
    public GameObject categoryTabPrefab;
    public Transform itemsContainer;
    public GameObject itemPrefab;
    public Button resetButton;

    [Header("Categories")]
    public List<MaterialTextureCategory> categories = new List<MaterialTextureCategory>();

    private IShopCategory activeCategory;
    private int coins;

    [SerializeField]
    private bool _isInMenu = true;
    public bool isInMenu
    {
        get => _isInMenu;
        set
        {
            if (_isInMenu == value) return;
            _isInMenu = value;

            if (!_isInMenu)
            {
                foreach (var cat in categories)
                    cat.CancelPreview();
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        coins = PlayerPrefs.GetInt("Coins", 0);
        UpdateCoinsUI();

        foreach (var cat in categories)
        {
            cat.LoadSavedSelection();
            CreateCategoryButton(cat);
        }

        if (categories.Count > 0)
            SetActiveCategory(categories[0]);

        if (adButton != null)
            adButton.onClick.AddListener(() => YandexAdManager.Instance.ShowAd());

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetPlayerPrefs);
    }

    private void CreateCategoryButton(IShopCategory category)
    {
        var buttonObj = Instantiate(categoryTabPrefab, tabButtonContainer);
        var button = buttonObj.GetComponent<Button>();
        var text = buttonObj.GetComponentInChildren<TMP_Text>();
        text.text = category.CategoryName;

        button.onClick.AddListener(() => SetActiveCategory(category));
    }

    public void SetActiveCategory(IShopCategory category)
    {
        activeCategory = category;
        RefreshItemsUI();
    }

    private void RefreshItemsUI()
    {
        foreach (Transform child in itemsContainer)
            Destroy(child.gameObject);

        if (activeCategory is MaterialTextureCategory matCat)
        {
            for (int i = 0; i < matCat.textures.Length; i++)
            {
                var item = Instantiate(itemPrefab, itemsContainer);
                var ui = item.GetComponent<ShopItemUI>();
                ui.Setup(matCat, i);
            }
        }
    }

    // --- Изменения: всегда сохраняем сразу после изменения coins ---
    public void AddCoins(int amount)
    {
        coins += amount;
        PlayerPrefs.SetInt("Coins", coins);
        PlayerPrefs.Save(); // <-- важное добавление
        UpdateCoinsUI();
    }

    public void SpendCoins(int amount)
    {
        coins = Mathf.Max(0, coins - amount);
        PlayerPrefs.SetInt("Coins", coins);
        PlayerPrefs.Save(); // <-- важное добавление
        UpdateCoinsUI();
    }
    // ----------------------------------------------------------------

    private void UpdateCoinsUI()
    {
        if (coinsText != null)
            coinsText.text = coins.ToString();
    }

    public bool TryBuyItem(MaterialTextureCategory category, int index, int price)
    {
        if (coins < price)
        {
            Debug.Log("Недостаточно монет!");
            return false;
        }

        // теперь используем SpendCoins, где PlayerPrefs.Save() уже вызывается
        SpendCoins(price);

        category.MarkAsBought(index);
        return true;
    }

    public void ResetPlayerPrefs()
    {
        Debug.LogWarning("Полный сброс всех сохранённых данных...");

        // Полностью очищаем PlayerPrefs
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save(); // обязательно, чтобы изменения записались на диск

        // Сбрасываем локальные переменные (если нужно)
        coins = 0;
        UpdateCoinsUI();

        // Обновляем UI магазина, если он есть
        foreach (var category in categories)
            category.RefreshUI();

        Debug.Log("Все PlayerPrefs удалены, данные сброшены!");
    }

}
