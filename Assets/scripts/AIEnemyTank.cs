using TMPro;
using UnityEngine;

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

    public MapGenerator mapGenerator;
    public PlayerController playerController;
    public Resultui resultUI;

    private float shootTimer;
    private Quaternion targetRotation;

    void Start()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
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

        // Проверяем препятствия перед собой
        bool blocked = Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, out RaycastHit hit, wallCheckDistance);

        // Рассчитываем направление и поворот
        Vector3 flatDir = dirToPlayer.normalized;
        float targetAngle = Mathf.Atan2(flatDir.x, flatDir.z) * Mathf.Rad2Deg;
        targetRotation = Quaternion.Euler(0, Mathf.Round(targetAngle / 45f) * 45f, 0);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 180f * Time.deltaTime);

        // Двигаемся
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
            Debug.DrawRay(firePoint.position, shootDir * hit.distance, Color.red, 0.5f);

            if (hit.collider.CompareTag("Player"))
            {
                PlayerDead();
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

    public void TakeDamage()
    {
        Destroy(gameObject);
    }

    void PlayerDead()
    {
        mapGenerator.DestroyAllEnemies();
        //player.transform.position = new Vector3(0, 2.6f, 0);
        player.transform.position=mapGenerator.defaultpos;
        int result = Mathf.FloorToInt(playerController.lifetime / 3 + playerController.kills * 3);

        // Сохраняем рекорд безопасно
        int bestScore = PlayerPrefs.GetInt("bestscore", 0);
        if (result > bestScore)
        {
            PlayerPrefs.SetInt("bestscore", result);
            PlayerPrefs.Save();
            bestScore = result; // обновляем локальную переменную
        }

        string yoursc = Localizator.Get("yorsc");
        string bestsc = Localizator.Get("bestsc");
        // Показываем результат на UI
        resultUI.Resulttext.text = $"yoursc: {result}\nbestsc: {bestScore}";
        resultUI.isdeath = true;
        resultUI.resmoney = result;

        playerController.lifetime = 0;
        playerController.kills = 0;
    }

}
