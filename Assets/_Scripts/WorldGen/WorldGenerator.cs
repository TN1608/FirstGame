using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using _Scripts.WorldGen;
using ProceduralWorld.Data;

namespace ProceduralWorld.Generation
{
    /// <summary>
    /// WorldGenerator — Quản lý việc sinh thế giới theo kiến trúc hybrid.
    /// Địa hình cốt lõi sử dụng Mesh 3D, trong khi các đối tượng trang trí vẫn sử dụng Tilemap.
    /// </summary>
    [SelectionBase]
    public class WorldGenerator : MonoBehaviour
    {
        [Header("=== CẤU HÌNH CHÍNH ===")]
        public WorldGenerationConfig config;
        public Transform objectsParent;

        [Header("=== HỆ THỐNG TILEMAP (HYBRID) ===")]
        [Tooltip("Lớp Tilemap dùng để đặt các đối tượng trang trí nhỏ nếu cần")]
        public Tilemap decorationLayer;

        // Trạng thái nội bộ
        private ChunkDataGenerator chunkGen;
        private Camera mainCam;
        private Dictionary<Vector2Int, ChunkData> loadedChunks = new();
        private Dictionary<Vector2Int, GameObject> meshChunks = new();
        private Queue<Vector2Int> chunkLoadQueue = new();
        private Vector2Int lastPlayerChunk = new Vector2Int(-9999, -9999);

        #region Lifecycle
        void Start()
        {
            mainCam = Camera.main;
            if (config == null)
            {
                Debug.LogError("[WorldGenerator] Chưa gán WorldGenerationConfig!");
                enabled = false;
                return;
            }

            // Khởi tạo Seed
            if (config.randomSeedEachTime)
            {
                config.seed = Random.Range(0, 1000000);
            }

            chunkGen = new ChunkDataGenerator(config);

            if (objectsParent == null)
            {
                objectsParent = new GameObject("WorldObjects").transform;
                objectsParent.SetParent(transform);
            }

            // Thiết lập sorting cho isometric 2.5D
            if (mainCam != null)
            {
                mainCam.transparencySortMode = TransparencySortMode.CustomAxis;
                mainCam.transparencySortAxis = new Vector3(0f, 1f, 0f);
            }

            // Sinh chunk ban đầu ngay lập tức để tránh màn hình trống
            UpdateChunkQueue(GetPlayerChunkCoord());

            StartCoroutine(ChunkStreamingLoop());
        }

        void Update()
        {
            Vector2Int playerChunk = GetPlayerChunkCoord();

            if (playerChunk != lastPlayerChunk)
            {
                lastPlayerChunk = playerChunk;
                UpdateChunkQueue(playerChunk);
            }
        }

        private Vector2Int GetPlayerChunkCoord()
        {
            Vector3 pos = Vector3.zero;
            if (mainCam != null)
            {
                pos = mainCam.transform.position;
                // Nếu camera đang nhìn vào một điểm (như Player), ta nên lấy điểm đó.
                // Nhưng mặc định dùng vị trí camera là đủ nếu camera follow player.
            }
            
            // Tìm Player nếu có để chính xác hơn
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) pos = player.transform.position;

            return new Vector2Int(
                Mathf.FloorToInt(pos.x / config.chunkSize),
                Mathf.FloorToInt(pos.y / config.chunkSize)
            );
        }
        #endregion

        #region Quản lý Chunk
        private void UpdateChunkQueue(Vector2Int playerChunk)
        {
            // Thêm các chunk mới vào hàng đợi dựa trên viewDistance
            for (int x = -config.viewDistance; x <= config.viewDistance; x++)
            {
                for (int y = -config.viewDistance; y <= config.viewDistance; y++)
                {
                    Vector2Int coord = playerChunk + new Vector2Int(x, y);
                    if (!loadedChunks.ContainsKey(coord) && !chunkLoadQueue.Contains(coord))
                    {
                        chunkLoadQueue.Enqueue(coord);
                    }
                }
            }

            // Hủy các chunk ở xa (viewDistance + 2)
            List<Vector2Int> toUnload = new();
            foreach (var coord in loadedChunks.Keys)
            {
                if (Vector2Int.Distance(coord, playerChunk) > config.viewDistance + 2)
                    toUnload.Add(coord);
            }
            foreach (var coord in toUnload) UnloadChunk(coord);
        }

