using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class Biome
{
    public string biomeName;
    public TileBase biomeTile;
}

public class Chunk
{
    public int chunkX, chunkY;
    public int size;
    public int[,] biomeMapChunk;
    public int[,] heightMapChunk;

    public Tilemap baseTilemap;
    public List<Tilemap> heightTilemaps;

    private TileBase deepWaterTile;
    private TileBase normalWaterTile;
    private TileBase shallowWaterTile;
    private List<Biome> biomes;

    public Chunk(int chunkX, int chunkY, int size, Tilemap baseTilemap, List<Tilemap> heightTilemaps, List<Biome> biomes,
                 TileBase deepWater, TileBase normalWater, TileBase shallowWater)
    {
        this.chunkX = chunkX;
        this.chunkY = chunkY;
        this.size = size;
        this.baseTilemap = baseTilemap;
        this.heightTilemaps = heightTilemaps;
        this.biomes = biomes;
        this.deepWaterTile = deepWater;
        this.normalWaterTile = normalWater;
        this.shallowWaterTile = shallowWater;

        biomeMapChunk = new int[size, size];
        heightMapChunk = new int[size, size];
    }

    public void SetBiomeData(int[,] fullMap)
    {
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                int mapX = chunkX * size + x;
                int mapY = chunkY * size + y;
                if (mapX < fullMap.GetLength(0) && mapY < fullMap.GetLength(1))
                    biomeMapChunk[x, y] = fullMap[mapX, mapY];
                else
                    biomeMapChunk[x, y] = -3;
            }
    }

    public void SetHeightData(int[,] fullHeightMap)
    {
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                int mapX = chunkX * size + x;
                int mapY = chunkY * size + y;
                if (mapX < fullHeightMap.GetLength(0) && mapY < fullHeightMap.GetLength(1))
                    heightMapChunk[x, y] = fullHeightMap[mapX, mapY];
                else
                    heightMapChunk[x, y] = 0;
            }
    }

    public void CreateTiles()
    {
        baseTilemap.ClearAllTiles();
        foreach (var htmap in heightTilemaps)
            htmap.ClearAllTiles();

        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                int biomeIndex = biomeMapChunk[x, y];
                TileBase baseTile = null;

                if (biomeIndex == -3)
                    baseTile = deepWaterTile;
                else if (biomeIndex == 0)
                    baseTile = normalWaterTile;
                else if (biomeIndex == -2)
                    baseTile = shallowWaterTile;
                else if (biomeIndex > 0 && biomeIndex <= biomes.Count)
                    baseTile = biomes[biomeIndex - 1].biomeTile;

                Vector3Int pos = new Vector3Int(x, y, 0);
                baseTilemap.SetTile(pos, baseTile);

                int height = heightMapChunk[x, y];
                if (height >= 1 && height <= heightTilemaps.Count)
                {
                    TileBase heightTile = null;
                    if (biomeIndex > 0 && biomeIndex <= biomes.Count)
                        heightTile = biomes[biomeIndex - 1].biomeTile;

                    heightTilemaps[height - 1].SetTile(pos, heightTile);
                }
            }
    }
}

public class IsometricMapGenerator : MonoBehaviour
{
    [Header("맵 설정")]
    public int mapWidth;
    public int mapHeight;
    public int chunkSize = 16;

    [Range(0.001f, 0.1f)]
    public float noiseScale = 0.03f;
    public float islandFactor = 2f;

    [Header("Seed 설정 (0 = 랜덤 시드)")]
    public int seed = 0;

    [Header("스무딩 필터 반복 횟수")]
    public int smoothingIterations = 3;
    [Header("고도 스무딩 반복 횟수")]
    public int heightSmoothingIterations = 2;

    [Header("경계 흔들림 설정")]
    public float boundaryNoiseScale = 0.1f;
    public float boundaryOffsetAmount = 0.5f;

    [Header("그리드")]
    public Grid grid;

    [Header("바이옴 리스트")]
    public List<Biome> biomes = new List<Biome>()
    {
        new Biome() { biomeName = "Forest", biomeTile = null },
        new Biome() { biomeName = "Desert", biomeTile = null },
        new Biome() { biomeName = "Snow", biomeTile = null },
        new Biome() { biomeName = "Lava", biomeTile = null }
    };

