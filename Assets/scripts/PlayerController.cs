using UnityEngine;
using YG; // YandexGame 2.x

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Shooting")]
    public GameObject bulletPrefab;
    public GameObject explosionPrefab;
    public Transform firePoint;
    public float bulletForce = 20f;

    [Header("Mobile Settings")]
    [Tooltip("Включить мобильное управление вручную для теста в редакторе.")]
    public bool mobileTestMode = false;

    private Vector3 moveDirection;
    private bool isMobile;
    private bool isDragging;

    // --- Джойстик ---
    private Vector2 joystickCenter;
    private Vector2 joystickInput;
    private float joystickRadius = 80f;

    void Start()
    {
        // Определение устройства через YG2 + ручная галочка
        isMobile = mobileTestMode || Application.isMobilePlatform;
        joystickCenter = new Vector2(150, 150);
    }

    void Update()
    {
        if (isMobile)
        {
            HandleTouchInput();
        }
        else
        {
            HandleKeyboardInput();
        }

        if (moveDirection != Vector3.zero)
        {
            transform.forward = moveDirection;
            transform.position += moveDirection * moveSpeed * Time.deltaTime;
        }
    }

    void HandleKeyboardInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        moveDirection = new Vector3(h, 0, v).normalized;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Shoot();
        }
    }

    void HandleTouchInput()
    {
        moveDirection = Vector3.zero;

        if (Input.touchCount > 0)
        {
            foreach (Touch touch in Input.touches)
            {
                if (touch.position.x < Screen.width / 2)
                {
                    Vector2 pos = touch.position;

                    if (touch.phase == TouchPhase.Began)
                    {
                        joystickCenter = pos;
                        isDragging = true;
                    }
                    else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                    {
                        Vector2 dir = pos - joystickCenter;
                        dir = Vector2.ClampMagnitude(dir, joystickRadius);
                        joystickInput = dir / joystickRadius;

                        moveDirection = new Vector3(joystickInput.x, 0, joystickInput.y);
                    }
                    else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        isDragging = false;
                        joystickInput = Vector2.zero;
                    }
                }
                else if (touch.position.x > Screen.width / 2 && touch.phase == TouchPhase.Began)
                {
                    Shoot();
                }
            }
        }
        else
        {
            // поддержка мыши для теста в редакторе
#if UNITY_EDITOR
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 pos = Input.mousePosition;
                if (pos.x < Screen.width / 2)
                {
                    joystickCenter = pos;
                    isDragging = true;
                }
                else
                {
                    Shoot();
                }
            }
            else if (Input.GetMouseButton(0) && isDragging)
            {
                Vector2 pos = Input.mousePosition;
                Vector2 dir = pos - joystickCenter;
                dir = Vector2.ClampMagnitude(dir, joystickRadius);
                joystickInput = dir / joystickRadius;
                moveDirection = new Vector3(joystickInput.x, 0, joystickInput.y);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                joystickInput = Vector2.zero;
            }
#endif
        }
    }

    void Shoot()
    {
        if (firePoint == null || bulletPrefab == null) return;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, transform.rotation);
        Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
        bulletRb.linearVelocity = transform.forward * bulletForce;

        bullet bulletScript = bullet.GetComponent<bullet>();
        if (bulletScript != null)
        {
            bulletScript.explosionPrefab = explosionPrefab;
        }
    }

    void OnGUI()
    {
        if (!isMobile) return;

        // Простая визуализация джойстика
        if (isDragging)
        {
            GUI.color = new Color(1, 1, 1, 0.15f);
            GUI.DrawTexture(new Rect(
                joystickCenter.x - joystickRadius,
                Screen.height - joystickCenter.y - joystickRadius,
                joystickRadius * 2, joystickRadius * 2), Texture2D.whiteTexture);

            Vector2 knobPos = joystickCenter + joystickInput * joystickRadius;
            GUI.color = new Color(1, 1, 1, 0.5f);
            GUI.DrawTexture(new Rect(
                knobPos.x - 25,
                Screen.height - knobPos.y - 25,
                50, 50), Texture2D.whiteTexture);
        }
        else
        {
            GUI.color = new Color(1, 1, 1, 0.1f);
            GUI.DrawTexture(new Rect(
                joystickCenter.x - 15,
                Screen.height - joystickCenter.y - 15,
                30, 30), Texture2D.whiteTexture);
        }
    }
}
