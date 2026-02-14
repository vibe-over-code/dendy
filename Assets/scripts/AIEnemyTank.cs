using TMPro;
using UnityEngine;

// Требует компонент Abilities (добавь его на префаб врага!)
[RequireComponent(typeof(Abilities))]
public class AIEnemyTank : MonoBehaviour
{
    public Transform player;
    public float moveSpeed = 5f;
    public GameObject explosionPrefab;
    public Transform firePoint;
    public float shootCooldown = 2f;
    public float detectionRadius = 30f;
    public float wallCheckDistance = 5f;
    public int shotDistance = 5;
    [Range(0f, 1f)] public float accuracy = 0.9f;

    [Header("Boss Settings")]
    public bool isBoss = false;
    public int maxHealth = 1; // У обычных врагов 1, у босса 3-5
    private int currentHealth;
    public float airStrikeCooldown = 10f; // Как часто босс бомбит
    private float airStrikeTimer;

    public MapGenerator mapGenerator;
    public PlayerController playerController;
    public Resultui resultUI;

    private float shootTimer;
    private Quaternion targetRotation;
    private Abilities abilities; // Ссылка на способности

    void Start()
    {
        abilities = GetComponent<Abilities>();
        currentHealth = maxHealth;

        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        targetRotation = transform.rotation;

        // Если это босс, даем ему щит на старте с шансом 50%
        if (isBoss && Random.value > 0.5f)
        {
            abilities.ActivateShield();
        }
    }

    void Update()
    {
        if (!player) return;

        Vector3 dirToPlayer = player.position - transform.position;
        dirToPlayer.y = 0;
        float distance = dirToPlayer.magnitude;
        if (distance > detectionRadius) return;

        // --- Логика Босса: Авиаудар ---
        if (isBoss)
        {
            airStrikeTimer -= Time.deltaTime;
            // Если игрок близко или просто по таймеру
            if (airStrikeTimer <= 0f && distance < 20f)
            {
                abilities.CallAirStrike(player.position); // Бомбим игрока
                airStrikeTimer = airStrikeCooldown;
            }
        }
        // ------------------------------

        // Проверяем препятствия
        bool blocked = Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, out RaycastHit hit, wallCheckDistance);

        // Движение и поворот (как было)
        Vector3 flatDir = dirToPlayer.normalized;
        float targetAngle = Mathf.Atan2(flatDir.x, flatDir.z) * Mathf.Rad2Deg;
        targetRotation = Quaternion.Euler(0, Mathf.Round(targetAngle / 45f) * 45f, 0);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 180f * Time.deltaTime);
        transform.position += transform.forward * moveSpeed * Time.deltaTime;

        // Стрельба
        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0f)
        {
            Shoot();
            shootTimer = shootCooldown;
        }
    }

    void Shoot()
    {
        if (!firePoint) return;
        Vector3 shootDir = transform.forward;
        if (accuracy < 1f)
        {
            float spread = (1f - accuracy) * 10f;
            shootDir = Quaternion.Euler(0, Random.Range(-spread, spread), 0) * shootDir;
        }

        if (Physics.Raycast(firePoint.position, shootDir, out RaycastHit hit, shotDistance))
        {
            // Визуал (для дебага или line renderer)
            // ...

            if (hit.collider.CompareTag("Player"))
            {
                // Вместо прямого PlayerDead вызываем метод получения урона у игрока
                PlayerController pc = hit.collider.GetComponent<PlayerController>();
                if (pc) pc.TakeHit();
            }
            else if (hit.collider.CompareTag("Wall") && mapGenerator)
            {
                mapGenerator.RemoveBlock(hit.point - hit.normal * 0.1f);
            }
            else if (hit.collider.CompareTag("Barrel") && playerController)
            {
                playerController.ExplodeBarrel(hit.collider.transform.position);
                Destroy(hit.collider.gameObject);
            }

            if (explosionPrefab)
                Instantiate(explosionPrefab, hit.point, Quaternion.identity);
        }
    }

    // НОВЫЙ МЕТОД ПОЛУЧЕНИЯ УРОНА
    public void TakeDamage(int damage = 1)
    {
        // 1. Проверяем щит
        if (!abilities.TryTakeDamage())
        {
            return; // Щит впитал урон
        }

        // 2. Отнимаем HP
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        // Очки: Босс дает больше
        if (playerController)
        {
            playerController.kills += isBoss ? 5 : 1;
        }

        if (explosionPrefab) Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}