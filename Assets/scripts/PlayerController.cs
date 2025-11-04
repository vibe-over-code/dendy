using UnityEngine;
using YG; // YandexGame 2.x
using System.Linq; // Для LINQ (FindFirstOf)

public class PlayerController : MonoBehaviour
{
    private MapGenerator mapGenerator;

    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Shooting - Raycast")]
    public GameObject explosionPrefab;
    public Transform firePoint;

    [Header("Explosion Settings")]
    public GameObject explosionPrefabBarrel; // Префаб для мощного взрыва
    public float innerRadius = 2f;       // ближний радиус — уничтожение
    public float outerRadius = 5f;       // дальний радиус — физический толчок
    public float explosionForce = 700f; // сила толчка
    public float upwardsModifier = 0f;  // вертикальная составляющая
    public LayerMask explosionMask;
    public float explosionRadius = 5f; // Используйте этот радиус вместо innerRadius
    public int rayCount = 16;         // Сколько лучей выпустить по кругу
    public float rayHeight = 0.5f;    // Высота, с которой исходят лучи (чтобы не застряли в полу)

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



    // Выстрел лучом (Raycast)

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


    // Логика взрыва бочки (удаление/отбрасывание объектов)
    public void ExplodeBarrel(Vector3 explosionCenter)
    {
        Debug.Log($"💥 Взрыв бочки в ({explosionCenter.x:F2}, {explosionCenter.y:F2}, {explosionCenter.z:F2}), радиус: {explosionRadius}");

        // =======================================================
        // I. СПАВН ЭФФЕКТА ВЗРЫВА
        // =======================================================
        if (explosionPrefabBarrel != null)
        {
            // Создаем эффект чуть выше земли, чтобы он не проваливался
            GameObject explosion = Instantiate(
                explosionPrefabBarrel,
                explosionCenter + Vector3.up * 0.1f, // Немного поднять
                Quaternion.identity
            );

            // Уничтожаем эффект через 2 секунды, если система частиц сама не самоуничтожается
            Destroy(explosion, 5f);
        }

        // =======================================================
        // II. ОБНАРУЖЕНИЕ ОБЪЕКТОВ и НАНЕСЕНИЕ УРОНА (OverlapSphere)
        // =======================================================

        // OverlapSphere находит все объекты для нанесения урона/удаления бочек
        Collider[] damageHits = Physics.OverlapSphere(explosionCenter, explosionRadius);

        foreach (var hit in damageHits)
        {
            GameObject affectedGO = hit.gameObject;
            float dist = Vector3.Distance(explosionCenter, affectedGO.transform.position);
            string tag = affectedGO.tag;

            
            if (tag == "Barrel" && dist <= explosionRadius)
            {
                /* Убедитесь, что не пытаетесь взорвать только что взорванную бочку на том же месте
                if (affectedGO.transform.position != explosionCenter)
                {
                    // Рекурсивный взрыв для цепной реакции
                    Vector3 newExplosionCenter = affectedGO.transform.position;
                    Destroy(affectedGO); // Сначала удаляем объект
                    ExplodeBarrel(newExplosionCenter);
                }
                */
            }
            

            // ------------------ Логика Урона (для игрока/врагов) ------------------
            else if (tag == "Enemy" || tag == "Player")
            {
                float maxDamage = 50f;

                // Расчет силы: чем ближе, тем сильнее
                // (t=1 в innerRadius, t=0 в explosionRadius)
                float t = Mathf.InverseLerp(explosionRadius, innerRadius, dist);
                float damage = Mathf.Lerp(0, maxDamage, t);

                Rigidbody rb = affectedGO.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    float force = damage * 0.5f; // Конвертируем урон в силу отталкивания
                    Vector3 direction = (affectedGO.transform.position - explosionCenter).normalized;

                    // Добавляем силу, направленную от центра взрыва
                    rb.AddForce(direction * force, ForceMode.Impulse);
                    // Опционально: AddExplosionForce, если вы хотите более "физичный" взрыв

                    // Debug.Log($"Применяем силу к {affectedGO.name}: {force:F4}");
                }
            }
        }

        // =======================================================
        // III. РАЗРУШЕНИЕ СТЕН (Raycast Explosion)
        // =======================================================

        if (mapGenerator != null)
        {
            // Лучи начинаются чуть выше пола, чтобы избежать застревания
            Vector3 rayStart = explosionCenter + Vector3.up * rayHeight;

            for (int i = 0; i < rayCount; i++)
            {
                float angle = i * (360f / rayCount);

                // Направление в горизонтальной плоскости (XZ)
                Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;

                RaycastHit rayHit;

                // Выпускаем луч
                if (Physics.Raycast(rayStart, direction, out rayHit, explosionRadius))
                {
                    GameObject affectedGO = rayHit.collider.gameObject;

                    if (affectedGO.CompareTag("Wall"))
                    {
                        // Ключевой момент: смещение точки попадания внутрь блока (0.1f)
                        // для правильной привязки к сетке в MapGenerator.RemoveBlock
                        Vector3 insidePoint = rayHit.point - rayHit.normal * 0.1f;

                        mapGenerator.RemoveBlock(insidePoint);

                        // Debug.DrawRay(rayStart, direction * rayHit.distance, Color.red, 2f);
                    }
                }
            }
        }
    }


    // Логика обработки попадания (аналог OnCollisionEnter из пули)
    void HandleRaycastHit(RaycastHit hit)
    {
        GameObject go = hit.collider.gameObject;
        if (explosionPrefab != null)
            Instantiate(explosionPrefab, hit.point, Quaternion.identity);
        
        if (go.CompareTag("Wall"))
        {
            if (mapGenerator != null)
            {
                // Берём точку чуть внутри блока откуда пришёл луч
                Vector3 insidePoint = hit.point - hit.normal * 0.1f;
                mapGenerator.RemoveBlock(insidePoint);
            }
        }

        else if (go.CompareTag("Barrel"))
        {
            // 💥 БОЧКА ВЗРЫВАЕТСЯ!
            // 1. Вызываем взрыв (чтобы разрушить окружение)
            Vector3 explosionCenter = go.transform.position;
            ExplodeBarrel(explosionCenter);

            // 2. Уничтожаем саму бочку (ее префаб-объект)
            Destroy(go);
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

    // Создает визуальный след с помощью LineRenderer.
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