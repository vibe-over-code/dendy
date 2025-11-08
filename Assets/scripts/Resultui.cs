using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

public class Resultui : MonoBehaviour
{
    public GameObject player;
    public TextMeshProUGUI Resulttext;
    public GameObject resui;
    public bool isdeath;
    public ShopManager shpm;
    public int resmoney;
    public GameObject canvas;

    private bool hasShownResult = false;

    void Update()
    {
        if (isdeath && !hasShownResult)
        {
            Dead();
            hasShownResult = true;
        }
    }

    private void Dead()
    {
        canvas.SetActive(false);
        int bestscore = PlayerPrefs.GetInt("bestscore", 0);
        string localized = Localizator.Get("score");
        string localized2 = Localizator.Get("bestscore");
        Resulttext.text =  localized2 + ": " + bestscore + "\n"+ "\n" + localized + ": " + resmoney;
        resui.SetActive(true);
        Time.timeScale = 0f;

        EventSystem.current.SetSelectedGameObject(null);

        var controller = player.GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = false;
    }

    public void Live()
    {
        canvas.SetActive(true);
        shpm.AddCoins(resmoney);
        resui.SetActive(false);
        Time.timeScale = 1f;

        isdeath = false;
        hasShownResult = false; // сбрасываем, чтобы при следующей смерти снова показать

        EventSystem.current.SetSelectedGameObject(null);

        var controller = player.GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = true;
    }
}
