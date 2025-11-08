using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Audio;

public class VolumeMenuController : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject volumeMenu;
    public Button toggleButton;

    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Audio")]
    public AudioMixer mainMixer;

    private const string SFX_VOLUME_PARAM = "SFXVolume";
    private const string MUSIC_VOLUME_PARAM = "MusicVolume";

    private const string PREF_SFX = "SFXVolumePref";
    private const string PREF_MUSIC = "MusicVolumePref";

    [Header("Gameplay References")]
    public GameObject player;

    private bool isMenuOpen = false;

    private void Start()
    {
        // Проверяем наличие EventSystem
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        if (volumeMenu != null)
            volumeMenu.SetActive(false);

        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleMenu);

        // Инициализация слайдеров с подгрузкой сохранённых значений
        if (mainMixer != null)
        {
            InitializeSlider(musicSlider, MUSIC_VOLUME_PARAM, PREF_MUSIC, OnMusicSliderChanged);
            InitializeSlider(sfxSlider, SFX_VOLUME_PARAM, PREF_SFX, OnSfxSliderChanged);
        }
    }

    /// <summary>
    /// Инициализация слайдера с подгрузкой сохранённого значения
    /// </summary>
    private void InitializeSlider(Slider slider, string paramName, string prefKey, UnityEngine.Events.UnityAction<float> listener)
    {
        if (slider == null) return;

        // Загружаем сохранённое значение или ставим по умолчанию
        float savedValue = PlayerPrefs.GetFloat(prefKey, 0.75f);
        slider.value = savedValue;
        listener.Invoke(savedValue); // Применяем сразу к AudioMixer

        // Отключаем навигацию
        var nav = slider.navigation;
        nav.mode = Navigation.Mode.None;
        slider.navigation = nav;

        // Подписка
        slider.onValueChanged.AddListener(listener);
    }

    private void ToggleMenu()
    {
        if (volumeMenu == null) return;

        isMenuOpen = !isMenuOpen;
        volumeMenu.SetActive(isMenuOpen);

        if (isMenuOpen)
        {
            Time.timeScale = 0f;

            if (player != null)
            {
                var controller = player.GetComponent<PlayerController>();
                if (controller != null)
                    controller.enabled = false;
            }
        }
        else
        {
            Time.timeScale = 1f;

            if (player != null)
            {
                var controller = player.GetComponent<PlayerController>();
                if (controller != null)
                    controller.enabled = true;
            }
        }

        EventSystem.current.SetSelectedGameObject(null);
    }

    // --- Методы для слайдеров ---
    private void OnSfxSliderChanged(float value)
    {
        SetVolume(value, SFX_VOLUME_PARAM);
        PlayerPrefs.SetFloat(PREF_SFX, value);
        PlayerPrefs.Save();
    }

    private void OnMusicSliderChanged(float value)
    {
        SetVolume(value, MUSIC_VOLUME_PARAM);
        PlayerPrefs.SetFloat(PREF_MUSIC, value);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Применяет громкость в микшере
    /// </summary>
    private void SetVolume(float value, string paramName)
    {
        if (mainMixer == null) return;

        if (value > 0)
            mainMixer.SetFloat(paramName, Mathf.Log10(value) * 20f);
        else
            mainMixer.SetFloat(paramName, -80f);
    }

    /// <summary>
    /// Включает или выключает всю музыку и SFX.
    /// Используется при показе рекламы.
    /// </summary>
    public void MuteAll(bool mute)
    {
        if (mainMixer == null) return;

        if (mute)
        {
            mainMixer.SetFloat(MUSIC_VOLUME_PARAM, -80f);
            mainMixer.SetFloat(SFX_VOLUME_PARAM, -80f);
        }
        else
        {
            // Восстанавливаем по сохранённым значениям
            float musicVal = PlayerPrefs.GetFloat(PREF_MUSIC, 0.75f);
            float sfxVal = PlayerPrefs.GetFloat(PREF_SFX, 0.75f);
            SetVolume(musicVal, MUSIC_VOLUME_PARAM);
            SetVolume(sfxVal, SFX_VOLUME_PARAM);

            if (musicSlider != null) musicSlider.value = musicVal;
            if (sfxSlider != null) sfxSlider.value = sfxVal;
        }
    }
}