        private IEnumerator ChunkStreamingLoop()
        {
            while (true)
            {
                if (chunkLoadQueue.Count > 0)
                {
                    Vector2Int coord = chunkLoadQueue.Dequeue();
                    LoadChunk(coord);
                    yield return null; // Mỗi frame load 1 chunk để tránh drop FPS
                }
                else
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }

        private void LoadChunk(Vector2Int coord)
        {
            if (loadedChunks.ContainsKey(coord)) return;

            ChunkData chunk = new ChunkData(coord, config.chunkSize);
            chunkGen.GenerateChunkData(chunk);

            // 1. Sinh Mesh cho địa hình
            if (config.useMeshTerrain)
            {
                GameObject meshObj = new GameObject($"MeshChunk_{coord.x}_{coord.y}");
                meshObj.transform.position = new Vector3(coord.x * config.chunkSize, coord.y * config.chunkSize, 0);
                meshObj.transform.SetParent(transform);

                Mesh mesh = MeshTerrainGenerator.GenerateMesh(coord, config.chunkSize, 1f, config, chunkGen.GetNoiseOffset());
                
                meshObj.AddComponent<MeshFilter>().sharedMesh = mesh;
                meshObj.AddComponent<MeshRenderer>().sharedMaterial = config.terrainMaterial;
                meshObj.AddComponent<MeshCollider>().sharedMesh = mesh;

                meshChunks[coord] = meshObj;
            }

            // 2. Sinh các đối tượng (Cây, đá, v.v.)
            SpawnObjectsInChunk(chunk);

            loadedChunks[coord] = chunk;
        }

        private void UnloadChunk(Vector2Int coord)
        {
            if (loadedChunks.TryGetValue(coord, out ChunkData chunk))
            {
                foreach (var obj in chunk.spawnedObjects)
                {
                    if (obj != null) Destroy(obj);
                }
                loadedChunks.Remove(coord);
            }

            if (meshChunks.TryGetValue(coord, out GameObject meshObj))
            {
                if (meshObj != null) Destroy(meshObj);
                meshChunks.Remove(coord);
            }
        }
        #endregion

        #region Sinh Đối Tượng
        private void SpawnObjectsInChunk(ChunkData chunk)
        {
            for (int y = 0; y < config.chunkSize; y++)
            {
                for (int x = 0; x < config.chunkSize; x++)
                {
                    int worldX = chunk.coord.x * config.chunkSize + x;
                    int worldY = chunk.coord.y * config.chunkSize + y;
                    float height01 = chunk.noiseValues[y, x];
                    BiomeType biome = chunk.biomeMap[y, x];

                    // Lấy config spawn cho biome này
                    BiomeSpawnConfig biomeConfig = config.biomeConfigs.Find(c => c.biomeType == biome);
                    if (biomeConfig == null || biomeConfig.spawnableObjects.Count == 0) continue;

                    if (chunkGen.CanSpawnObject(worldX, worldY, biome, biomeConfig.overallDensity))
                    {
                        SpawnableObjectConfig selected = GetWeightedRandomObject(biomeConfig.spawnableObjects);
                        if (selected == null) continue;

                        // Tính toán vị trí spawn trên mesh 3D
                        Vector3 spawnPos = new Vector3(worldX + 0.5f, worldY + 0.5f, height01 * config.meshHeightMultiplier);
                        
                        GameObject go = Instantiate(selected.prefab, spawnPos, Quaternion.identity, objectsParent);
                        chunk.spawnedObjects.Add(go);
                    }
                }
            }
        }

        private SpawnableObjectConfig GetWeightedRandomObject(List<SpawnableObjectWeight> weights)
        {
            float totalWeight = 0;
            foreach (var w in weights) totalWeight += w.weight;
            
            float roll = Random.Range(0f, totalWeight);
            float current = 0;
            foreach (var w in weights)
            {
                current += w.weight;
                if (roll <= current) return w.config;
            }
            return null;
        }
        #endregion

        #region Editor Tools
        [ContextMenu("Bake Noise Preview")]
        public void BakeNoisePreview()
        {
            // Công cụ debug trong Editor
            int res = 256;
            Texture2D tex = new Texture2D(res, res);
            Vector2 offset = chunkGen != null ? chunkGen.GetNoiseOffset() : Vector2.zero;
            
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float h = ImprovedNoiseGenerator.GetAdvancedHeight(x, y, config, offset);
                    tex.SetPixel(x, y, new Color(h, h, h));
                }
            }
            tex.Apply();

