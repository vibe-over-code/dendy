using UnityEngine;
using YG; // YandexGame 2.x
using System.Linq; // Для LINQ (FindFirstOf)

public class PlayerController : MonoBehaviour
{
    // *** НОВЫЕ ССЫЛКИ ***
    private MapGenerator mapGenerator;

    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Shooting - Raycast")]
    // Убираем bulletPrefab, но оставляем explosionPrefab для эффектов на месте попадания
    public GameObject explosionPrefab;
    public Transform firePoint;

    [Header("Explosion Settings")]
    public GameObject explosionPrefabBarrel; // Префаб для мощного взрыва
    public float innerRadius = 2f;       // ближний радиус — уничтожение
    public float outerRadius = 5f;       // дальний радиус — физический толчок
    public float explosionForce = 700f; // сила толчка
    public float upwardsModifier = 0f;  // вертикальная составляющая
    public LayerMask explosionMask;

    // **ВАЖНО:** Теперь это не force (сила), а дальность стрельбы
    [Tooltip("Максимальная дальность луча стрельбы")]
    public float maxShootDistance = 100f;

    // С какими объектами будем взаимодействовать
    public LayerMask shootableMask;

    [Header("Visuals")]
    [Tooltip("Префаб следа (должен содержать LineRenderer)")]
    public GameObject trailPrefab;
    public float trailDuration = 0.1f; // Как долго виден след

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
        // Ищем MapGenerator
        mapGenerator = FindObjectOfType<MapGenerator>();
        if (mapGenerator == null)
        {
            Debug.LogError("MapGenerator не найден! Разрушаемость блоков работать не будет.");
        }

        // Определение устройства через YG2 + ручная галочка
        isMobile = mobileTestMode || Application.isMobilePlatform;
        joystickCenter = new Vector2(150, 150);
    }

    void Update()
    {
        // ... (Код HandleTouchInput и HandleKeyboardInput без изменений)
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
            // Используем CharacterController или Rigidbody для движения, 
            // чтобы не провалиться сквозь пол. (Здесь используется прямое смещение)
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

    // ... (Код HandleTouchInput без изменений)
    void HandleTouchInput()
    {
        // ... (весь код джойстика)
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


    /// <summary>
    /// Выстрел лучом (Raycast)
    /// </summary>
    void Shoot()
    {
        if (firePoint == null) return;

        RaycastHit hit;

        // 1. Выпускаем луч от точки выстрела в направлении forward
        if (Physics.Raycast(firePoint.position, transform.forward, out hit, maxShootDistance, shootableMask))
        {
            // Успешное попадание!

            // 2. Обрабатываем логику попадания
            HandleRaycastHit(hit);

            // 3. Рисуем след до точки попадания
            DrawTrail(firePoint.position, hit.point);
        }
        else
        {
            // 4. Промах. Рисуем след на максимальную дистанцию
            Vector3 endPoint = firePoint.position + transform.forward * maxShootDistance;
            DrawTrail(firePoint.position, endPoint);
        }
    }

    // ... (после DrawTrail)

    /// <summary>
    /// Логика взрыва бочки (удаление/отбрасывание объектов)
    /// </summary>
    void ExplodeBarrel(Vector3 explosionCenter)
    {
        // Создаем визуальный эффект
        if (explosionPrefabBarrel != null)
            Instantiate(explosionPrefabBarrel, explosionCenter, Quaternion.identity);

        // Находим все объекты в радиусе outerRadius
        // Используем Physics.OverlapSphereNonAlloc для оптимизации, 
        // но оставим OverlapSphere, так как код требует минимальных изменений
        Collider[] hits = Physics.OverlapSphere(explosionCenter, outerRadius, explosionMask);

        foreach (var hit in hits)
        {
            GameObject affectedGO = hit.gameObject;
            // Игнорируем сам триггер взрыва или другие частицы
            if (affectedGO.CompareTag("Untagged")) continue;

            float dist = Vector3.Distance(explosionCenter, affectedGO.transform.position);

            // --- Внутренний радиус: Стены, Бочки, Враги — уничтожаем ---
            if (dist <= innerRadius)
            {
                // Если попадаем в бочку или врага - уничтожаем напрямую.
                if (affectedGO.CompareTag("Enemy") || affectedGO.CompareTag("Barrel"))
                {
                    Destroy(affectedGO);
                    continue;
                }
                // Если попадаем в СТЕНУ, просим MapGenerator удалить блок.
                else if (affectedGO.CompareTag("Wall"))
                {
                    if (mapGenerator != null)
                    {
                        // 💥 ИСПРАВЛЕНИЕ: Мы не можем использовать affectedGO.transform.position,
                        // потому что это позиция родительского чанка (0, 0, 0). 
                        // Вместо этого используем центр взрыва, чтобы определить блок.
                        mapGenerator.RemoveBlock(explosionCenter);
                    }
                    // После удаления стены она исчезнет при следующем кадре.
                    continue;
                }
            }

            // --- Внешний радиус (для всех, включая игрока): физический толчок ---
            Rigidbody rb = hit.attachedRigidbody;

            // Проверяем, есть ли Rigidbody (нужен для игрока, чтобы его отбросило)
            if (rb != null)
            {
                // Сила затухает от центра
                float t = Mathf.Clamp01(1f - (dist / outerRadius));
                float force = explosionForce * t;
                rb.AddExplosionForce(force, explosionCenter, outerRadius, upwardsModifier, ForceMode.Impulse);
            }
        }
    }
    // ...

    /// <summary>
    /// Логика обработки попадания (аналог OnCollisionEnter из пули)
    /// </summary>
    void HandleRaycastHit(RaycastHit hit)
    {
        GameObject go = hit.collider.gameObject;

        // --- Попадание в стену или бочку ---
        if (go.CompareTag("Wall") || go.CompareTag("Barrel"))
        {
            // Добавляем маленький эффект попадания для всех разрушаемых объектов
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, hit.point, Quaternion.identity);

            if (go.CompareTag("Barrel"))
            {
                // 💥 БОЧКА ВЗРЫВАЕТСЯ!
                // 1. Вызываем взрыв (чтобы разрушить окружение)
                ExplodeBarrel(hit.point);

                // 2. Уничтожаем саму бочку (ее префаб-объект)
                Destroy(go);
            }
            else // Стена
            {
                if (mapGenerator != null)
                {
                    // Стены удаляем через MapGenerator
                    mapGenerator.RemoveBlock(hit.point);
                }
            }
        }
        // --- Попадание во врага ---
        else if (go.CompareTag("Enemy"))
        {
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, hit.point, Quaternion.identity);

            Destroy(go); // Убиваем врага
        }
        // --- Попадание в другие объекты ---
        else
        {
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, hit.point, Quaternion.identity);
        }
    }

    // ... (Код DrawTrail и OnGUI без изменений)
    /// <summary>
    /// Создает визуальный след с помощью LineRenderer.
    /// </summary>
    void DrawTrail(Vector3 startPoint, Vector3 endPoint)
    {
        if (trailPrefab == null) return;

        // Создаем объект следа
        GameObject trail = Instantiate(trailPrefab, startPoint, Quaternion.identity);
        LineRenderer lr = trail.GetComponent<LineRenderer>();

        if (lr != null)
        {
            lr.positionCount = 2;
            lr.SetPosition(0, startPoint);
            lr.SetPosition(1, endPoint);
        }

        // Уничтожаем след через короткое время, чтобы он не висел вечно
        Destroy(trail, trailDuration);
    }

    // ... (Код OnGUI без изменений)
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