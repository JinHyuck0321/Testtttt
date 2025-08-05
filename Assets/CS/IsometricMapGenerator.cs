using UnityEngine;

public class IsometricMapGenerator : MonoBehaviour
{
    [Header("맵 설정")]
    public int mapWidth = 100;
    public int mapHeight = 100;
    [Range(0.001f, 0.1f)] public float noiseScale = 0.03f;
    public float islandFactor = 2f; // 외곽을 바다로 만드는 강도

    [Header("Seed 설정 (0 = 랜덤 시드)")]
    public int seed = 0;

    [Header("타일 프리팹")]
    public GameObject tilePrefab;

    [Header("바이옴 색상")]
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
            seed = System.DateTime.Now.GetHashCode();

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
                // 1. 대륙 마스크 (중심일수록 1, 외곽일수록 0)
                float dx = (x - center.x) / mapWidth * 2f;
                float dy = (y - center.y) / mapHeight * 2f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float islandMask = Mathf.Clamp01(1 - distance * islandFactor);

                // 2. 복합 노이즈 생성
                float baseNoise = Mathf.PerlinNoise((x + offsetX) * noiseScale, (y + offsetY) * noiseScale);
                float detailNoise = Mathf.PerlinNoise((x + offsetX + 9999) * noiseScale * 3f, (y + offsetY + 9999) * noiseScale * 3f);
                float mixedNoise = Mathf.Lerp(baseNoise, detailNoise, 0.4f);

                // 3. 랜덤 흔들기 추가 (등고선 방지)
                float jitterSeed = Mathf.Sin((x + 1) * 12.9898f + (y + 1) * 78.233f) * 43758.5453f;
                float jitter = (jitterSeed - Mathf.Floor(jitterSeed)) * 0.15f - 0.075f;

                // 4. 최종 값 계산
                float finalValue = Mathf.Clamp01(mixedNoise * islandMask + jitter);

                // 5. 바이옴 색상 결정
                Color tileColor = GetBiomeColor(finalValue);

                // 6. 아이소메트릭 위치 계산
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
