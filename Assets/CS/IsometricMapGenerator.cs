using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Biome
{
    public string biomeName;
    public Color biomeColor = Color.white;
}

public class IsometricMapGenerator : MonoBehaviour
{
    [Header("맵 설정")]
    public int mapWidth = 100;
    public int mapHeight = 100;
    [Range(0.001f, 0.1f)] public float noiseScale = 0.03f;
    public float islandFactor = 2f;

    [Header("Seed 설정 (0 = 랜덤 시드)")]
    public int seed = 0;

    [Header("타일 프리팹")]
    public GameObject tilePrefab;

    [Header("스무딩 필터 반복 횟수")]
    public int smoothingIterations = 3;

    [Header("경계 흔들림 설정")]
    public float boundaryNoiseScale = 0.1f;
    public float boundaryOffsetAmount = 0.5f;

    [Header("바이옴 리스트")]
    public List<Biome> biomes = new List<Biome>()
    {
        new Biome() { biomeName = "Forest", biomeColor = new Color(0.2f, 0.6f, 0.2f) },
        new Biome() { biomeName = "Desert", biomeColor = new Color(1f, 0.9f, 0.4f) },
        new Biome() { biomeName = "Snow", biomeColor = Color.white },
        new Biome() { biomeName = "Lava", biomeColor = new Color(0.6f, 0.1f, 0.1f) }
    };

    [Header("바다 색상")]
    public Color deepWaterColor = new Color(0f, 0.1f, 0.4f);
    public Color normalWaterColor = new Color(0.2f, 0.4f, 0.8f);
    public Color shallowWaterColor = new Color(0.4f, 0.7f, 1f);

    private float offsetX;
    private float offsetY;

    private int[,] biomeMap;
    private bool[,] isLand;

    void Start()
    {
        InitializeSeed();
        GenerateMap();
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
        {
            SmoothBiomeMap();
        }

        CreateTiles();
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
                        {
                            isBoundary = true;
                        }
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

    void CreateTiles()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                Vector3 isoPos = new Vector3((x - y) * 0.5f, (x + y) * 0.25f, 0);
                GameObject tile = Instantiate(tilePrefab, isoPos, Quaternion.identity, transform);
                SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();

                int biomeIndex = biomeMap[x, y];

                if (biomeIndex == -3)
                {
                    sr.color = deepWaterColor;
                }
                else if (biomeIndex == 0)
                {
                    sr.color = normalWaterColor;
                }
                else if (biomeIndex == -2)
                {
                    sr.color = shallowWaterColor;
                }
                else if (biomeIndex > 0 && biomeIndex <= biomes.Count)
                {
                    sr.color = biomes[biomeIndex - 1].biomeColor;
                }
                else
                {
                    sr.color = Color.magenta;
                }
            }
        }
    }
}