            // Hiển thị Preview thông qua Editor Window (Sẽ được xử lý bởi WorldGeneratorEditor)
            LastBakePreview = tex;
        }

        public static Texture2D LastBakePreview;
        #endregion
    }

    /// <summary>
    /// MeshTerrainGenerator — Procedural 3D mesh generation for chunks.
    /// Supports height displacement, normals, tangents, and biome-aware vertex data.
    /// </summary>
    public static class MeshTerrainGenerator
    {
        public static Mesh GenerateMesh(Vector2Int chunkCoord, int chunkSize, float tileSize, WorldGenerationConfig config, Vector2 seedOffset)
        {
            int res = chunkSize + 1;
            Vector3[] vertices = new Vector3[res * res];
            Vector2[] uv = new Vector2[res * res];
            Color[] colors = new Color[res * res];
            int[] triangles = new int[chunkSize * chunkSize * 6];

            int v = 0;
            int t = 0;

            for (int y = 0; y <= chunkSize; y++)
            {
                for (int x = 0; x <= chunkSize; x++)
                {
                    int worldX = chunkCoord.x * chunkSize + x;
                    int worldY = chunkCoord.y * chunkSize + y;

                    float height01 = ImprovedNoiseGenerator.GetAdvancedHeight(worldX, worldY, config, seedOffset);
                    float zHeight = height01 * config.meshHeightMultiplier;

                    vertices[v] = new Vector3(x * tileSize, y * tileSize, zHeight);
                    uv[v] = new Vector2((float)x / chunkSize, (float)y / chunkSize);
                    
                    // Biome-aware coloring (for simple Shader Graph or debug)
                    BiomeType biome = ImprovedNoiseGenerator.GetBiome(height01, config);
                    colors[v] = GetBiomeColor(biome);

                    if (x < chunkSize && y < chunkSize)
                    {
                        // Sửa thứ tự triangle để mặt hướng lên (Clockwise)
                        triangles[t + 0] = v;
                        triangles[t + 1] = v + res;
                        triangles[t + 2] = v + res + 1;
                        
                        triangles[t + 3] = v;
                        triangles[t + 4] = v + res + 1;
                        triangles[t + 5] = v + 1;
                        t += 6;
                    }
                    v++;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = $"Chunk_{chunkCoord.x}_{chunkCoord.y}";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.colors = colors;
            
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            
            return mesh;
        }

        private static Color GetBiomeColor(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Water => new Color(0.2f, 0.5f, 0.9f),
                BiomeType.Path => new Color(0.8f, 0.7f, 0.4f),
                BiomeType.Brush => new Color(0.4f, 0.6f, 0.2f),
                BiomeType.Grass => new Color(0.3f, 0.8f, 0.3f),
                BiomeType.Stone => new Color(0.6f, 0.6f, 0.6f),
                _ => Color.white
            };
        }
    }
}