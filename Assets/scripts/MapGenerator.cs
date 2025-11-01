using UnityEngine;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    public Transform player;

    [Header("Простые объекты карты")]
    public GameObject wallPrefab;
    public GameObject planePrefab;

    [Header("Новые объекты")]
    public GameObject barrelPrefab;
    public GameObject rampPrefab;

    [Header("Враги")]
    public GameObject enemyPrefab;
    public int minEnemiesPerChunk = 1;
    public int maxEnemiesPerChunk = 3;
    public float enemySpawnCheckRadius = 1f;

    public int chunkSize = 16;
    public float blockSize = 3f;
    public int renderDistance = 3;
    public int viewDistance = 1;

    public Transform mapParent;

    private Dictionary<Vector2Int, GameObject> loadedChunks = new();
    private Vector2Int currentChunk;

    void Start()
    {
        UpdatePlayerReference();
        currentChunk = GetChunkCoord(player != null ? player.position : Vector3.zero);
        UpdateChunks();
    }

    void Update()
    {
        UpdatePlayerReference();
        if (player == null) return;

        Vector2Int newChunk = GetChunkCoord(player.position);
        if (newChunk != currentChunk || Input.GetKeyDown(KeyCode.G))
        {
            currentChunk = newChunk;
            UpdateChunks();
        }
    }

    void UpdatePlayerReference()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
    }

    void UpdateChunks()
    {
        HashSet<Vector2Int> needed = new();

        for (int dx = -renderDistance; dx <= renderDistance; dx++)
        {
            for (int dz = -renderDistance; dz <= renderDistance; dz++)
            {
                Vector2Int coord = currentChunk + new Vector2Int(dx, dz);
                needed.Add(coord);

                if (!loadedChunks.ContainsKey(coord))
                {
                    GameObject chunk = GenerateChunk(coord);
                    loadedChunks.Add(coord, chunk);
                }

                bool visible = (Mathf.Abs(dx) <= viewDistance) && (Mathf.Abs(dz) <= viewDistance);
                SetChunkRender(loadedChunks[coord], visible);
            }
        }

        List<Vector2Int> toRemove = new();
        foreach (var kvp in loadedChunks)
        {
            if (!needed.Contains(kvp.Key))
            {
                Destroy(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove)
            loadedChunks.Remove(key);
    }

    void SetChunkRender(GameObject chunk, bool visible)
    {
        if (chunk == null) return;
        MeshRenderer[] renderers = chunk.GetComponentsInChildren<MeshRenderer>();
        foreach (var r in renderers)
        {
            r.enabled = visible;
        }
    }

    GameObject GenerateChunk(Vector2Int chunkCoord)
    {
        GameObject chunkGO = new GameObject($"Chunk_{chunkCoord.x}_{chunkCoord.y}");
        chunkGO.transform.parent = mapParent;

        Vector3 chunkOrigin = new Vector3(chunkCoord.x * chunkSize * blockSize, 0, chunkCoord.y * chunkSize * blockSize);

        // Пол
        if (planePrefab != null)
        {
            Vector3 floorPos = chunkOrigin + new Vector3((chunkSize * blockSize) / 2f - blockSize / 2f, -0.1f, (chunkSize * blockSize) / 2f - blockSize / 2f);
            GameObject floor = Instantiate(planePrefab, floorPos, Quaternion.identity, chunkGO.transform);
            floor.name = "ChunkFloor";
        }

        // Стены, бочки, рампы
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                Vector3 pos = chunkOrigin + new Vector3(x * blockSize, 0, z * blockSize);

                // Стена
                if (Random.value < 0.3f)
                {
                    if (wallPrefab != null)
                    {
                        GameObject wall = Instantiate(wallPrefab, pos, Quaternion.identity, chunkGO.transform);
                        wall.tag = "Wall";
                    }
                }

                // Бочка
                if (Random.value < 0.1f && barrelPrefab != null)
                {
                    Quaternion rot = Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0);
                    Instantiate(barrelPrefab, pos, rot, chunkGO.transform);
                }

                // Рампа
                if (Random.value < 0.05f && rampPrefab != null)
                {
                    Quaternion rot = Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0);
                    Instantiate(rampPrefab, pos, rot, chunkGO.transform);
                }
            }
        }

        // Спавн врагов (формирования)
        int enemiesToSpawn = Random.Range(minEnemiesPerChunk, maxEnemiesPerChunk + 1);
        HashSet<Vector3> usedPositions = new();

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            // Формируем треугольник из 3-4 танков
            if (enemyPrefab != null && i == 0 && enemiesToSpawn >= 3)
            {
                Vector3 centerPos = GetRandomSpawnPos(chunkOrigin, usedPositions);
                SpawnEnemyFormation(centerPos, 3, chunkGO.transform, usedPositions);
                i += 2; // пропускаем 2, т.к. треугольник уже добавлен
            }
            else
            {
                Vector3 pos = GetRandomSpawnPos(chunkOrigin, usedPositions);
                GameObject enemy = Instantiate(enemyPrefab, pos, Quaternion.identity, chunkGO.transform);
                var ai = enemy.GetComponent<AIEnemyTank>();
                if (ai != null) ai.player = player;
                usedPositions.Add(pos);
            }
        }

        return chunkGO;
    }

    Vector3 GetRandomSpawnPos(Vector3 chunkOrigin, HashSet<Vector3> usedPositions)
    {
        Vector3 spawnPos;
        int attempts = 0;
        do
        {
            int sx = Random.Range(0, chunkSize);
            int sz = Random.Range(0, chunkSize);
            spawnPos = chunkOrigin + new Vector3(sx * blockSize, 0, sz * blockSize);
            attempts++;
        }
        while ((Physics.CheckSphere(spawnPos + Vector3.up, enemySpawnCheckRadius) || usedPositions.Contains(spawnPos)) && attempts < 20);

        return spawnPos;
    }

    void SpawnEnemyFormation(Vector3 center, int count, Transform parent, HashSet<Vector3> usedPositions)
    {
        // Расставляем танки треугольником
        float spacing = 2f;
        Vector3[] offsets;
        if (count == 3)
        {
            offsets = new Vector3[]
            {
                Vector3.zero,
                new Vector3(spacing, 0, 0),
                new Vector3(spacing/2f, 0, spacing)
            };
        }
        else // 4 танка
        {
            offsets = new Vector3[]
            {
                Vector3.zero,
                new Vector3(spacing, 0, 0),
                new Vector3(0, 0, spacing),
                new Vector3(spacing, 0, spacing)
            };
        }

        foreach (var off in offsets)
        {
            Vector3 pos = center + off;
            GameObject enemy = Instantiate(enemyPrefab, pos, Quaternion.identity, parent);
            var ai = enemy.GetComponent<AIEnemyTank>();
            if (ai != null) ai.player = player;
            usedPositions.Add(pos);
        }
    }

    Vector2Int GetChunkCoord(Vector3 position)
    {
        int cx = Mathf.FloorToInt(position.x / (chunkSize * blockSize));
        int cz = Mathf.FloorToInt(position.z / (chunkSize * blockSize));
        return new Vector2Int(cx, cz);
    }
}
