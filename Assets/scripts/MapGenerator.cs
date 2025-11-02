using UnityEngine;
using System.Collections.Generic;

public class MapGeneratorMobile : MonoBehaviour
{
    [Header("Player & Map Settings")]
    public Transform player;
    public Transform mapParent;
    public int chunkSize = 16;
    public float blockSize = 3f;
    public int renderDistance = 3;
    public int viewDistance = 1;

    [Header("Prefabs")]
    public GameObject wallPrefab;
    public GameObject planePrefab;
    public GameObject barrelPrefab;
    public GameObject enemyPrefab;

    [Header("Enemy Settings")]
    public int minEnemiesPerChunk = 1;
    public int maxEnemiesPerChunk = 3;
    public float enemySpawnCheckRadius = 1f;

    private readonly Dictionary<Vector2Int, GameObject> loadedChunks = new();
    private Vector2Int currentChunk;
    private float chunkWorldSize;
    private float checkTimer;

    // --- Пулы объектов ---
    private ObjectPool wallPool, planePool, barrelPool, enemyPool;

    void Start()
    {
        chunkWorldSize = chunkSize * blockSize;

        // Инициализация пулов
        wallPool = new ObjectPool(wallPrefab, 100, mapParent);
        planePool = new ObjectPool(planePrefab, 10, mapParent);
        barrelPool = new ObjectPool(barrelPrefab, 40, mapParent);
        enemyPool = new ObjectPool(enemyPrefab, 50, mapParent);

        FindPlayerOnce();
        currentChunk = GetChunkCoord(player != null ? player.position : Vector3.zero);
        GenerateInitialChunks();
    }

    void Update()
    {
        if (player == null)
        {
            FindPlayerOnce();
            if (player == null) return;
        }

        checkTimer += Time.deltaTime;
        if (checkTimer < 0.25f) return; // проверяем не чаще, чем 4 раза в секунду
        checkTimer = 0f;

        Vector2Int newChunk = GetChunkCoord(player.position);
        if (newChunk != currentChunk)
        {
            currentChunk = newChunk;
            UpdateChunks();
        }
    }

    void FindPlayerOnce()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void GenerateInitialChunks()
    {
        for (int dx = -renderDistance; dx <= renderDistance; dx++)
        {
            for (int dz = -renderDistance; dz <= renderDistance; dz++)
            {
                Vector2Int coord = currentChunk + new Vector2Int(dx, dz);
                GameObject chunk = GenerateChunk(coord);
                loadedChunks.Add(coord, chunk);
                chunk.SetActive(Mathf.Abs(dx) <= viewDistance && Mathf.Abs(dz) <= viewDistance);
            }
        }
    }

    void UpdateChunks()
    {
        // Отключаем все чанки
        foreach (var kvp in loadedChunks)
            kvp.Value.SetActive(false);

        // Включаем нужные и создаём недостающие
        for (int dx = -renderDistance; dx <= renderDistance; dx++)
        {
            for (int dz = -renderDistance; dz <= renderDistance; dz++)
            {
                Vector2Int coord = currentChunk + new Vector2Int(dx, dz);

                if (!loadedChunks.TryGetValue(coord, out GameObject chunk))
                {
                    chunk = GenerateChunk(coord);
                    loadedChunks.Add(coord, chunk);
                }

                bool visible = Mathf.Abs(dx) <= viewDistance && Mathf.Abs(dz) <= viewDistance;
                chunk.SetActive(visible);
            }
        }
    }

    GameObject GenerateChunk(Vector2Int chunkCoord)
    {
        GameObject chunkGO = new($"Chunk_{chunkCoord.x}_{chunkCoord.y}");
        chunkGO.transform.SetParent(mapParent, false);

        Vector3 origin = new(chunkCoord.x * chunkWorldSize, 0, chunkCoord.y * chunkWorldSize);

        // Пол
        GameObject floor = planePool.Get(origin + new Vector3(chunkWorldSize / 2f - blockSize / 2f, -0.1f, chunkWorldSize / 2f - blockSize / 2f));
        floor.transform.SetParent(chunkGO.transform);

        // Стены
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                if (Random.value < 0.3f)
                {
                    Vector3 pos = origin + new Vector3(x * blockSize, 0, z * blockSize);
                    GameObject wall = wallPool.Get(pos);
                    wall.transform.SetParent(chunkGO.transform);
                }
            }
        }

        // Бочки
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                if (Random.value < 0.04f)
                {
                    Vector3 pos = origin + new Vector3(x * blockSize, 0, z * blockSize);
                    GameObject barrel = barrelPool.Get(pos);
                    barrel.transform.SetParent(chunkGO.transform);
                }
            }
        }

        // Враги
        int enemiesToSpawn = Random.Range(minEnemiesPerChunk, maxEnemiesPerChunk + 1);
        for (int i = 0; i < enemiesToSpawn; i++)
        {
            int sx = Random.Range(0, chunkSize);
            int sz = Random.Range(0, chunkSize);
            Vector3 pos = origin + new Vector3(sx * blockSize, 0, sz * blockSize);

            GameObject enemy = enemyPool.Get(pos);
            if (enemy.TryGetComponent(out AIEnemyTank ai))
                ai.player = player;

            enemy.transform.SetParent(chunkGO.transform);
        }

        return chunkGO;
    }

    Vector2Int GetChunkCoord(Vector3 pos)
    {
        return new(
            Mathf.FloorToInt(pos.x / chunkWorldSize),
            Mathf.FloorToInt(pos.z / chunkWorldSize)
        );
    }
}

/// <summary>
/// Простой пул объектов, безопасный для WebGL / мобильных устройств
/// </summary>
public class ObjectPool
{
    private readonly GameObject prefab;
    private readonly Transform parent;
    private readonly Queue<GameObject> pool;

    public ObjectPool(GameObject prefab, int initialCount, Transform parent)
    {
        this.prefab = prefab;
        this.parent = parent;
        pool = new Queue<GameObject>(initialCount);

        for (int i = 0; i < initialCount; i++)
        {
            GameObject obj = Object.Instantiate(prefab, parent);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    public GameObject Get(Vector3 position)
    {
        GameObject obj = pool.Count > 0 ? pool.Dequeue() : Object.Instantiate(prefab, parent);
        obj.transform.position = position;
        obj.SetActive(true);
        return obj;
    }

    public void Return(GameObject obj)
    {
        obj.SetActive(false);
        pool.Enqueue(obj);
    }
}
