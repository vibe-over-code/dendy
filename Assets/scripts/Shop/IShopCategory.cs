public interface IShopCategory
{
    string CategoryName { get; }

    void ApplyItem(int index);
    void LoadSavedSelection();

    //добавляем предпросмотр
    void PreviewItem(int index);
    void CancelPreview();

    void RefreshUI();
}

