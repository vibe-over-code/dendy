using UnityEngine;

[System.Serializable]
public class MaterialTextureCategory : IShopCategory
{
    [Tooltip("Ключ локализации категории (например, material_metal)")]
    public string categoryName; // используется как ключ локализации

    [Tooltip("Материал, к которому применяются текстуры")]
    public Material targetMaterial;

    [Tooltip("Список доступных текстур (основные материалы)")]
    public Texture[] textures;

    [Tooltip("Иконки для отображения в магазине (если не указано — используется сама текстура)")]
    public Texture2D[] icons;

    private const string PREF_KEY_BOUGHT = "Bought_";
    private const string PREF_KEY_SELECTED = "Selected_";

    private int selectedIndex;
    private int previewIndex = -1;
    private bool previewIsBought = false;

    /// <summary>
    /// Локализованное имя категории
    /// </summary>
    public string CategoryName
    {
        get
        {
            if (!string.IsNullOrEmpty(categoryName))
                return Localizator.Get(categoryName);
            return categoryName;
        }
    }

    /// <summary>
    /// Возвращает иконку для магазина. Если нет иконки — fallback на текстуру
    /// </summary>
    public Texture GetIcon(int index)
    {
        if (icons != null && index >= 0 && index < icons.Length && icons[index] != null)
            return icons[index];

        if (textures != null && index >= 0 && index < textures.Length)
            return textures[index];

        return null;
    }

    public void ApplyItem(int index)
    {
        if (index < 0 || index >= textures.Length) return;
        if (!IsBought(index)) return;

        selectedIndex = index;
        targetMaterial.SetTexture("_BaseMap", textures[index]);
        PlayerPrefs.SetInt(PREF_KEY_SELECTED + categoryName, index);
    }

    public void PreviewItem(int index)
    {
        if (index < 0 || index >= textures.Length) return;

        previewIndex = index;
        previewIsBought = IsBought(index);
        targetMaterial.SetTexture("_BaseMap", textures[index]);
    }

    public void CancelPreview()
    {
        if (previewIndex != -1 && !previewIsBought)
        {
            targetMaterial.SetTexture("_BaseMap", textures[selectedIndex]);
        }
        previewIndex = -1;
    }

    public void LoadSavedSelection()
    {
        selectedIndex = PlayerPrefs.GetInt(PREF_KEY_SELECTED + categoryName, 0);
        if (textures.Length > selectedIndex)
            targetMaterial.SetTexture("_BaseMap", textures[selectedIndex]);
    }

    public bool IsBought(int index)
    {
        return PlayerPrefs.GetInt(PREF_KEY_BOUGHT + categoryName + "_" + index, 0) == 1;
    }

    public void MarkAsBought(int index)
    {
        PlayerPrefs.SetInt(PREF_KEY_BOUGHT + categoryName + "_" + index, 1);
        PlayerPrefs.Save();
    }

    public void RefreshUI()
    {
        selectedIndex = 0;
        previewIndex = -1;
        previewIsBought = false;

        if (textures.Length > 0 && targetMaterial != null)
            targetMaterial.SetTexture("_BaseMap", textures[0]);
    }
}
