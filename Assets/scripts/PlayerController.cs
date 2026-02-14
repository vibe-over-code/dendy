using UnityEngine;
using YG; // YandexGame 2.x
using System.Linq; // Для LINQ (FindFirstOf)

public class PlayerController : MonoBehaviour
{
    private MapGenerator mapGenerator;
    private Abilities abilities;
    public Resultui resultUI;


    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Shooting - Raycast")]
    public GameObject explosionPrefab;
    public Transform firePoint;
    [Header("Настройки трассера (Эмуляция полета)")]
    public Material trailMaterial;
    public float trailSpeed = 50f;     // Скорость полета трассера
    public float trailLength = 0.5f;   // Длина видимого трассера
    public float trailDuration = 0.5f; // Время жизни объекта (чтобы избежать мусора)
    public Color trailColor = Color.yellow;
    public float trailWidth = 0.2f;

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

    [Tooltip("Максимальная дальность луча стрельбы")]
    public float maxShootDistance = 100f;

    // С какими объектами будем взаимодействовать
    public LayerMask shootableMask;

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
    public int kills;
    public float lifetime;

    void Start()
    {
        abilities = GetComponent<Abilities>();
        mapGenerator = FindObjectOfType<MapGenerator>();
        isMobile = mobileTestMode || Application.isMobilePlatform;

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
        lifetime+= 1f * Time.deltaTime;
        if (isMobile)
        {
            HandleTouchInput();
        }
        else
        {
            HandleKeyboardInput();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            abilities.CallAirStrike(transform.position + transform.forward * 10f);
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



    // Выстрел(Raycast)

    void Shoot()
    {
        if (firePoint == null) return;

        RaycastHit hit;

        // Выпускаем луч
        if (Physics.Raycast(firePoint.position, transform.forward, out hit, maxShootDistance, shootableMask))
        {
            HandleRaycastHit(hit);
            DrawTrail(firePoint.position, hit.point);
        }
        else
        {
            Vector3 endPoint = firePoint.position + transform.forward * maxShootDistance;
            DrawTrail(firePoint.position, endPoint);
        }
    }



    // Логика взрыва бочки (удаление/отбрасывание объектов)
    public void ExplodeBarrel(Vector3 explosionCenter)
    {
        // Простая защита от двойного вызова по той же позиции
        if (_lastExplosionCenter.HasValue && Vector3.Distance(_lastExplosionCenter.Value, explosionCenter) < 0.1f)
            return;
        _lastExplosionCenter = explosionCenter;

        //Debug.Log($"Взрыв бочки в ({explosionCenter.x:F2}, {explosionCenter.y:F2}, {explosionCenter.z:F2}), радиус: {explosionRadius}");

        // I. СПАВН ЭФФЕКТА ВЗРЫВА
        if (explosionPrefabBarrel != null)
        {
            GameObject explosion = Instantiate(
                explosionPrefabBarrel,
                explosionCenter + Vector3.up * 0.1f,
                Quaternion.identity
            );
            Destroy(explosion, 1f);
        }

        // II. ОБРАБОТКА ОБЪЕКТОВ (OverlapSphere)
        Collider[] damageHits = Physics.OverlapSphere(explosionCenter, explosionRadius);

        foreach (var hit in damageHits)
        {
            GameObject affectedGO = hit.gameObject;
            float dist = Vector3.Distance(explosionCenter, affectedGO.transform.position);
            string tag = affectedGO.tag;

            if (tag == "Barrel" && dist <= explosionRadius)
            {
                
            }
            else if (tag == "Enemy" || tag == "Player")
            {
                float maxDamage = 50f;
                float t = Mathf.InverseLerp(explosionRadius, innerRadius, dist);
                float damage = Mathf.Lerp(0, maxDamage, t);

                Rigidbody rb = affectedGO.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    float force = damage * 0.5f;
                    Vector3 direction = (affectedGO.transform.position - explosionCenter).normalized;
                    rb.AddForce(direction * force, ForceMode.Impulse);
                }
            }
        }

        // III. УДАЛЕНИЕ СТЕН (Raycast)
        if (mapGenerator != null)
        {
            Vector3 rayStart = explosionCenter + Vector3.up * rayHeight;
            for (int i = 0; i < rayCount; i++)
            {
                float angle = i * (360f / rayCount);
                Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;

                if (Physics.Raycast(rayStart, direction, out RaycastHit rayHit, explosionRadius))
                {
                    GameObject affectedGO = rayHit.collider.gameObject;
                    if (affectedGO.CompareTag("Wall"))
                    {
                        Vector3 insidePoint = rayHit.point - rayHit.normal * 0.1f;
                        mapGenerator.RemoveBlock(insidePoint);
                    }
                }
            }
        }

        // Через небольшую задержку можно сбросить защиту
        StartCoroutine(ResetLastExplosion(1f));
    }

    private Vector3? _lastExplosionCenter = null;

    private System.Collections.IEnumerator ResetLastExplosion(float delay)
    {
        yield return new WaitForSeconds(delay);
        _lastExplosionCenter = null;
    }

    // Логика обработки попадания
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
            // БОЧКА ВЗРЫВАЕТСЯ!
            // 1. Вызываем взрыв (чтобы разрушить окружение)
            Vector3 explosionCenter = go.transform.position;
            ExplodeBarrel(explosionCenter);

            // 2. Уничтожаем саму бочку
            Destroy(go);
        }

