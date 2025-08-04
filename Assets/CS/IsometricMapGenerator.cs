using UnityEngine;

public class IsometricMapGenerator : MonoBehaviour
{
    public int mapWidth = 100;
    public int mapHeight = 100;
    public float noiseScale = 0.03f;
    public float islandFactor = 2f;

    [Header("Seed 설정 (0 = 랜덤 시드)")]
    public int seed = 0;

    public GameObject tilePrefab;

    public Color waterColor = Color.blue;
    public Color forestColor = new Color(0.2f, 0.6f, 0.2f);
    public Color desertColor = new Color(1f, 0.9f, 0.4f);
    public Color snowColor = Color.white;
    public Color lavaColor = new Color(0.6f, 0.1f, 0.1f);

    private float offsetX;
    private float offsetY;

    void Start()
    {
        InitializeSeed();
        GenerateMap();
    }

    void InitializeSeed()
    {
        if (seed == 0)
        {
            seed = System.DateTime.Now.GetHashCode(); // 랜덤 시드
        }

        System.Random prng = new System.Random(seed);
        offsetX = prng.Next(-100000, 100000);
        offsetY = prng.Next(-100000, 100000);
    }

    void GenerateMap()
    {
        Vector2 center = new Vector2(mapWidth / 2f, mapHeight / 2f);

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                // 중심으로부터 거리 계산 (대륙형 유도)
                float dx = (x - center.x) / mapWidth * 2f;
                float dy = (y - center.y) / mapHeight * 2f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float islandMask = Mathf.Clamp01(1 - distance * islandFactor);

                // 시드 기반 노이즈 + 대륙 마스크
                float noiseValue = Mathf.PerlinNoise(
                    (x + offsetX) * noiseScale,
                    (y + offsetY) * noiseScale
                );
                float finalValue = noiseValue * islandMask;

                Color tileColor = GetBiomeColor(finalValue);

                // 아이소메트릭 좌표로 배치
                Vector3 isoPos = new Vector3((x - y) * 0.5f, (x + y) * 0.25f, 0);
                GameObject tile = Instantiate(tilePrefab, isoPos, Quaternion.identity, transform);
                tile.GetComponent<SpriteRenderer>().color = tileColor;
            }
        }
    }

    Color GetBiomeColor(float value)
    {
        if (value < 0.2f) return waterColor;
        else if (value < 0.4f) return forestColor;
        else if (value < 0.6f) return desertColor;
        else if (value < 0.8f) return snowColor;
        else return lavaColor;
    }
}