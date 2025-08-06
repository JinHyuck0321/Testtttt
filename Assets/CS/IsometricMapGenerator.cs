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

    [Header("바이옴 리스트")]
    public List<Biome> biomes = new List<Biome>()
    {
        new Biome() { biomeName = "Forest", biomeColor = new Color(0.2f, 0.6f, 0.2f) },
        new Biome() { biomeName = "Desert", biomeColor = new Color(1f, 0.9f, 0.4f) },
        new Biome() { biomeName = "Snow", biomeColor = Color.white },
        new Biome() { biomeName = "Lava", biomeColor = new Color(0.6f, 0.1f, 0.1f) }
    };

    [Header("물 색상")]
    public Color waterColor = new Color(0.2f, 0.4f, 0.8f); // 얕은 바다
    public Color deepWaterColor = new Color(0f, 0.1f, 0.4f); // 깊은 바다

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

        // 바다/땅 구분
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

                float jitterSeed = Mathf.Sin((x + 1) * 12.9898f + (y + 1) * 78.233f) * 43758.5453f;
                float jitter = (jitterSeed - Mathf.Floor(jitterSeed)) * 0.15f - 0.075f;

                float finalValue = Mathf.Clamp01(mixedNoise * islandMask + jitter);

                if (finalValue < 0.1f)
                {
                    biomeMap[x, y] = -2; // 깊은 바다
                    isLand[x, y] = false;
                }
                else if (finalValue < 0.3f)
                {
                    biomeMap[x, y] = 0; // 얕은 바다
                    isLand[x, y] = false;
                }
                else
                {
                    biomeMap[x, y] = -1; // 미할당 땅
                    isLand[x, y] = true;
                }
            }
        }

        RegionGrowBiomes();
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

                biomeMap[seedPos.x, seedPos.y] = i + 1; // 바이옴 인덱스는 1부터
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
                    biomeMap[x, y] = 1; // 기본 바이옴
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

                if (biomeIndex == -2)
                {
                    sr.color = deepWaterColor;
                }
                else if (biomeIndex == 0)
                {
                    sr.color = waterColor;
                }
                else if (biomeIndex > 0 && biomeIndex <= biomes.Count)
                {
                    sr.color = biomes[biomeIndex - 1].biomeColor;
                }
                else
                {
                    sr.color = Color.magenta; // 에러 디버깅용
                }
            }
        }
    }
}