using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopItemUI : MonoBehaviour
{
    public RawImage previewImage; // RawImage для отображения иконки или текстуры
    public TMP_Text priceText;
    public Button buyButton;

    private MaterialTextureCategory category;
    private int index;
    private int price;

    public void Setup(MaterialTextureCategory cat, int i)
    {
        category = cat;
        index = i;
        price = i * 50;

        // Устанавливаем текстуру в RawImage (иконка или fallback на основную текстуру)
        if (previewImage != null)
        {
            Texture tex = cat.GetIcon(i);
            if (tex != null)
                previewImage.texture = tex;
        }

        if (priceText != null)
            priceText.text = price.ToString();

        bool bought = cat.IsBought(i);
        if (buyButton != null)
            buyButton.gameObject.SetActive(!bought);

        // Обработчик кнопки “Купить”
        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() =>
            {
                if (ShopManager.Instance.TryBuyItem(cat, index, price))
                {
                    cat.ApplyItem(index);
                    buyButton.gameObject.SetActive(false);
                }
            });
        }

        // Предпросмотр кликом по изображению
        if (previewImage != null)
        {
            Button click = previewImage.gameObject.GetComponent<Button>();
            if (click == null)
                click = previewImage.gameObject.AddComponent<Button>();

            click.onClick.RemoveAllListeners();
            click.onClick.AddListener(() =>
            {
                if (ShopManager.Instance.isInMenu)
                    cat.PreviewItem(index);
            });
        }
    }
}
