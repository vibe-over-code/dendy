using UnityEngine;

public class Menu : MonoBehaviour
{
    [SerializeField]
    private GameObject m_Menu;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ShopManager.Instance.isInMenu = false;
            m_Menu.SetActive(false);
        }
    }

    public void menuOpen() 
    {
        m_Menu.SetActive(true);
        ShopManager.Instance.isInMenu = true;
    }

    public void menuClose() { m_Menu.SetActive(false); ShopManager.Instance.isInMenu = false; }
}
