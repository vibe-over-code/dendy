using UnityEngine;
using UnityEngine.EventSystems;

public class Menu : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject m_Menu;   // Главное меню
    public GameObject Setting;                    // Панель настроек

    [Header("Gameplay References")]
    public GameObject player;                     // Ссылка на объект игрока

    private bool isMenuOpen = false;

    private void Start()
    {
        // Гарантируем, что есть EventSystem
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        if (m_Menu != null)
            m_Menu.SetActive(false);
    }

    private void Update()
    {
        // Нажатие ESC — открыть/закрыть меню
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isMenuOpen)
                CloseMenu();
            else
                OpenMenu();
        }
    }

    public void OpenMenu()
    {
        if (m_Menu == null) return;

        m_Menu.SetActive(true);
        Setting?.SetActive(false);
        ShopManager.Instance.isInMenu = true;
        isMenuOpen = true;

        // Ставим игру на паузу
        Time.timeScale = 0f;

        // Снимаем фокус с UI
        EventSystem.current.SetSelectedGameObject(null);

        // Блокируем управление игроком
        if (player != null)
        {
            var controller = player.GetComponent<PlayerController>();
            if (controller != null)
                controller.enabled = false;
        }
    }

    public void CloseMenu()
    {
        if (m_Menu == null) return;

        m_Menu.SetActive(false);
        Setting?.SetActive(true);
        ShopManager.Instance.isInMenu = false;
        isMenuOpen = false;

        // Возвращаем время
        Time.timeScale = 1f;

        // Убираем фокус
        EventSystem.current.SetSelectedGameObject(null);

        // Возвращаем управление игроку
        if (player != null)
        {
            var controller = player.GetComponent<PlayerController>();
            if (controller != null)
                controller.enabled = true;
        }
    }
}
