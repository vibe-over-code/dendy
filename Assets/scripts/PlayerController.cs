using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public GameObject bulletPrefab;
    public GameObject explosionPrefab;
    public Transform firePoint;
    public float bulletForce = 20f;

    private Vector3 moveDirection;

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        moveDirection = new Vector3(h, 0, v).normalized;

        if (moveDirection != Vector3.zero)
        {
            transform.forward = moveDirection;
            transform.position += moveDirection * moveSpeed * Time.deltaTime;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Shoot();
        }
    }

    void Shoot()
    {
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, transform.rotation);
        Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
        bulletRb.linearVelocity = transform.forward * bulletForce;

        // Передаём префаб взрыва в снаряд
        bullet bulletScript = bullet.GetComponent<bullet>();
        if (bulletScript != null)
        {
            bulletScript.explosionPrefab = explosionPrefab;
        }
    }
}
