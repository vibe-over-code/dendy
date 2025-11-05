using UnityEngine;
using YG; // YandexGame 2.x
using System.Linq;

public class PlayerController : MonoBehaviour
{
    private MapGenerator mapGenerator;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 720f; // скорость поворота за мышкой

    [Header("Shooting - Raycast")]
    public GameObject explosionPrefab;
    public Transform firePoint;
    [Header("Настройки трассера (Эмуляция полета)")]
    public Material trailMaterial;
    public float trailSpeed = 50f;
    public float trailLength = 0.5f;
    public float trailDuration = 0.5f;
    public Color trailColor = Color.yellow;
    public float trailWidth = 0.2f;

    [Header("Explosion Settings")]
    public GameObject explosionPrefabBarrel;
    public float innerRadius = 2f;
    public float outerRadius = 5f;
    public float explosionForce = 700f;
    public float upwardsModifier = 0f;
    public LayerMask explosionMask;
    public float explosionRadius = 5f;
    public int rayCount = 16;
    public float rayHeight = 0.5f;

    [Tooltip("Максимальная дальность луча стрельбы")]
    public float maxShootDistance = 100f;
    public LayerMask shootableMask;

    [Header("Mobile Settings")]
    public bool mobileTestMode = false;

    private Vector3 moveInput; // 👈 заменил moveDirection на moveInput
    private bool isMobile;
    private bool isDragging;
    private Vector2 joystickCenter;
    private Vector2 joystickInput;
    private float joystickRadius = 80f;

    private Camera mainCamera;

    void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();
        if (mapGenerator == null)
            Debug.LogError("MapGenerator не найден! Разрушаемость блоков работать не будет.");

        isMobile = mobileTestMode || Application.isMobilePlatform;
        joystickCenter = new Vector2(150, 150);
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (isMobile)
            HandleTouchInput();
        else
            HandleKeyboardInput();

        // Движение относительно поворота
        MoveRelativeToRotation();

        // Поворот за мышкой только на ПК
        if (!isMobile)
            RotateTowardsMouse();
    }

    // 👇 движение относительно направления танка
    void MoveRelativeToRotation()
    {
        if (moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 move = transform.TransformDirection(moveInput) * moveSpeed * Time.deltaTime;
            transform.position += move;
        }
    }

    // 👇 вращение за мышкой
    void RotateTowardsMouse()
    {
        if (mainCamera == null) return;

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            Vector3 direction = (worldPoint - transform.position);
            direction.y = 0;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }
    }

    void HandleKeyboardInput()
    {
        //float h = Input.GetAxisRaw("Horizontal"); // A / D
        float v = Input.GetAxisRaw("Vertical");   // W / S
        moveInput = new Vector3(0, 0, v).normalized;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            Shoot();
    }

    void HandleTouchInput()
    {
        moveInput = Vector3.zero;

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
                        moveInput = new Vector3(joystickInput.x, 0, joystickInput.y);
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

#if UNITY_EDITOR
        else
        {
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
                moveInput = new Vector3(joystickInput.x, 0, joystickInput.y);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                joystickInput = Vector2.zero;
            }
        }
#endif
    }

    // ------------------ стрельба ------------------

    void Shoot()
    {
        if (firePoint == null) return;

        RaycastHit hit;

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

    public void ExplodeBarrel(Vector3 explosionCenter)
    {
        if (explosionPrefabBarrel != null)
        {
            GameObject explosion = Instantiate(
                explosionPrefabBarrel,
                explosionCenter + Vector3.up * 0.1f,
                Quaternion.identity
            );
            Destroy(explosion, 1f);
        }

        Collider[] damageHits = Physics.OverlapSphere(explosionCenter, explosionRadius);

        foreach (var hit in damageHits)
        {
            GameObject go = hit.gameObject;
            float dist = Vector3.Distance(explosionCenter, go.transform.position);
            string tag = go.tag;

            if (tag == "Enemy" || tag == "Player")
            {
                float maxDamage = 50f;
                float t = Mathf.InverseLerp(explosionRadius, innerRadius, dist);
                float damage = Mathf.Lerp(0, maxDamage, t);

                Rigidbody rb = go.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    float force = damage * 0.5f;
                    Vector3 dir = (go.transform.position - explosionCenter).normalized;
                    rb.AddForce(dir * force, ForceMode.Impulse);
                }
            }
        }

        if (mapGenerator != null)
        {
            Vector3 rayStart = explosionCenter + Vector3.up * rayHeight;

            for (int i = 0; i < rayCount; i++)
            {
                float angle = i * (360f / rayCount);
                Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;

                if (Physics.Raycast(rayStart, direction, out RaycastHit rayHit, explosionRadius))
                {
                    GameObject go = rayHit.collider.gameObject;
                    if (go.CompareTag("Wall"))
                    {
                        Vector3 insidePoint = rayHit.point - rayHit.normal * 0.1f;
                        mapGenerator.RemoveBlock(insidePoint);
                    }
                }
            }
        }
    }

    void HandleRaycastHit(RaycastHit hit)
    {
        GameObject go = hit.collider.gameObject;
        if (explosionPrefab != null)
            Instantiate(explosionPrefab, hit.point, Quaternion.identity);

        if (go.CompareTag("Wall"))
        {
            if (mapGenerator != null)
            {
                Vector3 insidePoint = hit.point - hit.normal * 0.1f;
                mapGenerator.RemoveBlock(insidePoint);
            }
        }
        else if (go.CompareTag("Barrel"))
        {
            Vector3 explosionCenter = go.transform.position;
            ExplodeBarrel(explosionCenter);
            Destroy(go);
        }
        else if (go.CompareTag("Enemy"))
        {
            Instantiate(explosionPrefab, hit.point, Quaternion.identity);
            Destroy(go);
        }
    }

    void DrawTrail(Vector3 start, Vector3 end)
    {
        if (trailMaterial == null) return;

        GameObject trailGO = new GameObject("ProjectileTrail");
        ProjectileTrail trail = trailGO.AddComponent<ProjectileTrail>();

        trail.Initialize(
            start, end,
            trailSpeed, trailLength, trailWidth,
            trailColor, trailMaterial, trailDuration
        );
    }

    void OnGUI()
    {
        if (!isMobile) return;

        if (isDragging)
        {
            GUI.color = new Color(1, 1, 1, 0.15f);
            GUI.DrawTexture(new Rect(
                joystickCenter.x - joystickRadius,
                Screen.height - joystickCenter.y - joystickRadius,
                joystickRadius * 2, joystickRadius * 2),
                Texture2D.whiteTexture);

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
