using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using Random = System.Random; // Используем System.Random для детерминированной генерации (по сиду чанка)
using TMPro;
using UnityEngine.UI;
using System.Collections;


// Новые структуры для блочных данных
public enum BlockType { Air, Wall, Barrel }

/// <summary>
/// Хранит данные и ссылки на объекты для одного чанка.
/// Это помогает при перестройке и разрушении.
/// </summary>
public class MapChunkData
{
    public BlockType[,] data;
    public List<GameObject> spawnedObjects; // Для врагов, бочек и т.п.

    public MapChunkData(int size)
    {
        data = new BlockType[size, size];
        spawnedObjects = new List<GameObject>();
    }
}

/// <summary>
/// Генератор карты, использующий процедурную генерацию по чанкам,
/// динамическую загрузку/выгрузку и поддержку разрушаемости.
/// </summary>
public class MapGenerator : MonoBehaviour
{
    // --- ССЫЛКИ ---
    public Transform player;
    private PlayerController playerControllerInstance;
    public Resultui Resultui;

    [Header("Простые объекты карты")]
    public Material wallMaterial;
    public GameObject barrelPrefab;

    // --- НАСТРОЙКИ ВРАГОВ (КОНТРОЛЬ) ---
    [Header("Враги")]
    public GameObject enemyPrefab;
    [Header("Настройки сложности")]
    [Range(0f, 2f)] public float difficulty = 1f; // 0 = легко, 1 = нормально, 2 = сложно
    private const string DIFFICULTY_PREF_KEY = "Difficulty";

    [Header("Контроль спавна врагов")]
    public int baseMaxEnemiesInScene = 50;       // базовое значение при сложности = 1
    public int baseEnemiesPerSpawn = 2;          // базовое значение при сложности = 1
    public float enemySpawnInterval = 10f;       // интервал между волнами спавна

    private int maxEnemiesInScene;
    private int enemiesPerSpawn;
    private float lastEnemySpawnTime = -999f;
    public List<GameObject> allEnemies = new();

    [Header("Базовые параметры спавна врагов по сложности")]
    public float baseEnemySpawnChance = 1f;  // базовый шанс появления врагов
    public int baseMinEnemiesIfSpawned = 1;  // мин. кол-во врагов при спавне
    public int baseMaxEnemiesIfSpawned = 3;  // макс. кол-во врагов при спавне

    // Дистанции спавна
    public float minEnemySpawnDistance = 60f; // чуть дальше зоны видимости
    public float maxEnemySpawnDistance = 120f; // максимум — дальше этого не нужно

    // Проверка коллизий
    public float enemySpawnCheckRadius = 1.5f;


    // --- НАСТРОЙКИ ГЕНЕРАЦИИ ---
    [Header("Настройки Генерации")]
    public int chunkSize = 16;
    public float blockSize = 3f;
    public int renderDistance = 3;
    public int viewDistance = 1;

    [Header("Настройки Глобального Сида")]
    public bool useRandomSeed = true;
    public int fixedSeed = 12345;
    private int globalSeed;

    public Transform mapParent;
    public Slider difficultySlider;

    [Header("Пол")]
    public GameObject singleFloorPrefab;
    private Transform centralFloor;

    [Header("Инверсия нормалей (Для меша)")]
    public bool invertForward = false;
    public bool invertBack = false;
    public bool invertRight = false;
    public bool invertLeft = false;
    public bool invertUp = false;

    // --- СОСТОЯНИЕ КАРТЫ ---
    private Dictionary<Vector2Int, GameObject> loadedChunks = new();
    private Dictionary<Vector2Int, MapChunkData> chunkDataContainer = new();
    private Vector2Int currentChunk;
    public Vector3 defaultpos;

    [Header("New Features")]
    public GameObject bossPrefab; // Тот же танк, но с галочкой isBoss = true и размером побольше
    public GameObject shieldPickupPrefab; // Сфера с коллайдером (Trigger) и скриптом PickupItem
    public float bossSpawnChance = 0.05f; // 5% шанс спавна босса вместо обычного
    public float itemSpawnChance = 0.05f; // Шанс спавна предмета в пустой клетке