    [Header("바다 타일")]
    public TileBase deepWaterTile;
    public TileBase normalWaterTile;
    public TileBase shallowWaterTile;

    [Header("고도 레이어 설정")]
    [Range(1, 10)]
    public int heightLayerCount = 5;

    [Header("아이소메트릭 타일 높이 간격")]
    public float heightLayerYOffset = 0.25f;

    [Header("고도 기준 (0~1 사이 값, 오름차순)")]
    public float[] heightThresholds = new float[] { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f };


    private float offsetX;
    private float offsetY;

    private int[,] biomeMap;
    private bool[,] isLand;
    private int[,] heightMap;

    private List<Chunk> chunks = new List<Chunk>();

    void Start()
    {
        mapHeight = mapHeight * chunkSize;
        mapWidth = mapWidth * chunkSize;
        InitializeSeed();
        GenerateMap();
        CreateChunks();
        AssignDataToChunks();
        RefreshChunks();
    }

    void InitializeSeed()
    {
        if (seed == 0)
            seed = System.DateTime.Now.GetHashCode();

        System.Random prng = new System.Random(seed);
        offsetX = prng.Next(-100000, 100000);
        offsetY = prng.Next(-100000, 100000);
    }

    void GenerateMap()
    {
        Vector2 center = new Vector2(mapWidth / 2f, mapHeight / 2f);

        biomeMap = new int[mapWidth, mapHeight];
        isLand = new bool[mapWidth, mapHeight];
        heightMap = new int[mapWidth, mapHeight];

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                float dx = (x - center.x) / mapWidth * 2f;
                float dy = (y - center.y) / mapHeight * 2f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float islandMask = Mathf.Clamp01(1 - distance * islandFactor);

                float baseNoise = Mathf.PerlinNoise((x + offsetX) * noiseScale, (y + offsetY) * noiseScale);
                float detailNoise = Mathf.PerlinNoise((x + offsetX + 9999) * noiseScale * 3f, (y + offsetY + 9999) * noiseScale * 3f);
                float mixedNoise = Mathf.Lerp(baseNoise, detailNoise, 0.4f);

                float noiseWeight = islandMask > 0.4f ? 1f : 0.5f;
                float jitterSeed = Mathf.Sin((x + 1) * 12.9898f + (y + 1) * 78.233f) * 43758.5453f;
                float jitter = (jitterSeed - Mathf.Floor(jitterSeed)) * 0.15f - 0.075f;

                float finalValue = Mathf.Clamp01(mixedNoise * islandMask * noiseWeight + jitter);

                // 고도 결정 (배열 기준)
                int heightLevel = 0;
                for (int i = 0; i < heightThresholds.Length; i++)
                {
                    if (finalValue < heightThresholds[i])
                    {
                        heightLevel = i;
                        break;
                    }
                }
                if (finalValue >= heightThresholds[heightThresholds.Length - 1])
                    heightLevel = heightLayerCount - 1;


                heightMap[x, y] = heightLevel;

                if (finalValue < 0.1f)
                {
                    biomeMap[x, y] = -3;
                    isLand[x, y] = false;
                }
                else if (finalValue < 0.28f)
                {
                    biomeMap[x, y] = 0;
                    isLand[x, y] = false;
                }
                else if (finalValue < 0.38f)
                {
                    biomeMap[x, y] = -2;
                    isLand[x, y] = false;
                }
                else
                {
                    biomeMap[x, y] = -1;
                    isLand[x, y] = true;
                }
            }
        }

        RegionGrowBiomes();
        ApplyBoundaryNoise();

        for (int i = 0; i < smoothingIterations; i++)
            SmoothBiomeMap();
        for (int i = 0; i < heightSmoothingIterations; i++)
            SmoothHeightMap();
    }

    void RegionGrowBiomes()
    {
        System.Random rand = new System.Random(seed);
        int seedsPerBiome = 5;

        List<Vector2Int> landPositions = new List<Vector2Int>();
        for (int x = 0; x < mapWidth; x++)
            for (int y = 0; y < mapHeight; y++)
                if (isLand[x, y])
                    landPositions.Add(new Vector2Int(x, y));

        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int i = 0; i < biomes.Count; i++)
        {
            for (int s = 0; s < seedsPerBiome; s++)
            {
                if (landPositions.Count == 0) break;

                int idx = rand.Next(landPositions.Count);
                Vector2Int seedPos = landPositions[idx];
                landPositions.RemoveAt(idx);

                biomeMap[seedPos.x, seedPos.y] = i + 1;
                queue.Enqueue(seedPos);
            }
        }

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            Vector2Int pos = queue.Dequeue();
            int currentBiome = biomeMap[pos.x, pos.y];

            for (int dir = 0; dir < 4; dir++)
            {
                int nx = pos.x + dx[dir];
                int ny = pos.y + dy[dir];

                if (nx < 0 || nx >= mapWidth || ny < 0 || ny >= mapHeight) continue;
                if (!isLand[nx, ny]) continue;
                if (biomeMap[nx, ny] != -1) continue;

                biomeMap[nx, ny] = currentBiome;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        for (int x = 0; x < mapWidth; x++)
            for (int y = 0; y < mapHeight; y++)
                if (isLand[x, y] && biomeMap[x, y] == -1)
                    biomeMap[x, y] = 1;
    }

    void ApplyBoundaryNoise()
    {
        int[,] newBiomeMap = (int[,])biomeMap.Clone();

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                int currentBiome = biomeMap[x, y];
                if (currentBiome <= 0) continue;

                bool isBoundary = false;
                for (int nx = x - 1; nx <= x + 1 && !isBoundary; nx++)
                {
                    for (int ny = y - 1; ny <= y + 1 && !isBoundary; ny++)
                    {
                        if (nx < 0 || nx >= mapWidth || ny < 0 || ny >= mapHeight) continue;
                        if (biomeMap[nx, ny] != currentBiome && biomeMap[nx, ny] > 0)
                            isBoundary = true;
                    }
                }

                if (isBoundary)
                {
                    float noiseValue = Mathf.PerlinNoise((x + offsetX) * boundaryNoiseScale, (y + offsetY) * boundaryNoiseScale);
                    if (noiseValue > 0.5f + boundaryOffsetAmount * 0.5f)
                    {
                        int randomBiome = Mathf.Clamp(Mathf.RoundToInt(noiseValue * biomes.Count), 1, biomes.Count);
                        if (randomBiome != currentBiome)
                            newBiomeMap[x, y] = randomBiome;
                    }
                }
            }
        }

        biomeMap = newBiomeMap;
    }

    void SmoothBiomeMap()
    {
        int[,] newBiomeMap = new int[mapWidth, mapHeight];

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                Dictionary<int, int> countMap = new Dictionary<int, int>();

                for (int nx = x - 1; nx <= x + 1; nx++)
                {
                    for (int ny = y - 1; ny <= y + 1; ny++)
                    {
                        if (nx < 0 || nx >= mapWidth || ny < 0 || ny >= mapHeight) continue;

                        int biome = biomeMap[nx, ny];
                        if (!countMap.ContainsKey(biome))
                            countMap[biome] = 0;
                        countMap[biome]++;
                    }
                }

                int maxCount = 0;
                int dominantBiome = biomeMap[x, y];
                foreach (var pair in countMap)
                {
                    if (pair.Value > maxCount)
                    {
                        maxCount = pair.Value;
                        dominantBiome = pair.Key;
                    }
                }

                newBiomeMap[x, y] = dominantBiome;
            }
        }

        biomeMap = newBiomeMap;
    }
    void SmoothHeightMap()
    {
        int[,] newHeightMap = new int[mapWidth, mapHeight];

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                // 바다(-3, 0, -2)는 스무딩 제외
                if (biomeMap[x, y] <= 0)
                {
                    newHeightMap[x, y] = heightMap[x, y];
                    continue;
                }

                Dictionary<int, int> countMap = new Dictionary<int, int>();

                for (int nx = x - 1; nx <= x + 1; nx++)
                {
                    for (int ny = y - 1; ny <= y + 1; ny++)
                    {
                        if (nx < 0 || nx >= mapWidth || ny < 0 || ny >= mapHeight) continue;

                        int h = heightMap[nx, ny];
                        if (!countMap.ContainsKey(h))
                            countMap[h] = 0;
                        countMap[h]++;
                    }
                }

                // 최빈값 계산
                int dominantHeight = heightMap[x, y];
                int maxCount = 0;
                foreach (var pair in countMap)
                {
                    if (pair.Value > maxCount)
                    {
                        maxCount = pair.Value;
                        dominantHeight = pair.Key;
                    }
                }

                newHeightMap[x, y] = dominantHeight;
            }
        }

        heightMap = newHeightMap;
    }

    void CreateChunks()
    {
        int chunkCountX = Mathf.CeilToInt(mapWidth / (float)chunkSize);
        int chunkCountY = Mathf.CeilToInt(mapHeight / (float)chunkSize);

        float tileWidth = 1f;
        float tileHeight = 0.5f;

        for (int cx = 0; cx < chunkCountX; cx++)
        {
            for (int cy = 0; cy < chunkCountY; cy++)
            {
                GameObject chunkObj = new GameObject($"Chunk_{cx}_{cy}");

                if (grid != null)
                {
                    chunkObj.transform.parent = grid.transform;
                    float isoX = (cx - cy) * chunkSize * tileWidth / 2f;
                    float isoY = (cx + cy) * chunkSize * tileHeight / 2f;
                    chunkObj.transform.localPosition = new Vector3(isoX, isoY, 0);
                }
                else
                {
                    Debug.LogWarning("Grid가 연결되어 있지 않습니다. 청크가 씬 최상위에 생성됩니다.");
                    chunkObj.transform.parent = this.transform;
                    float isoX = (cx - cy) * chunkSize * tileWidth / 2f;
                    float isoY = (cx + cy) * chunkSize * tileHeight / 2f;
                    chunkObj.transform.localPosition = new Vector3(isoX, isoY, 0);
                }

                List<Tilemap> layerTilemaps = new List<Tilemap>();

                for (int h = 0; h < heightLayerCount; h++)
                {
                    GameObject layerObj = new GameObject($"Layer_{h}");
                    layerObj.transform.parent = chunkObj.transform;

                    Tilemap layerTilemap = layerObj.AddComponent<Tilemap>();
                    TilemapRenderer renderer = layerObj.AddComponent<TilemapRenderer>();
                    renderer.sortOrder = TilemapRenderer.SortOrder.TopRight;
                    renderer.sortingOrder = -(cx + cy);

                    float yOffset = h * heightLayerYOffset;
                    layerObj.transform.localPosition = new Vector3(0, yOffset, 0);

                    layerTilemaps.Add(layerTilemap);
                }

                for (int x = 0; x < chunkSize; x++)
                {
                    for (int y = 0; y < chunkSize; y++)
                    {
                        int mapX = cx * chunkSize + x;
                        int mapY = cy * chunkSize + y;

                        if (mapX < 0 || mapY < 0 || mapX >= mapWidth || mapY >= mapHeight)
                            continue;

                        int heightLevel = heightMap[mapX, mapY];
                        int biomeIndex = biomeMap[mapX, mapY];
                        TileBase tile = null;

                        if (biomeIndex == -3)
                            tile = deepWaterTile;
                        else if (biomeIndex == 0)
                            tile = normalWaterTile;
                        else if (biomeIndex == -2)
                            tile = shallowWaterTile;
                        else if (biomeIndex > 0 && biomeIndex <= biomes.Count)
                            tile = biomes[biomeIndex - 1].biomeTile;

                        // 바다 타일일 경우 고도 강제 0
                        if (biomeIndex <= 0)
                            heightLevel = 0;

                        Vector3Int tilePos = new Vector3Int(x, y, 0);

                        // 0부터 heightLevel까지 모두 채워줌
                        for (int h = 0; h <= heightLevel && h < layerTilemaps.Count; h++)
                        {
                            layerTilemaps[h].SetTile(tilePos, tile);
                        }
                    }
                }
            }
        }
    }




    void AssignDataToChunks()
    {
        foreach (var chunk in chunks)
        {
            chunk.SetBiomeData(biomeMap);
            chunk.SetHeightData(heightMap);
        }
    }

    void RefreshChunks()
    {
        foreach (var chunk in chunks)
        {
            chunk.CreateTiles();
        }
    }
}
