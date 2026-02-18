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

    private Material shieldMat;

    private int hitPosID = Shader.PropertyToID("_HitPos");
    private int hitTimeID = Shader.PropertyToID("_HitTime");

    void Start()
    {
        Camera.main.depthTextureMode = DepthTextureMode.Depth;
        if (shieldVisual)
        {
            Renderer rend = shieldVisual.GetComponent<Renderer>();
            if (rend)
            {
                // Создаем инстанс материала, чтобы менять его только у этого танка
                shieldMat = rend.material;
                hitTimeID = Shader.PropertyToID("_HitTime");
            }
            // Убеждаемся, что детектор включен, если щит активен
            shieldVisual.SetActive(hasShield);
        }
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

    public void OnShieldHit(Vector3 worldContactPoint)
    {
        if (!hasShield || shieldMat == null) return;

        shieldMat.SetVector(hitPosID, new Vector4(worldContactPoint.x, worldContactPoint.y, worldContactPoint.z, 1));
        shieldMat.SetFloat(hitTimeID, Time.time);

    }

    // ������ ��������� ����� � ������ ����
    // ���������� TRUE, ���� ���� ������, � FALSE, ���� ��� �������� ���
    public bool TryTakeDamage()
    {
        if (hasShield)
        {
            Debug.Log("Щит сработал! Поглощаю урон.");
            //OnShieldHit();
            hasShield = false; // Выключаем щит
            UpdateShieldVisual();
            return false; // Урон не прошел
        }
        Debug.Log("Щита нет! Игрок получает урон.");
        return true; // Урон прошел
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