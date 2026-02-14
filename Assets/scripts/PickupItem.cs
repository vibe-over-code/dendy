using UnityEngine;

public enum PickupType
{
    Shield,
    AirStrikeCall
}

public class PickupItem : MonoBehaviour
{
    public PickupType type;
    public GameObject pickupEffect;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            Abilities abilities = other.GetComponent<Abilities>();

            if (player && abilities)
            {
                if (type == PickupType.Shield)
                {
                    abilities.ActivateShield();
                }
                else if (type == PickupType.AirStrikeCall)
                {
                    abilities.CallAirStrike(player.transform.position + player.transform.forward * 15f);
                }

                if (pickupEffect) Instantiate(pickupEffect, transform.position, Quaternion.identity);
                Destroy(gameObject);
            }
        }
    }

    void Update()
    {
        // Вращение предмета
        transform.Rotate(Vector3.up * 90f * Time.deltaTime);
    }
}