using UnityEngine;

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
                Shoot();
                shootTimer = shootCooldown;
            }
            else if (distance <= detectionRadius)
            {
                // Стреляет по игроку
                Shoot();
                shootTimer = shootCooldown;
            }
        }
    }

    void Shoot()
    {
        if (!bulletPrefab || !firePoint) return;

        Vector3 shootDir = transform.forward;
        if (accuracy < 1f)
        {
            float maxAngle = (1f - accuracy) * 15f;
            shootDir = Quaternion.Euler(
                Random.Range(-maxAngle, maxAngle),
                Random.Range(-maxAngle, maxAngle),
                0) * shootDir;
        }

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(shootDir));
        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        rb.linearVelocity = shootDir * bulletForce;

        var b = bullet.GetComponent<bullet>();
        if (b != null)
            b.explosionPrefab = explosionPrefab;
    }

    public void TakeDamage()
    {
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Bullet"))
        {
            TakeDamage();
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("Wall"))
        {
            Destroy(other.gameObject);
        }
    }
}