    // UV-смещение (если используется атлас текстур)
    private readonly Vector2 WALL_UV_OFFSET = new Vector2(0, 0);

    // ====================== UNITY LIFECYCLE ======================

    void Start()
    {
        ApplyDifficultySettings();
        if (useRandomSeed)
        {
            globalSeed = System.DateTime.Now.GetHashCode();
        }
        else
        {
            globalSeed = fixedSeed;
        }
        UpdatePlayerReference();
        SetupCentralFloor();
        if (player != null)
        {
            playerControllerInstance = player.GetComponent<PlayerController>();
        }
        
        currentChunk = GetChunkCoord(player != null ? player.position : Vector3.zero);
        MovePlayerToFreeCellNearCenter();
        defaultpos = player.transform.position;
        UpdateChunks();
        if (difficultySlider != null)
        {
            difficultySlider.value = difficulty; // установить текущую сложность
            difficultySlider.onValueChanged.AddListener(OnDifficultySliderChanged);
        }
        OnDifficultySliderChanged(difficulty);

    }
    public void OnDifficultySliderChanged(float value)
    {
        difficulty = value;
        PlayerPrefs.SetFloat(DIFFICULTY_PREF_KEY, difficulty);
        PlayerPrefs.Save();
        ApplyDifficultySettings();
    }

