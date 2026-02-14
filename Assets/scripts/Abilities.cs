using UnityEngine;
using System.Collections;

public class Abilities : MonoBehaviour
{
    [Header("Shield Settings")]
    public GameObject shieldVisual; // ���� ���������� ����� � ��������
    public bool hasShield = false;

    [Header("Air Strike Settings")]
    public GameObject bombPrefab; // ������ �����
    public GameObject warningMarkerPrefab; // ������� ������� ���� (Sprite on floor)
    public float strikeRadius = 15f;
    public int bombCount = 5;

    void Start()
    {
        UpdateShieldVisual();
    }

    public void ActivateShield()
    {
        hasShield = true;
        UpdateShieldVisual();
    }

    public void DeactivateShield()
    {
        hasShield = false;
        UpdateShieldVisual();
    }

    void UpdateShieldVisual()
    {
        if (shieldVisual) shieldVisual.SetActive(hasShield);
    }

    // ������ ��������� ����� � ������ ����
    // ���������� TRUE, ���� ���� ������, � FALSE, ���� ��� �������� ���
    public bool TryTakeDamage()
    {
        if (hasShield)
        {
            DeactivateShield();
            // ��� ����� �������� ���� �������� ����
            return false; // ���� �� ������
        }
        return true; // ���� ������
    }

    // ����� ��������� ������ ����
    public void CallAirStrike(Vector3 targetPosition)
    {
        StartCoroutine(AirStrikeRoutine(targetPosition));
    }

    IEnumerator AirStrikeRoutine(Vector3 center)
    {
        for (int i = 0; i < bombCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * strikeRadius;
            Vector3 strikePos = center + new Vector3(randomCircle.x, 0, randomCircle.y);

            // 1. ���������� ������
            if (warningMarkerPrefab)
            {
                GameObject marker = Instantiate(warningMarkerPrefab, strikePos + Vector3.up * 0.1f, Quaternion.Euler(90, 0, 0));
                Destroy(marker, 2f); // ������� ������ ����� 2 ���
            }

            // 2. ���� � ���������� �����
            yield return new WaitForSeconds(0.5f);

            if (bombPrefab)
            {
                Vector3 spawnPos = strikePos + Vector3.up * 20f; // ����� ������ ������
                GameObject bomb = Instantiate(bombPrefab, spawnPos, Quaternion.identity);
                // � ����� ������ ���� Rigidbody � ������, ������������ ��� �������� (��� � ������� �����)
                // ��� ����� ������ ��������� � ���� �����:
                Rigidbody rb = bomb.GetComponent<Rigidbody>();
                if (rb) rb.linearVelocity = Vector3.down * 20f;
            }
        }
    }
}