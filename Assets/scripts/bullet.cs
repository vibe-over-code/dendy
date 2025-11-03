using UnityEngine;
using UnityEngine.SceneManagement;

public class bullet : MonoBehaviour
{
    // --- Добавляем ссылку на наш Генератор ---
    private MapGenerator mapGenerator;

    public float lifetime = 5f;
    public GameObject explosionPrefab;
    public GameObject explosionPrefabbar;
    public AudioSource endSound;

    [Header("Радиусы взрыва")]
    public float innerRadius = 2f;       // ближний радиус — уничтожение
    public float outerRadius = 5f;       // дальний радиус — физический толчок

    [Header("Физика")]
    public float explosionForce = 700f; // сила толчка
    public float upwardsModifier = 0f;  // вертикальная составляющая AddExplosionForce
    public LayerMask layerMask = ~0;    // какие слои затрагивает

    void Start()
    {
        // Ищем наш генератор карты на старте
        mapGenerator = FindObjectOfType<MapGenerator>();
        if (mapGenerator == null)
        {
            Debug.LogError("MapGenerator не найден на сцене! Разрушаемость блоков работать не будет.");
        }

        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// Уничтожает блоки (стены/бочки) с использованием логики MapGenerator,
    /// чтобы меши чанков перестраивались корректно.
    /// </summary>
    void HandleBlockDestruction(GameObject target)
    {
        // Врагов и прочие объекты (без тега Wall/Barrel) можно просто Destroy
        if (!target.CompareTag("Wall") && !target.CompareTag("Barrel"))
        {
            Destroy(target);
            return;
        }

        // Если это стена или бочка, используем MapGenerator
        if (mapGenerator != null)
        {
            // Используем RemoveBlock, чтобы обновить данные чанка и перестроить меши
            mapGenerator.RemoveBlock(target.transform.position);

            // ВАЖНО: Мы не вызываем тут Destroy(target)! 
            // MapGenerator.RemoveBlock уже удалил бочку (если это была она)
            // и инициировал перестройку меша (если это была стена).
        }
        else
        {
            // Если генератора нет, просто удаляем объект (как запасной вариант)
            Destroy(target);
        }
    }


    void OnCollisionEnter(Collision other)
    {
        // ВАЖНО: Используем transform.position, чтобы MapGenerator вычислил правильный центр ячейки
        Vector3 hitPosition = transform.position;
        GameObject go = other.gameObject;

        // --- обычное попадание во врага или стену ---
        if (go.CompareTag("Enemy") || go.CompareTag("Wall"))
        {
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);

            // Используем новый метод для разрушения (для Wall)
            // Для Enemy просто уничтожаем, если не используем систему здоровья
            if (go.CompareTag("Wall"))
            {
                HandleBlockDestruction(go);
            }
            else
            {
                Destroy(go);
            }

            Destroy(gameObject);
            return;
        }

        // --- попадание в игрока (тут игрок должен умереть, если попала пуля врага) ---
        if (go.CompareTag("Player"))
        {
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);

            // Если пуля попадает напрямую, предполагаем, что игрок умирает
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            if (endSound != null) endSound.Play();

            Destroy(gameObject);
            return;
        }

        // --- попадание в бочку (ВЗРЫВ) ---
        if (go.CompareTag("Barrel"))
        {
            // 1. Сначала удаляем бочку, которая вызвала взрыв
            HandleBlockDestruction(go);

            if (explosionPrefabbar != null)
                Instantiate(explosionPrefabbar, transform.position, Quaternion.identity);

            // 2. Находим все объекты в радиусе outerRadius
            Collider[] hits = Physics.OverlapSphere(hitPosition, outerRadius, layerMask);

            foreach (var hit in hits)
            {
                GameObject affectedGO = hit.gameObject;
                float dist = Vector3.Distance(hitPosition, affectedGO.transform.position);

                // --- Внутренний радиус: Стены, Бочки, Враги — уничтожаем ---
                if (dist <= innerRadius)
                {
                    if (affectedGO.CompareTag("Enemy") || affectedGO.CompareTag("Wall") || affectedGO.CompareTag("Barrel"))
                    {
                        // Используем HandleBlockDestruction, если это блок, иначе просто Destroy
                        if (affectedGO.CompareTag("Wall") || affectedGO.CompareTag("Barrel"))
                        {
                            HandleBlockDestruction(affectedGO);
                        }
                        else
                        {
                            Destroy(affectedGO); // Уничтожаем врага
                        }
                        continue; // Переходим к следующему объекту
                    }
                }

                // --- Внешний радиус (для всех, включая игрока!): физический толчок ---
                Rigidbody rb = hit.attachedRigidbody;

                // Нам нужно, чтобы Rigidbody был И у игрока, чтобы его отбросило
                if (rb != null)
                {
                    // Сила затухает от центра
                    float t = Mathf.Clamp01(1f - (dist / outerRadius));
                    float force = explosionForce * t;
                    rb.AddExplosionForce(force, hitPosition, outerRadius, upwardsModifier, ForceMode.Impulse);
                }

                // !!! ИГРОКА НЕ ТРОГАЕМ (НЕ УНИЧТОЖАЕМ) !!!
                // Мы удалили всю логику SceneManager.LoadScene из этого цикла.
            }

            Destroy(gameObject);
            return;
        }

        // --- попадание в любой другой объект ---
        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    // (OnDrawGizmosSelected остается без изменений)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.6f); // внутренний (красный)
        Gizmos.DrawWireSphere(transform.position, innerRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f); // внешний (оранжевый)
        Gizmos.DrawWireSphere(transform.position, outerRadius);
    }
}