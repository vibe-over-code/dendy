using UnityEngine;
using UnityEngine.SceneManagement;

public class bullet : MonoBehaviour
{
    public float lifetime = 5f;
    public GameObject explosionPrefab;
    public GameObject explosionPrefabbar;
    public AudioSource endSound;

    [Header("–адиусы взрыва")]
    public float innerRadius = 2f;      // ближний радиус Ч уничтожение
    public float outerRadius = 5f;      // дальний радиус Ч физический толчок

    [Header("‘изика")]
    public float explosionForce = 700f; // сила толчка
    public float upwardsModifier = 0f;  // вертикальна€ составл€юща€ AddExplosionForce
    public LayerMask layerMask = ~0;    // какие слои затрагивает

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnCollisionEnter(Collision other)
    {
        // --- обычное попадание во врага или стену ---
        if (other.gameObject.CompareTag("Enemy") || other.gameObject.CompareTag("Wall"))
        {
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);

            Destroy(other.gameObject);
            Destroy(gameObject);
            return;
        }

        // --- попадание в игрока ---
        if (other.gameObject.CompareTag("Player"))
        {
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);

            Destroy(other.gameObject);
            Destroy(gameObject);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            if (endSound != null) endSound.Play();
            return;
        }

        // --- попадание в бочку ---
        if (other.gameObject.CompareTag("Barrel"))
        {
            Destroy(other.gameObject);
            Destroy(gameObject);

            // создаЄм эффект взрыва
            if (explosionPrefabbar != null)
                Instantiate(explosionPrefabbar, transform.position, Quaternion.identity);

            // находим все объекты в радиусе outerRadius
            Collider[] hits = Physics.OverlapSphere(transform.position, outerRadius, layerMask);

            foreach (var hit in hits)
            {
                GameObject go = hit.gameObject;
                float dist = Vector3.Distance(transform.position, go.transform.position);

                // --- ¬нутренний радиус: всЄ уничтожаем (кроме игрока) ---
                if (dist <= innerRadius && !go.CompareTag("Player"))
                {
                    if (go.CompareTag("Enemy") || go.CompareTag("Wall") || go.CompareTag("Barrel"))
                    {
                        Destroy(go);
                        continue;
                    }
                    if (go.CompareTag("Player"))
                    {
                        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    }
                }

                // --- ¬нешний радиус: физический толчок ---
                Rigidbody rb = hit.attachedRigidbody;
                if (rb != null)
                {
                    // сила затухает от центра
                    float t = Mathf.Clamp01(1f - (dist / outerRadius));
                    float force = explosionForce * t;
                    rb.AddExplosionForce(force, transform.position, outerRadius, upwardsModifier, ForceMode.Impulse);
                }
                
            }
            return;
        }

        // --- попадание в любой другой объект ---
        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    // визуализаци€ радиусов взрыва в редакторе
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.6f); // внутренний (красный)
        Gizmos.DrawWireSphere(transform.position, innerRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f); // внешний (оранжевый)
        Gizmos.DrawWireSphere(transform.position, outerRadius);
    }
}
