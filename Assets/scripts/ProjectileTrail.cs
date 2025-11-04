// ➡️ Файл: ProjectileTrail.cs
using UnityEngine;

public class ProjectileTrail : MonoBehaviour
{
    private Vector3 startPos;
    private Vector3 endPos;
    private float speed;
    private float length;
    private float startTime;
    private float distance;

    private LineRenderer lr;

    public void Initialize(Vector3 start, Vector3 end, float travelSpeed, float trailLength, float width, Color color, Material material, float lifetime)
    {
        startPos = start;
        endPos = end;
        speed = travelSpeed;
        length = trailLength;

        distance = Vector3.Distance(startPos, endPos);
        startTime = Time.time;

        // Создаем LineRenderer
        lr = gameObject.AddComponent<LineRenderer>();
        lr.sharedMaterial = material;
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = width;
        lr.endWidth = width * 0.5f;
        lr.positionCount = 2;

        // Уничтожаем объект после окончания полета/времени жизни
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (lr == null) return;

        // 1. Вычисляем текущее положение головы трассера
        float distanceCovered = (Time.time - startTime) * speed;
        float fractionOfJourney = distanceCovered / distance;

        // Текущее положение, где находится "голова" трассера
        Vector3 currentHead = Vector3.Lerp(startPos, endPos, fractionOfJourney);

        // 2. Вычисляем положение хвоста трассера
        // Откатываемся назад от головы на фиксированную длину
        Vector3 direction = (endPos - startPos).normalized;
        Vector3 currentTail = currentHead - direction * length;

        // 3. Обновляем позиции LineRenderer
        lr.SetPosition(0, currentTail);
        lr.SetPosition(1, currentHead);

        // 4. Проверяем завершение полета (попадание в цель)
        if (fractionOfJourney >= 1.0f)
        {
            // Здесь можно добавить эффект взрыва в точке endPos
            Destroy(gameObject);
        }

        // 5. Опционально: постепенное затухание цвета (если нужно)
        // float alpha = 1.0f - fractionOfJourney;
        // lr.startColor = new Color(lr.startColor.r, lr.startColor.g, lr.startColor.b, alpha);
    }
}