    void Update()
    {
        UpdatePlayerReference();
        if (player == null) return;

        UpdateCentralFloorPosition(player.position);

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
            GameObject taggedPlayer = GameObject.FindWithTag("Player");
            if (taggedPlayer != null)
                player = taggedPlayer.transform;
        }
    }

    // ====================== УПРАВЛЕНИЕ ПОЛОМ ======================

    void SetupCentralFloor()
    {
        if (singleFloorPrefab != null)
        {
            GameObject floorGO = Instantiate(singleFloorPrefab, Vector3.zero, Quaternion.identity, mapParent);
            floorGO.name = "Central_Floor";
            centralFloor = floorGO.transform;

            int floorSizeInBlocks = (2 * renderDistance + 2) * chunkSize;
            float scaleFactor = floorSizeInBlocks * blockSize;
            centralFloor.localScale = new Vector3(scaleFactor, 1f, scaleFactor);
        }
    }

    void UpdateCentralFloorPosition(Vector3 playerPosition)
    {
        if (centralFloor == null) return;

        float halfRenderWorld = (renderDistance + 0.5f) * chunkSize * blockSize;

        // Выравниваем позицию пола по сетке чанков
        Vector3 newPos = new Vector3(
            Mathf.Round(playerPosition.x / (chunkSize * blockSize)) * chunkSize * blockSize,
            -0.1f, // Слегка ниже стен
            Mathf.Round(playerPosition.z / (chunkSize * blockSize)) * chunkSize * blockSize
        );
        centralFloor.position = newPos;
    }

    // ====================== УПРАВЛЕНИЕ ЧАНКАМИ ======================

    void UpdateChunks()
    {
        HashSet<Vector2Int> needed = new();

        for (int dx = -renderDistance; dx <= renderDistance; dx++)
        {
            for (int dz = -renderDistance; dz <= renderDistance; dz++)
            {
                Vector2Int coord = currentChunk + new Vector2Int(dx, dz);
                needed.Add(coord);

                // Если чанк не загружен, генерируем
                if (!loadedChunks.ContainsKey(coord))
                {
                    GameObject chunk = GenerateChunk(coord);
                    loadedChunks.Add(coord, chunk);
                }

                // Включаем/выключаем рендер
                bool visible = (Mathf.Abs(dx) <= viewDistance) && (Mathf.Abs(dz) <= viewDistance);
                SetChunkRender(loadedChunks[coord], visible);
            }
        }

        // Удаляем чанки, которые вышли за renderDistance
        // Удаляем чанки, которые вышли за renderDistance
        List<Vector2Int> toRemove = new();
        foreach (var kvp in loadedChunks)
        {
            if (!needed.Contains(kvp.Key))
            {
                if (chunkDataContainer.TryGetValue(kvp.Key, out MapChunkData chunkData))
                {
                    for (int i = chunkData.spawnedObjects.Count - 1; i >= 0; i--)
                    {
                        GameObject obj = chunkData.spawnedObjects[i];
                        if (obj != null)
                        {
                            if (obj.CompareTag("Enemy"))
                                allEnemies.Remove(obj);

                            Destroy(obj);
                        }
                    }
                }

                Destroy(kvp.Value);
                toRemove.Add(kvp.Key);
                chunkDataContainer.Remove(kvp.Key);
            }
        }
        foreach (var key in toRemove)
            loadedChunks.Remove(key);
    }

    void SetChunkRender(GameObject chunk, bool visible)
    {
        if (chunk == null) return;
        chunk.SetActive(visible);
    }

    /// <summary>
    /// Генерирует новый чанк: данные, меш и спавн объектов.
    /// </summary>
    GameObject GenerateChunk(Vector2Int chunkCoord)
    {
        GameObject chunkGO = new GameObject($"Chunk_{chunkCoord.x}_{chunkCoord.y}");
        chunkGO.transform.parent = mapParent;
        Vector3 chunkOrigin = new Vector3(chunkCoord.x * chunkSize * blockSize, 0, chunkCoord.y * chunkSize * blockSize);
        chunkGO.transform.position = chunkOrigin;

        int seed = globalSeed + chunkCoord.x * 1000 + chunkCoord.y;
        Random chunkRnd = new Random(seed);

        MapChunkData chunkData = new MapChunkData(chunkSize);

        // 1. Создание данных (стены, бочки)
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                Vector3 pos = new Vector3(x * blockSize, 0, z * blockSize);

                if (chunkRnd.NextDouble() < 0.3)
                {
                    chunkData.data[x, z] = BlockType.Wall;
                }
                else if (chunkRnd.NextDouble() < 0.03 && barrelPrefab != null)
                {
                    chunkData.data[x, z] = BlockType.Barrel;
                    Quaternion rot = Quaternion.Euler(0, chunkRnd.Next(0, 4) * 90f, 0);
                    // Спавним бочки в центре блока
                    Vector3 worldPos = chunkOrigin + pos + new Vector3(blockSize / 2f, 0, blockSize / 2f);
                    GameObject barrel = Instantiate(barrelPrefab, worldPos, rot, chunkGO.transform);
                    barrel.tag = "Barrel";
                    chunkData.spawnedObjects.Add(barrel);
                }
                else
                {
                    chunkData.data[x, z] = BlockType.Air;

                    // Попытка спавна предмета
                    if (shieldPickupPrefab != null && chunkRnd.NextDouble() < itemSpawnChance)
                    {
                        Vector3 itemPos = chunkOrigin + pos + new Vector3(blockSize / 2f, 0.5f, blockSize / 2f);
                        GameObject item = Instantiate(shieldPickupPrefab, itemPos, Quaternion.identity, chunkGO.transform);
                        chunkData.spawnedObjects.Add(item);
                    }
                }
            }
        }

        // ГАРАНТИРУЕМ СВОБОДНОЕ МЕСТО ДЛЯ СТАРТА ИГРОКА (ЧАНК 0,0)
        if (chunkCoord.x == 0 && chunkCoord.y == 0)
        {
            int centerX = chunkSize / 2;
            int centerZ = chunkSize / 2;

            // Очистка 5x5 зоны в центре от стен и бочек
            for (int x = centerX - 2; x <= centerX + 2; x++)
            {
                for (int z = centerZ - 2; z <= centerZ + 2; z++)
                {
                    if (x >= 0 && x < chunkSize && z >= 0 && z < chunkSize)
                    {
                        chunkData.data[x, z] = BlockType.Air;
                    }
                }
            }

            // Удаляем физически спавненные бочки в этой зоне.
            for (int i = chunkData.spawnedObjects.Count - 1; i >= 0; i--)
            {
                GameObject obj = chunkData.spawnedObjects[i];
                Vector3 localPos = obj.transform.localPosition;
                int blockX = Mathf.FloorToInt(localPos.x / blockSize);
                int blockZ = Mathf.FloorToInt(localPos.z / blockSize);

                if (blockX >= centerX - 2 && blockX <= centerX + 2 &&
                    blockZ >= centerZ - 2 && blockZ <= centerZ + 2)
                {
                    DestroyImmediate(obj);
                    chunkData.spawnedObjects.RemoveAt(i);
                }
            }
            SpawnEnemiesInChunk(chunkCoord);

        }

        chunkDataContainer.Add(chunkCoord, chunkData);

        // 2. Построение Mesh (стен)
        Mesh mesh = BuildMesh(chunkCoord, chunkData.data);

        GameObject combinedStaticGO = new GameObject("CombinedBlocks");
        combinedStaticGO.transform.parent = chunkGO.transform;
        combinedStaticGO.transform.localPosition = Vector3.zero;

        combinedStaticGO.AddComponent<MeshFilter>().mesh = mesh;
        combinedStaticGO.AddComponent<MeshRenderer>().sharedMaterial = wallMaterial;
        combinedStaticGO.AddComponent<MeshCollider>().sharedMesh = mesh;
        combinedStaticGO.tag = "Wall";

        SpawnEnemiesInChunk(chunkCoord);

        return chunkGO;
    }
    IEnumerator SpawnEnemiesDelayed(Vector2Int chunkCoord)
    {
        yield return new WaitForSeconds(2f);

        // Если игрок ещё не активен
        if (player == null) yield break;

        // Проверка времени спавна
        if (Time.time - lastEnemySpawnTime < enemySpawnInterval)
            yield break;

        // Проверка лимита
        allEnemies.RemoveAll(e => e == null);
        if (allEnemies.Count >= maxEnemiesInScene)
            yield break;

        // Получаем данные чанка
        if (!chunkDataContainer.TryGetValue(chunkCoord, out MapChunkData chunkData)) yield break;

        Vector3 chunkOrigin = new Vector3(chunkCoord.x * chunkSize * blockSize, 0, chunkCoord.y * chunkSize * blockSize);
        int enemiesToSpawn = Mathf.Min(enemiesPerSpawn, maxEnemiesInScene - allEnemies.Count);

        HashSet<Vector3> usedPositions = new();

        // Создаем генератор случайных чисел ОДИН раз перед циклом
        System.Random rng = new System.Random();

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            // ИСПРАВЛЕНИЕ 1: Передаем созданный rng
            Vector3 spawnPos = GetRandomSpawnPos(chunkOrigin, chunkData.data, usedPositions, rng);

            if (spawnPos == Vector3.negativeInfinity) continue;

            GameObject prefabToSpawn = enemyPrefab;

            // ИСПРАВЛЕНИЕ 2: Теперь rng существует и этот код сработает
            if (bossPrefab != null && rng.NextDouble() < bossSpawnChance)
            {
                prefabToSpawn = bossPrefab;
            }

            // ИСПРАВЛЕНИЕ 3: Заменили randomPos на spawnPos
            GameObject enemy = Instantiate(prefabToSpawn, spawnPos + Vector3.up * 0.5f, Quaternion.identity, mapParent);

            enemy.tag = "Enemy";

            // Привязываем врага к чанку
            chunkData.spawnedObjects.Add(enemy);
            allEnemies.Add(enemy);

            // Передаём скрипты
            var ai = enemy.GetComponent<AIEnemyTank>();
            if (ai != null)
            {
                ai.player = player;
                ai.mapGenerator = this;
                ai.playerController = playerControllerInstance;
                ai.resultUI = Resultui;

                // Если это босс, включаем флаг (если вы добавили isBoss в AIEnemyTank)
                if (prefabToSpawn == bossPrefab)
                {
                    ai.isBoss = true;
                    ai.maxHealth = 5; // Пример HP для босса
                }
            }

            usedPositions.Add(spawnPos);
        }

        lastEnemySpawnTime = Time.time;
    }

    void SpawnEnemiesInChunk(Vector2Int chunkCoord)
    {
        StartCoroutine(SpawnEnemiesDelayed(chunkCoord));
    }


    void SpawnEnemy(Transform chunkParent, Vector3 chunkOrigin, Vector2Int chunkCoord, BlockType[,] data, List<GameObject> spawnedObjects)
    {
        if (enemyPrefab == null || player == null)
            return;

        Vector3 randomPos = GetRandomSpawnPos(chunkOrigin, data, new HashSet<Vector3>(), new System.Random(globalSeed + chunkCoord.x * 1000 + chunkCoord.y));

        if (randomPos == Vector3.negativeInfinity)
            return;

        GameObject enemy = Instantiate(enemyPrefab, randomPos + Vector3.up * 0.5f, Quaternion.identity, chunkParent);
        enemy.tag = "Enemy"; // обязательно ставим тег

        spawnedObjects.Add(enemy);
        allEnemies.Add(enemy);

        // Передаём ссылки скрипту AIEnemyTank
        AIEnemyTank ai = enemy.GetComponent<AIEnemyTank>();
        if (ai != null)
        {
            ai.player = player;
            ai.mapGenerator = this;
            ai.playerController = playerControllerInstance;
            ai.resultUI = Resultui;
        }
    }


    // ====================== ФУНКЦИИ MESH (СТЕНЫ) ======================

    Mesh BuildMesh(Vector2Int chunkCoord, BlockType[,] data)
    {
        List<Vector3> vertices = new();
        List<int> triangles = new();
        List<Vector2> uvs = new();

        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                if (data[x, z] == BlockType.Wall)
                {
                    AddFace(x, z, Vector3.up, vertices, triangles, uvs);
                    if (!IsWallAt(chunkCoord, x, z + 1)) AddFace(x, z, Vector3.forward, vertices, triangles, uvs);
                    if (!IsWallAt(chunkCoord, x, z - 1)) AddFace(x, z, Vector3.back, vertices, triangles, uvs);
                    if (!IsWallAt(chunkCoord, x + 1, z)) AddFace(x, z, Vector3.right, vertices, triangles, uvs);
                    if (!IsWallAt(chunkCoord, x - 1, z)) AddFace(x, z, Vector3.left, vertices, triangles, uvs);
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();

        return mesh;
    }

    bool IsWallAt(Vector2Int chunkCoord, int x, int z)
    {
        if (x >= 0 && x < chunkSize && z >= 0 && z < chunkSize)
        {
            if (chunkDataContainer.TryGetValue(chunkCoord, out MapChunkData chunkData))
            {
                return chunkData.data[x, z] == BlockType.Wall;
            }
            return false;
        }

        Vector2Int neighborCoord = chunkCoord;
        int localX = x;
        int localZ = z;

        if (x < 0) { neighborCoord.x--; localX += chunkSize; }
        else if (x >= chunkSize) { neighborCoord.x++; localX -= chunkSize; }

        if (z < 0) { neighborCoord.y--; localZ += chunkSize; }
        else if (z >= chunkSize) { neighborCoord.y++; localZ -= chunkSize; }

        if (chunkDataContainer.TryGetValue(neighborCoord, out MapChunkData neighborChunk))
        {
            if (localX >= 0 && localX < chunkSize && localZ >= 0 && localZ < chunkSize)
            {
                return neighborChunk.data[localX, localZ] == BlockType.Wall;
            }
        }
        return false;
    }

    BlockType GetBlockType(Vector2Int currentChunkCoord, int x, int z)
    {
        if (x >= 0 && x < chunkSize && z >= 0 && z < chunkSize)
        {
            if (chunkDataContainer.TryGetValue(currentChunkCoord, out MapChunkData currentChunkData))
            {
                return currentChunkData.data[x, z];
            }
            return BlockType.Air;
        }

        Vector2Int neighborCoord = currentChunkCoord;
        int localX = x;
        int localZ = z;

        if (x < 0) { neighborCoord.x--; localX += chunkSize; }
        else if (x >= chunkSize) { neighborCoord.x++; localX -= chunkSize; }

        if (z < 0) { neighborCoord.y--; localZ += chunkSize; }
        else if (z >= chunkSize) { neighborCoord.y++; localZ -= chunkSize; }

        if (chunkDataContainer.TryGetValue(neighborCoord, out MapChunkData neighborChunkData))
        {
            if (localX >= 0 && localX < chunkSize && localZ >= 0 && localZ < chunkSize)
            {
                return neighborChunkData.data[localX, localZ];
            }
        }
        return BlockType.Wall;
    }

    void AddFace(int x, int z, Vector3 direction, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
    {
        int triIndex = vertices.Count;
        float s = blockSize;

        if (direction == Vector3.forward)
        {
            vertices.Add(new Vector3(x * s, 0, (z + 1) * s));
            vertices.Add(new Vector3((x + 1) * s, 0, (z + 1) * s));
            vertices.Add(new Vector3((x + 1) * s, s, (z + 1) * s));
            vertices.Add(new Vector3(x * s, s, (z + 1) * s));
        }
        else if (direction == Vector3.back)
        {
            vertices.Add(new Vector3((x + 1) * s, 0, z * s));
            vertices.Add(new Vector3(x * s, 0, z * s));
            vertices.Add(new Vector3(x * s, s, z * s));
            vertices.Add(new Vector3((x + 1) * s, s, z * s));
        }
        else if (direction == Vector3.right)
        {
            vertices.Add(new Vector3((x + 1) * s, 0, z * s));
            vertices.Add(new Vector3((x + 1) * s, 0, (z + 1) * s));
            vertices.Add(new Vector3((x + 1) * s, s, (z + 1) * s));
            vertices.Add(new Vector3((x + 1) * s, s, z * s));
        }
        else if (direction == Vector3.left)
        {
            vertices.Add(new Vector3(x * s, 0, (z + 1) * s));
            vertices.Add(new Vector3(x * s, 0, z * s));
            vertices.Add(new Vector3(x * s, s, z * s));
            vertices.Add(new Vector3(x * s, s, (z + 1) * s));
        }
        else if (direction == Vector3.up)
        {
            vertices.Add(new Vector3(x * s, s, z * s));
            vertices.Add(new Vector3((x + 1) * s, s, z * s));
            vertices.Add(new Vector3((x + 1) * s, s, (z + 1) * s));
            vertices.Add(new Vector3(x * s, s, (z + 1) * s));
        }
        else return;

        bool invert =
            (direction == Vector3.forward && invertForward) ||
            (direction == Vector3.back && invertBack) ||
            (direction == Vector3.right && invertRight) ||
            (direction == Vector3.left && invertLeft) ||
            (direction == Vector3.up && invertUp);

        if (!invert)
        {
            triangles.Add(triIndex + 0);
            triangles.Add(triIndex + 2);
            triangles.Add(triIndex + 1);
            triangles.Add(triIndex + 0);
            triangles.Add(triIndex + 3);
            triangles.Add(triIndex + 2);
        }
        else
        {
            triangles.Add(triIndex + 1);
            triangles.Add(triIndex + 2);
            triangles.Add(triIndex + 0);
            triangles.Add(triIndex + 2);
            triangles.Add(triIndex + 3);
            triangles.Add(triIndex + 0);
        }

        uvs.Add(WALL_UV_OFFSET + new Vector2(0, 0));
        uvs.Add(WALL_UV_OFFSET + new Vector2(1, 0));
        uvs.Add(WALL_UV_OFFSET + new Vector2(1, 1));
        uvs.Add(WALL_UV_OFFSET + new Vector2(0, 1));
    }


    // ====================== ФУНКЦИИ РАЗРУШЕНИЯ ======================

    public void RemoveBlock(Vector3 worldPosition)
    {
        Vector2Int chunkCoord = GetChunkCoord(worldPosition);

        if (chunkDataContainer.TryGetValue(chunkCoord, out MapChunkData chunkData))
        {
            Vector3 chunkOrigin = new Vector3(chunkCoord.x * chunkSize * blockSize, 0, chunkCoord.y * chunkSize * blockSize);
            Vector3 relativePos = worldPosition - chunkOrigin;

            // Конвертация относительных координат в блочные индексы
            int localX = Mathf.FloorToInt(relativePos.x / blockSize);
            int localZ = Mathf.FloorToInt(relativePos.z / blockSize);

            if (localX >= 0 && localX < chunkSize && localZ >= 0 && localZ < chunkSize)
            {
                if (chunkData.data[localX, localZ] != BlockType.Wall) return;

                chunkData.data[localX, localZ] = BlockType.Air;

                RebuildChunk(chunkCoord);
                RebuildNeighbors(chunkCoord, localX, localZ);
            }
        }
    }

    void RebuildChunk(Vector2Int coord)
    {
        if (loadedChunks.TryGetValue(coord, out GameObject chunkGO) &&
            chunkDataContainer.TryGetValue(coord, out MapChunkData chunkData))
        {
            Transform combinedStaticGO = chunkGO.transform.Find("CombinedBlocks");

            if (combinedStaticGO == null) return;

            MeshFilter mf = combinedStaticGO.GetComponent<MeshFilter>();
            MeshCollider mc = combinedStaticGO.GetComponent<MeshCollider>();

            if (mf != null && mc != null)
            {
                Mesh newMesh = BuildMesh(coord, chunkData.data);

                if (mf.mesh != null) Destroy(mf.mesh);

                mf.mesh = newMesh;
                mc.sharedMesh = null;
                mc.sharedMesh = newMesh;
            }
        }
    }

    void RebuildNeighbors(Vector2Int centerCoord, int localX, int localZ)
    {
        if (localX == 0) RebuildChunk(centerCoord + Vector2Int.left);
        if (localX == chunkSize - 1) RebuildChunk(centerCoord + Vector2Int.right);
        if (localZ == 0) RebuildChunk(centerCoord + Vector2Int.down);
        if (localZ == chunkSize - 1) RebuildChunk(centerCoord + Vector2Int.up);
    }

    // ====================== ФУНКЦИИ СПАВНА ======================

    void SpawnEnemies(Transform chunkParent, Vector3 chunkOrigin, Vector2Int chunkCoord, BlockType[,] data, List<GameObject> spawnedObjects)
    {
        if (enemyPrefab == null || player == null)
            return;

        // Чистим список, чтобы удалить уничтоженных врагов
        allEnemies.RemoveAll(e => e == null);

        // Проверяем лимит врагов
        if (allEnemies.Count >= maxEnemiesInScene)
            return;

        // Генератор случайных чисел по сидy чанка
        System.Random rnd = new System.Random(globalSeed + chunkCoord.x * 1000 + chunkCoord.y);

        // Определяем количество врагов за спавн
        int enemiesToSpawn = Mathf.Min(enemiesPerSpawn, maxEnemiesInScene - allEnemies.Count);

        HashSet<Vector3> usedPositions = new();

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            Vector3 pos = GetRandomSpawnPos(chunkOrigin, data, usedPositions, rnd);
            if (float.IsNegativeInfinity(pos.x)) continue;

            GameObject enemy = Instantiate(enemyPrefab, pos + Vector3.up * 0.5f, Quaternion.identity, chunkParent);
            spawnedObjects.Add(enemy);
            allEnemies.Add(enemy);
            usedPositions.Add(pos);

            // Настройка AI
            var ai = enemy.GetComponent<AIEnemyTank>();
            if (ai != null)
            {
                ai.player = player;
                ai.mapGenerator = this;
                ai.playerController = playerControllerInstance;
                ai.resultUI = Resultui;
            }
        }
    }


    private void ApplyDifficultySettings()
    {
        difficulty = PlayerPrefs.GetFloat(DIFFICULTY_PREF_KEY, 1f);

        maxEnemiesInScene = Mathf.RoundToInt(baseMaxEnemiesInScene * Mathf.Lerp(0.5f, 2f, difficulty / 2f));
        enemiesPerSpawn = Mathf.RoundToInt(baseEnemiesPerSpawn * Mathf.Lerp(0.5f, 2f, difficulty / 2f));

        Debug.Log($"[Difficulty] Загружено: {difficulty:F2}, Макс врагов = {maxEnemiesInScene}, За спавн = {enemiesPerSpawn}");
    }



    Vector3 GetRandomSpawnPos(Vector3 chunkOrigin, BlockType[,] data, HashSet<Vector3> used, Random rng)
    {
        Vector3 spawnPos = Vector3.negativeInfinity;
        int attempts = 0;
        int maxAttempts = chunkSize * chunkSize * 2;
        float halfBlock = blockSize / 2f;

        do
        {
            int sx = rng.Next(0, chunkSize);
            int sz = rng.Next(0, chunkSize);

            // 1. Проверяем, что ячейка пустая
            if (data[sx, sz] != BlockType.Air) continue;

            // Вычисляем позицию в центре блока (по XZ)
            spawnPos = chunkOrigin + new Vector3(sx * blockSize + halfBlock, 0, sz * blockSize + halfBlock);

            // 2. Проверка коллизий
            Collider[] colliders = Physics.OverlapSphere(spawnPos + Vector3.up * 0.5f, enemySpawnCheckRadius);
            bool isClear = true;
            foreach (var col in colliders)
            {
                // Исключаем триггеры, пол и стены. Иначе считаем занятым.
                if (!col.isTrigger && col.gameObject.tag != "Floor" && col.gameObject.tag != "Wall")
                {
                    isClear = false;
                    break;
                }
            }

            if (isClear && !used.Contains(spawnPos))
            {
                return spawnPos;
            }

            attempts++;
        }
        while (attempts < maxAttempts);

        return Vector3.negativeInfinity; // Не удалось найти место
    }

    Vector2Int GetChunkCoord(Vector3 position)
    {
        int cx = Mathf.FloorToInt(position.x / (chunkSize * blockSize));
        int cz = Mathf.FloorToInt(position.z / (chunkSize * blockSize));
        return new Vector2Int(cx, cz);
    }


    public Vector3 WorldToBlockPosition(Vector3 worldPosition)
    {
        Vector2Int chunkCoord = GetChunkCoord(worldPosition);
        Vector3 chunkOrigin = new Vector3(chunkCoord.x * chunkSize * blockSize, 0, chunkCoord.y * chunkSize * blockSize);
        Vector3 relativePos = worldPosition - chunkOrigin;

        int localX = Mathf.FloorToInt(relativePos.x / blockSize);
        int localZ = Mathf.FloorToInt(relativePos.z / blockSize);

        return chunkOrigin + new Vector3(localX * blockSize, 0, localZ * blockSize);
    }
    public void MovePlayerToFreeCellNearCenter()
    {
        Vector2Int centerChunk = new Vector2Int(0, 0);

        if (!chunkDataContainer.TryGetValue(centerChunk, out MapChunkData chunkData))
            return;

        int centerX = chunkSize / 2;
        int centerZ = chunkSize / 2;
        float halfBlock = blockSize / 2f;
        Vector3 chunkOrigin = new Vector3(centerChunk.x * chunkSize * blockSize, 0, centerChunk.y * chunkSize * blockSize);

        // Радиус поиска свободных клеток
        int radius = Mathf.Min(chunkSize / 2, 8);

        for (int r = 0; r <= radius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    int x = centerX + dx;
                    int z = centerZ + dz;
                    if (x < 0 || z < 0 || x >= chunkSize || z >= chunkSize)
                        continue;

                    if (chunkData.data[x, z] == BlockType.Air)
                    {
                        Vector3 worldPos = chunkOrigin + new Vector3(x * blockSize + halfBlock, 0, z * blockSize + halfBlock);
                        player.position = worldPos + Vector3.up * 0.5f; // немного приподнять
                        return;
                    }
                }
            }
        }

        Debug.LogWarning("❗Не найдено свободного места для игрока в центральном чанке!");
    }
    
    public void DestroyAllEnemies()
    {
        foreach (var enemy in allEnemies)
        {
            if (enemy != null)
                Destroy(enemy);
        }
        allEnemies.Clear();

        // Также очищаем врагов в чанках
        foreach (var chunk in chunkDataContainer.Values)
        {
            for (int i = chunk.spawnedObjects.Count - 1; i >= 0; i--)
            {
                var obj = chunk.spawnedObjects[i];
                if (obj != null && obj.CompareTag("Enemy"))
                {
                    Destroy(obj);
                    chunk.spawnedObjects.RemoveAt(i);
                }
            }
        }

        Debug.Log(" Все враги удалены");
    }

}