using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class VolumeMenuController : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject volumeMenu;  // Панель меню (Canvas или Panel)
    public Button toggleButton;    // Кнопка для открытия меню
    public Slider volumeSlider;    // Слайдер громкости

    [Header("Gameplay References")]
    public GameObject player;      // Игрок, чтобы можно было отключить его управление

    private bool isMenuOpen = false;

    private void Start()
    {
        // Гарантируем наличие EventSystem
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        if (volumeMenu != null)
            volumeMenu.SetActive(false);

        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleMenu);

        if (volumeSlider != null)
        {
            if (MusicManager.Instance != null)
                volumeSlider.value = MusicManager.Instance.volume;

            var nav = volumeSlider.navigation;
            nav.mode = Navigation.Mode.None;
            volumeSlider.navigation = nav;

            volumeSlider.onValueChanged.AddListener(OnSliderChanged);
        }
    }

    private void ToggleMenu()
    {
        if (volumeMenu == null) return;

        isMenuOpen = !isMenuOpen;
        volumeMenu.SetActive(isMenuOpen);

        if (isMenuOpen)
        {
            EventSystem.current.SetSelectedGameObject(null);
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
            EventSystem.current.SetSelectedGameObject(null);
            Time.timeScale = 1f; // Возвращаем время

            // Возвращаем управление игроку
            if (player != null)
            {
                var controller = player.GetComponent<PlayerController>();
                if (controller != null)
                    controller.enabled = true;
            }
        }
    }

    private void OnSliderChanged(float value)
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetVolume(value);
    }
}
