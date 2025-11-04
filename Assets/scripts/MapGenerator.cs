using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

// Используем System.Random для детерминированной генерации (по сиду чанка)
using Random = System.Random;

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
    public Transform player;

    [Header("Простые объекты карты")]
    public Material wallMaterial;
    public GameObject barrelPrefab;

    [Header("Враги")]
    public GameObject enemyPrefab;
    public int minEnemiesPerChunk = 1;
    public int maxEnemiesPerChunk = 3;
    // Радиус проверки свободного места для спавна (чтобы враги не застряли сразу)
    public float enemySpawnCheckRadius = 1.5f;

    [Header("Настройки Генерации")]
    public int chunkSize = 16;
    public float blockSize = 3f;
    public int renderDistance = 3;
    public int viewDistance = 1;

    public Transform mapParent;

    [Header("ОПТИМИЗАЦИЯ: Общий Пол")]
    public GameObject singleFloorPrefab;
    private Transform centralFloor;

    [Header("Инверсия нормалей (для отладки)")]
    public bool invertForward = false;
    public bool invertBack = false;
    public bool invertRight = false;
    public bool invertLeft = false;
    public bool invertUp = false;


    private Dictionary<Vector2Int, GameObject> loadedChunks = new();
    // Контейнер для хранения всех данных чанков (даже если они не отрисовываются)
    private Dictionary<Vector2Int, MapChunkData> chunkDataContainer = new();
    private Vector2Int currentChunk;

    // UV-смещение (если используется атлас текстур)
    private readonly Vector2 WALL_UV_OFFSET = new Vector2(0, 0);

    void Start()
    {
        UpdatePlayerReference();
        SetupCentralFloor();
        // Определяем, в каком чанке находится игрок при старте
        currentChunk = GetChunkCoord(player != null ? player.position : Vector3.zero);
        UpdateChunks();
    }

    void Update()
    {
        UpdatePlayerReference();
        if (player == null) return;

        UpdateCentralFloorPosition(player.position);

        Vector2Int newChunk = GetChunkCoord(player.position);
        // Обновляем чанки при смене чанка или нажатии 'G' (для отладки)
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
            // Поиск игрока по CharacterController или тегу "Player"
            var playerObj = FindObjectOfType<UnityEngine.CharacterController>();
            if (playerObj != null)
                player = playerObj.transform;
            if (player == null)
            {
                GameObject taggedPlayer = GameObject.FindWithTag("Player");
                if (taggedPlayer != null)
                    player = taggedPlayer.transform;
            }
        }
    }

    void SetupCentralFloor()
    {
        if (singleFloorPrefab != null)
        {
            GameObject floorGO = Instantiate(singleFloorPrefab, Vector3.zero, Quaternion.identity, mapParent);
            floorGO.name = "Central_Floor";
            centralFloor = floorGO.transform;

            int floorSizeInBlocks = (2 * renderDistance + 2) * chunkSize;
            float scaleFactor = floorSizeInBlocks * blockSize;
            // Установка правильного размера пола, чтобы покрыть всю область рендеринга
            centralFloor.localScale = new Vector3(scaleFactor, 1f, scaleFactor);
        }
    }

    void UpdateCentralFloorPosition(Vector3 playerPosition)
    {
        if (centralFloor == null) return;

        float halfChunkSizeWorld = chunkSize * blockSize;

        // Вычисляем позицию, выровненную по сетке чанков
        Vector3 newPos = new Vector3(
            (float)Math.Floor(playerPosition.x / halfChunkSizeWorld) * halfChunkSizeWorld + halfChunkSizeWorld / 2f,
            -0.1f, // Слегка ниже стен
            (float)Math.Floor(playerPosition.z / halfChunkSizeWorld) * halfChunkSizeWorld + halfChunkSizeWorld / 2f
        );
        centralFloor.position = newPos;
    }

    /// <summary>
    /// Динамически загружает/выгружает чанки вокруг игрока.
    /// </summary>
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

                // Включаем/выключаем рендер (Chunk LOD)
                bool visible = (Mathf.Abs(dx) <= viewDistance) && (Mathf.Abs(dz) <= viewDistance);
                SetChunkRender(loadedChunks[coord], visible);
            }
        }

        // Удаляем чанки, которые вышли за renderDistance
        List<Vector2Int> toRemove = new();
        foreach (var kvp in loadedChunks)
        {
            if (!needed.Contains(kvp.Key))
            {
                Destroy(kvp.Value);
                toRemove.Add(kvp.Key);
                chunkDataContainer.Remove(kvp.Key); // Удаляем данные чанка
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

        // Детерминированный Random для повторяемости генерации чанка
        int seed = chunkCoord.x * 1000 + chunkCoord.y;
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
                    GameObject barrel = Instantiate(barrelPrefab, chunkOrigin + pos, rot, chunkGO.transform);
                    barrel.tag = "Barrel";
                    chunkData.spawnedObjects.Add(barrel);
                }
                else
                {
                    chunkData.data[x, z] = BlockType.Air;
                }
            }
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

        // 3. Спавн врагов
        SpawnEnemies(chunkGO.transform, chunkOrigin, chunkData.data, chunkData.spawnedObjects);

        return chunkGO;
    }

    // ====================== ФУНКЦИИ BLOCK/MESH ======================

    /// <summary>
    /// Строит комбинированный меш для всех стен чанка, используя Face Culling.
    /// </summary>
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
                    // Верхняя грань всегда есть
                    AddFace(x, z, Vector3.up, vertices, triangles, uvs);

                    // Добавляем грань только если рядом НЕТ стены
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

    /// <summary>
    /// Проверяет, есть ли именно блок СТЕНЫ.
    /// Всё остальное (включая бочки, пол, воздух) не считается препятствием.
    /// </summary>
    bool IsWallAt(Vector2Int chunkCoord, int x, int z)
    {
        // 1️⃣ Проверяем внутри текущего чанка
        if (x >= 0 && x < chunkSize && z >= 0 && z < chunkSize)
        {
            if (chunkDataContainer.TryGetValue(chunkCoord, out MapChunkData chunkData))
            {
                return chunkData.data[x, z] == BlockType.Wall;
            }
            return false;
        }

        // 2️⃣ Проверяем соседний чанк, если координаты вышли за границы
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

        // 3️⃣ Если соседний чанк не загружен — считаем, что стены там нет
        return false;
    }

    /// <summary>
    /// Получает тип блока в указанных локальных координатах, обрабатывая границы чанков.
    /// </summary>
    BlockType GetBlockType(Vector2Int currentChunkCoord, int x, int z)
    {
        // 1. Проверка внутри текущего чанка
        if (x >= 0 && x < chunkSize && z >= 0 && z < chunkSize)
        {
            if (chunkDataContainer.TryGetValue(currentChunkCoord, out MapChunkData currentChunkData))
            {
                return currentChunkData.data[x, z];
            }
            return BlockType.Air;
        }

        // 2. Проверка соседнего чанка
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

        // 3. Если соседний чанк не загружен — считаем стеной, чтобы не было дыр
        return BlockType.Wall;
    }

    /// <summary>
    /// Добавляет 4 вершины и 2 треугольника для создания одной грани куба.
    /// </summary>
    void AddFace(int x, int z, Vector3 direction, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
    {
        int triIndex = vertices.Count;
        float s = blockSize;

        // Вершины для каждой стороны
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

        // Определяем, нужно ли инвертировать
        bool invert =
            (direction == Vector3.forward && invertForward) ||
            (direction == Vector3.back && invertBack) ||
            (direction == Vector3.right && invertRight) ||
            (direction == Vector3.left && invertLeft) ||
            (direction == Vector3.up && invertUp);

        // Добавляем треугольники
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

        // UV
        uvs.Add(WALL_UV_OFFSET + new Vector2(0, 0));
        uvs.Add(WALL_UV_OFFSET + new Vector2(1, 0));
        uvs.Add(WALL_UV_OFFSET + new Vector2(1, 1));
        uvs.Add(WALL_UV_OFFSET + new Vector2(0, 1));
    }



    // Удаляет блок по мировым координатам и перестраивает затронутые чанки.
    public void RemoveBlock(Vector3 worldPosition)
    {
        Vector2Int chunkCoord = GetChunkCoord(worldPosition);

        if (chunkDataContainer.TryGetValue(chunkCoord, out MapChunkData chunkData))
        {
            // 1. Вычисляем начало чанка (origin)
            // ИСПОЛЬЗУЕМ: new Vector3(chunkCoord.x * chunkSize * blockSize, 0, chunkCoord.y * chunkSize * blockSize)
            // ВМЕСТО: loadedChunks[chunkCoord].transform.position, так как transform может быть не инициализирован или неточен.
            Vector3 chunkOrigin = new Vector3(chunkCoord.x * chunkSize * blockSize, 0, chunkCoord.y * chunkSize * blockSize);
            Vector3 relativePos = worldPosition - chunkOrigin;

            // 2. Конвертация относительных координат в блочные индексы
            int localX = Mathf.FloorToInt(relativePos.x / blockSize);
            int localZ = Mathf.FloorToInt(relativePos.z / blockSize);

            if (localX >= 0 && localX < chunkSize && localZ >= 0 && localZ < chunkSize)
            {
                BlockType currentType = chunkData.data[localX, localZ];

                // 💥 ВАЖНОЕ ИЗМЕНЕНИЕ: УДАЛЯЕМ ТОЛЬКО СТЕНЫ.
                // Бочки теперь удаляются напрямую в PlayerController.
                if (currentType != BlockType.Wall) return;

                // Удаляем блок
                chunkData.data[localX, localZ] = BlockType.Air;

                // Перестраиваем текущий и соседние чанки
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
            // ❗ ИЩЕМ КОМПОНЕНТЫ В ДОЧЕРНЕМ ОБЪЕКТЕ "CombinedBlocks"
            Transform combinedStaticGO = chunkGO.transform.Find("CombinedBlocks");
            
            if (combinedStaticGO == null) return; // Если не нашли, то и нечего перестраивать.

            MeshFilter mf = combinedStaticGO.GetComponent<MeshFilter>();
            MeshCollider mc = combinedStaticGO.GetComponent<MeshCollider>();

            if (mf != null && mc != null)
            {
                // Убеждаемся, что GetBlockType работает с актуальными данными
                Mesh newMesh = BuildMesh(coord, chunkData.data);

                // Очистка старого меша для предотвращения утечек памяти
                if (mf.mesh != null) Destroy(mf.mesh);

                mf.mesh = newMesh;
                // В Unity для MeshCollider нужно сначала присвоить null, 
                // а потом новый меш, чтобы он гарантированно обновился.
                mc.sharedMesh = null; 
                mc.sharedMesh = newMesh;
            }
        }
    }

    void RebuildNeighbors(Vector2Int centerCoord, int localX, int localZ)
    {
        // Перестраиваем соседей, только если удаленный блок был на границе,
        // чтобы открыть грань в соседнем чанке.
        if (localX == 0) RebuildChunk(centerCoord + Vector2Int.left);
        if (localX == chunkSize - 1) RebuildChunk(centerCoord + Vector2Int.right);
        if (localZ == 0) RebuildChunk(centerCoord + Vector2Int.down);
        if (localZ == chunkSize - 1) RebuildChunk(centerCoord + Vector2Int.up);
    }

    // ====================== ФУНКЦИИ СПАВНА ======================

    void SpawnEnemies(Transform chunkParent, Vector3 chunkOrigin, BlockType[,] data, List<GameObject> spawnedObjects)
    {
        // Используем детерминированный сид для врагов, чтобы они всегда появлялись в одних и тех же местах
        int seed = chunkParent.position.GetHashCode();
        Random enemyRnd = new Random(seed);

        int enemiesToSpawn = enemyRnd.Next(minEnemiesPerChunk, maxEnemiesPerChunk + 1);
        HashSet<Vector3> usedPositions = new();

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            if (enemyPrefab != null)
            {
                Vector3 pos = GetRandomSpawnPos(chunkOrigin, data, usedPositions, enemyRnd);

                // Если позиция найдена
                if (pos.x != float.NegativeInfinity)
                {
                    GameObject enemy = Instantiate(enemyPrefab, pos, Quaternion.identity, chunkParent);
                    // Важно: поднимаем врага, чтобы он не провалился в пол
                    enemy.transform.position += Vector3.up * 0.5f;

                    var ai = enemy.GetComponent<AIEnemyTank>();
                    if (ai != null) ai.player = player;
                    usedPositions.Add(pos);
                    spawnedObjects.Add(enemy);
                }
            }
        }
    }

    Vector3 GetRandomSpawnPos(Vector3 chunkOrigin, BlockType[,] data, HashSet<Vector3> used, Random rng)
    {
        Vector3 spawnPos = Vector3.negativeInfinity;
        int attempts = 0;
        int maxAttempts = chunkSize * chunkSize * 2;

        do
        {
            int sx = rng.Next(0, chunkSize);
            int sz = rng.Next(0, chunkSize);

            // 1. Проверяем, что ячейка пустая
            if (data[sx, sz] != BlockType.Air) continue;

            spawnPos = chunkOrigin + new Vector3(sx * blockSize, 0, sz * blockSize);

            // 2. Проверяем, что место свободно от других объектов (врагов, стен и т.д.)
            // Используем центр сферы чуть выше пола
            Collider[] colliders = Physics.OverlapSphere(spawnPos + Vector3.up * 0.5f, enemySpawnCheckRadius);
            bool isClear = true;
            foreach (var col in colliders)
            {
                // Исключаем пол (если у него есть коллайдер)
                if (!col.isTrigger && col.gameObject.name != "Central_Floor" && col.gameObject.tag != "Floor")
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

    // Вспомогательный метод (оставлен из оригинального кода, но не используется в GenerateChunk)
    void SpawnEnemyFormation(Vector3 center, int count, Transform parent, HashSet<Vector3> used)
    {
        float spacing = 2f;
        Vector3[] offsets;
        if (count == 3)
        {
            offsets = new Vector3[] { Vector3.zero, new Vector3(spacing, 0, 0), new Vector3(spacing / 2f, 0, spacing) };
        }
        else
        {
            offsets = new Vector3[] { Vector3.zero, new Vector3(spacing, 0, 0), new Vector3(0, 0, spacing), new Vector3(spacing, 0, spacing) };
        }

        foreach (var off in offsets)
        {
            Vector3 pos = center + off;
            GameObject enemy = Instantiate(enemyPrefab, pos, Quaternion.identity, parent);
            var ai = enemy.GetComponent<AIEnemyTank>();
            if (ai != null) ai.player = player;
            used.Add(pos);
        }
    }

    // В классе MapGenerator
    public Vector3 WorldToBlockPosition(Vector3 worldPosition)
    {
        // Находим координаты чанка (как в GetChunkCoord)
        Vector2Int chunkCoord = GetChunkCoord(worldPosition);

        // Вычисляем начало чанка (origin) (как в RemoveBlock)
        Vector3 chunkOrigin = new Vector3(chunkCoord.x * chunkSize * blockSize, 0, chunkCoord.y * chunkSize * blockSize);
        Vector3 relativePos = worldPosition - chunkOrigin;

        // Конвертация относительных координат в блочные индексы (как в RemoveBlock)
        int localX = Mathf.FloorToInt(relativePos.x / blockSize);
        int localZ = Mathf.FloorToInt(relativePos.z / blockSize);

        // Возвращаем позицию центра блока (или его начала) в мировых координатах
        // В данном случае, возвращаем начало блока, как его обрабатывает RemoveBlock:
        return chunkOrigin + new Vector3(localX * blockSize, 0, localZ * blockSize);
    }

}