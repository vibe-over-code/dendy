using UnityEngine;

// Этот скрипт висит на самой сфере щита и ждет касаний
[RequireComponent(typeof(SphereCollider))]
public class ShieldDetector : MonoBehaviour
{
    private Abilities parentAbilities;
    private SphereCollider col;

    void Start()
    {
        // Ищем компонент способностей у родителя (танка)
        parentAbilities = GetComponentInParent<Abilities>();

        col = GetComponent<SphereCollider>();
        col.isTrigger = true; // Важно! Делаем триггером, чтобы ловить все касания

        // Настраиваем слой, чтобы ловить пули
        // Убедитесь, что у вас есть слой для пуль (например, "Projectile")
        // Или просто используйте Default, если пули в Default.
        // gameObject.layer = LayerMask.NameToLayer("YourShieldLayer"); 
    }

    // Сюда будут прилетать пули (если они Trigger) или другие объекты
    void OnTriggerEnter(Collider other)
    {
        if (other.transform.root == transform.root) return;

        // Ловим точку контакта. У триггера её нет напрямую, 
        // поэтому берем ближайшую точку на коллайдере к объекту
        Vector3 contactPoint = other.ClosestPointOnBounds(transform.position);

        if (other.CompareTag("Bullet") || other.CompareTag("Enemy"))
        {
            if (parentAbilities != null)
            {
                parentAbilities.OnShieldHit(contactPoint);
            }
        }
    }
}
