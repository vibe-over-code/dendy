using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ProjectileTrail : MonoBehaviour
{
    [Header("Trail Settings")]
    public Color trailColor = Color.yellow;
    public float trailWidth = 0.2f;
    public float trailLifetime = 0.5f;

    private ParticleSystem trailPS;
    private float spawnTime;
    private Vector3 targetPoint;
    private bool moving = false;
    private float speed = 100f; // скорость движения трассера

    void Start()
    {
        spawnTime = Time.time;
        CreateTrail();
    }

    void CreateTrail()
    {
        trailPS = gameObject.AddComponent<ParticleSystem>();

        var main = trailPS.main;
        main.startColor = trailColor;
        main.startLifetime = trailLifetime;
        main.startSize = trailWidth;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 500;

        var emission = trailPS.emission;
        emission.rateOverTime = 0;

        var trails = trailPS.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.Ribbon;
        trails.ratio = 1f;
        trails.lifetime = trailLifetime;
        trails.dieWithParticles = true;

        var shape = trailPS.shape;
        shape.enabled = false;

        var renderer = trailPS.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.trailMaterial = renderer.material;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.sortingOrder = 1;

        trailPS.Play();
    }

    void Update()
    {
        if (trailPS == null) return;

        // Эмитим частицу на позиции снаряда
        trailPS.Emit(1);

        // Движение трассера, если активировано
        if (moving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPoint, speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, targetPoint) < 0.1f)
            {
                moving = false;
                Destroy(gameObject, trailLifetime);
            }
        }
    }

    private void OnDestroy()
    {
        if (trailPS != null)
        {
            trailPS.transform.parent = null;
            Destroy(trailPS.gameObject, trailLifetime);
        }
    }

    // -----------------------------
    // Метод для создания трассера
    // -----------------------------
    public static void DrawTrail(Vector3 startPoint, Vector3 endPoint,
                                 float trailWidth = 0.2f,
                                 Color? trailColor = null,
                                 float trailLifetime = 0.5f)
    {
        Color color = trailColor ?? Color.yellow;

        GameObject trailGO = new GameObject("ProjectileTracer");
        trailGO.transform.position = startPoint;

        ProjectileTrail trail = trailGO.AddComponent<ProjectileTrail>();
        trail.trailColor = color;
        trail.trailWidth = trailWidth;
        trail.trailLifetime = trailLifetime;

        trail.targetPoint = endPoint;
        trail.moving = true;

        // Автоудаление
        Object.Destroy(trailGO, trailLifetime + 1f);
    }
}