        //Попадание во врага
        if (hit.collider.CompareTag("Enemy"))
        {
            // Вместо Destroy(go) и kills+=1
            AIEnemyTank enemyAI = hit.collider.GetComponent<AIEnemyTank>();
            if (enemyAI)
            {
                enemyAI.TakeDamage(1);
            }
            // Визуальный эффект
            if (explosionPrefab) Instantiate(explosionPrefab, hit.point, Quaternion.identity);
        }
        //Попадание в другие объекты
        else
        {
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, hit.point, Quaternion.identity);
        }
    }

    public void TakeHit()
    {
        // Если щит активен - поглощаем урон
        if (!abilities.TryTakeDamage())
        {
            Debug.Log("Shield Absorbed Damage!");
            return;
        }

        // Если щита нет - умираем
        PlayerDead();
    }

    // Создает визуальный след с помощью LineRenderer.
    // Создает визуальный след с помощью ParticleSystem-трассера
    void DrawTrail(Vector3 startPoint, Vector3 endPoint)
    {
        // Проверяем, что firePoint установлен
        if (firePoint == null) return;

        // Используем статический метод ProjectileTrail для создания кометного хвоста
        ProjectileTrail.DrawTrail(
            startPoint,   // позиция старта
            endPoint,     // позиция конца (или попадания)
            trailWidth,   // ширина хвоста
            trailColor,   // цвет
            trailDuration // время жизни
        );
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

    void PlayerDead()
    {
        Debug.Log("Player Died");

        // Все враги удаляются
        if (mapGenerator) mapGenerator.DestroyAllEnemies();

        // Респы
        transform.position = mapGenerator ? mapGenerator.defaultpos : Vector3.zero;

        // Подсчет очков
        int result = Mathf.FloorToInt(lifetime / 3 + kills * 3);

        // Сохранение (YG или PlayerPrefs)
        int bestScore = PlayerPrefs.GetInt("bestscore", 0);
        if (result > bestScore)
        {
            PlayerPrefs.SetInt("bestscore", result);
            PlayerPrefs.Save();
            bestScore = result;
        }

        // UI
        if (resultUI)
        {
            resultUI.Resulttext.text = $"Score: {result}\nBest: {bestScore}";
            resultUI.isdeath = true;
            resultUI.resmoney = result;
        }

        lifetime = 0;
        kills = 0;

        // Сбросить щит при смерти
        abilities.DeactivateShield();
    }
}