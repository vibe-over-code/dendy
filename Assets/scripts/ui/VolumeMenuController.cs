using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Audio; // ⬅️ ОЧЕНЬ ВАЖНО: Добавлено для работы с микшером

/// <summary>
/// Контроллер меню громкости и паузы.
/// Управляет двумя слайдерами, которые регулируют SFX и Музыку через AudioMixer.
/// </summary>
public class VolumeMenuController : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject volumeMenu;   // Панель меню (Canvas или Panel)
    public Button toggleButton;     // Кнопка для открытия меню
    
    // Новые разделенные слайдеры
    public Slider musicSlider;      // Слайдер громкости музыки
    public Slider sfxSlider;        // Слайдер громкости звуковых эффектов (SFX)
    
    [Header("Audio")]
    public AudioMixer mainMixer;    // Ссылка на MainMixer (из Project)
    
    // Имена открытых параметров в микшере (должны совпадать!)
    private const string SFX_VOLUME_PARAM = "SFXVolume";
    private const string MUSIC_VOLUME_PARAM = "MusicVolume";
    
    [Header("Gameplay References")]
    public GameObject player;       // Игрок, чтобы можно было отключить его управление

    private bool isMenuOpen = false;

    private void Start()
    {
        // Гарантируем наличие EventSystem (для UI)
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        if (volumeMenu != null)
            volumeMenu.SetActive(false);

        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleMenu);

        // --- Инициализация Music Slider ---
        if (musicSlider != null && mainMixer != null)
        {
            InitializeSlider(musicSlider, MUSIC_VOLUME_PARAM, OnMusicSliderChanged);
        }

        // --- Инициализация SFX Slider ---
        if (sfxSlider != null && mainMixer != null)
        {
            InitializeSlider(sfxSlider, SFX_VOLUME_PARAM, OnSfxSliderChanged);
        }
    }

    /// <summary>
    /// Инициализирует слайдер, устанавливая его значение из микшера и подписывая на событие.
    /// </summary>
    private void InitializeSlider(Slider slider, string paramName, UnityEngine.Events.UnityAction<float> listener)
    {
        // 1. Установка начального значения из микшера
        float currentVolume_db;
        if (mainMixer.GetFloat(paramName, out currentVolume_db))
        {
            // Преобразование дБ обратно в линейное значение (0 до 1) для слайдера
            slider.value = Mathf.Pow(10, currentVolume_db / 20f);
        }
        else
        {
            // Если не удалось загрузить, устанавливаем по умолчанию
            slider.value = 0.75f;
        }

        // 2. Отключение навигации (чтобы избежать конфликтов с управлением танком)
        var nav = slider.navigation;
        nav.mode = Navigation.Mode.None;
        slider.navigation = nav;

        // 3. Подписка на событие
        slider.onValueChanged.AddListener(listener);
    }
    
    private void ToggleMenu()
    {
        if (volumeMenu == null) return;

        isMenuOpen = !isMenuOpen;
        volumeMenu.SetActive(isMenuOpen);

        // Управление паузой и временем
        if (isMenuOpen)
        {
            Time.timeScale = 0f; // Ставим игру на паузу
            
            // Блокируем управление игроком
            if (player != null)
            {
                var controller = player.GetComponent<PlayerController>();
                if (controller != null)
                    controller.enabled = false;
            }
        }
        else
        {
            Time.timeScale = 1f; // Возвращаем время
            
            // Возвращаем управление игроку
            if (player != null)
            {
                var controller = player.GetComponent<PlayerController>();
                if (controller != null)
                    controller.enabled = true;
            }
        }
        
        // Сброс фокуса UI, чтобы избежать случайного ввода после закрытия меню
        EventSystem.current.SetSelectedGameObject(null);
    }
    
    // --- Методы обработки слайдеров ---
    
    private void OnSfxSliderChanged(float value)
    {
        SetVolume(value, SFX_VOLUME_PARAM);
    }
    
    private void OnMusicSliderChanged(float value)
    {
        SetVolume(value, MUSIC_VOLUME_PARAM);
    }
    
    /// <summary>
    /// Устанавливает громкость в Audio Mixer, преобразуя линейное значение (0-1) в логарифмическое (дБ).
    /// </summary>
    private void SetVolume(float value, string paramName)
    {
        if (mainMixer == null) return;
        
        if (value > 0)
        {
            // Логарифмическое преобразование: 1.0 -> 0 дБ, 0.1 -> -20 дБ и т.д.
            mainMixer.SetFloat(paramName, Mathf.Log10(value) * 20);
        }
        else
        {
            // При значении 0 выставляем тишину
            mainMixer.SetFloat(paramName, -80f); 
        }
    }
}