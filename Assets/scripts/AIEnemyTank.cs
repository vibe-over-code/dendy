using UnityEngine;
using UnityEngine.SceneManagement;

public class AIEnemyTank : MonoBehaviour
{
    public Transform player;
    public float moveSpeed = 5f;
    public GameObject bulletPrefab;
    public GameObject explosionPrefab;
    public Transform firePoint;
    public float bulletForce = 20f;
    public float shootCooldown = 2f;
    [Range(0f, 1f)] public float accuracy = 0.9f;
    public float detectionRadius = 30f;
    public float wallCheckDistance = 5f;
    public int shotDistance = 5;

    public MapGenerator mapGenerator;
    public PlayerController playerController;

    private float shootTimer;
    private Quaternion targetRotation;

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        targetRotation = transform.rotation;
    }

    void Update()
    {
        if (!player) return;

        Vector3 dirToPlayer = player.position - transform.position;
        dirToPlayer.y = 0;
        float distance = dirToPlayer.magnitude;
        if (distance > detectionRadius) return;

        // Проверяем препятствие перед собой
        bool blocked = Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, out RaycastHit hit, wallCheckDistance);

        // Рассчитываем дискретный угол (45° или 90°)
        Vector3 flatDir = dirToPlayer.normalized;
        float targetAngle = Mathf.Atan2(flatDir.x, flatDir.z) * Mathf.Rad2Deg;
        targetAngle = Mathf.Round(targetAngle / 45f) * 45f; // кратно 45°
        targetRotation = Quaternion.Euler(0, targetAngle, 0);

        // Плавно поворачиваемся
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 180f * Time.deltaTime);

        // Двигаемся постоянно
        transform.position += transform.forward * moveSpeed * Time.deltaTime;

        // Стреляем по таймеру
        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0f)
        {
            // Если видит стену — стреляет в неё
            if (blocked && hit.collider.CompareTag("Wall"))
            {
                Instantiate(explosionPrefab, hit.point, Quaternion.identity);
                Shoot();
                shootTimer = shootCooldown;
            }
            else if (distance <= detectionRadius)
            {
                Instantiate(explosionPrefab, hit.point, Quaternion.identity);
                // Стреляет по игроку
                Shoot();
                shootTimer = shootCooldown;
            }
        }
    }

    void Shoot()
    {
        if (!firePoint) return;

        // 1. Рассчитываем направление с учетом неточности
        Vector3 shootDir = transform.forward;
        if (accuracy < 1f)
        {
            float maxAngle = (1f - accuracy) * 10f; // Угол разброса

            // Применяем неточность. Вращение только по Y (горизонталь) для простоты.
            // Если танк может стрелять вверх/вниз, используйте 3D-вращение.
            shootDir = Quaternion.Euler(
                0, // Y-компонента
                Random.Range(-maxAngle, maxAngle), // Горизонтальное отклонение
                0) * shootDir;
        }

        // 2. Выпускаем Raycast
        RaycastHit hit;
        if (Physics.Raycast(firePoint.position, shootDir, out hit, shotDistance))
        {
            // Визуализация луча (для отладки)
            Debug.DrawRay(firePoint.position, shootDir * hit.distance, Color.red, 0.5f);

            GameObject affectedGO = hit.collider.gameObject;

            // 3. Обрабатываем попадание

            if (affectedGO.CompareTag("Player"))
            {
                string currentSceneName = SceneManager.GetActiveScene().name;
                // Перезагружаем текущую сцену
                SceneManager.LoadScene(currentSceneName);
                Debug.Log("AI попал в игрока!");
            }
            else if (affectedGO.CompareTag("Wall") && mapGenerator != null)
            {
                // Разрушение стены: используем логику из MapGenerator
                Vector3 insidePoint = hit.point - hit.normal * 0.1f;
                mapGenerator.RemoveBlock(insidePoint);
                Debug.Log("AI разрушил стену!");
            }
            else if (affectedGO.CompareTag("Barrel") && playerController != null)
            {
                // Взрыв бочки: переиспользуем метод игрока.
                playerController.ExplodeBarrel(affectedGO.transform.position);
                Destroy(affectedGO); // Бочка уничтожается
                Debug.Log("AI взорвал бочку!");
            }
        }
        else
        {
            // Если промахнулись (для отладки)
            Debug.DrawRay(firePoint.position, shootDir * shotDistance, Color.yellow, 0.5f);
        }
    }

    public void TakeDamage()
    {
        Destroy(gameObject);
    }
}
