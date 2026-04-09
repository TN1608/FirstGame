// ==================== ProperHeightMapTerrainGenerator.cs ====================
// WORKING 3D Terrain Mesh Generator for Isometric 2.5D
// Creates actual 3D displaced geometry, not flat meshes

using _Scripts.WorldGen;
using UnityEngine;
using ProceduralWorld.Generation;
using ProceduralWorld.Data;

namespace ProceduralWorld.Generation
{
    /// <summary>
    /// PROPER Height Map Terrain Generator
    /// 
    /// Creates actual 3D displaced mesh terrain for 2.5D isometric games
    /// Based on: Minecraft terrain + IsoCORE style layering
    /// 
    /// Features:
    /// • Proper vertex displacement based on height
    /// • Multiple material regions (water, sand, grass, stone)
    /// • Correct normals for lighting
    /// • UV mapping for texture variation
    /// • Mesh optimization
    /// </summary>
    public static class ProperHeightMapTerrainGenerator
    {
        #region ===== MAIN MESH GENERATION =====
        /// <summary>
        /// Generate proper 3D displaced mesh from height data
        /// </summary>
        public static Mesh GenerateDisplacedTerrainMesh(
            float[,] heightMap,
            BiomeType[,] biomeMap,
            Vector2Int chunkCoord,
            int chunkSize,
            float tileSize,
            WorldGenerationConfig config)
        {
            Mesh mesh = new Mesh();
            mesh.name = $"TerrainMesh_{chunkCoord.x}_{chunkCoord.y}";

            int resolution = chunkSize + 1;  // Vertices = tiles + 1
            Vector3[] vertices = new Vector3[resolution * resolution];
            Vector2[] uv = new Vector2[resolution * resolution];
            Color[] vertexColors = new Color[resolution * resolution];
            int[] triangles = new int[chunkSize * chunkSize * 6];

            #region Generate Vertices with Height Displacement
            int vertIndex = 0;
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    // Get height at this vertex
                    int heightMapX = Mathf.Min(x, chunkSize - 1);
                    int heightMapY = Mathf.Min(y, chunkSize - 1);
                    float height = heightMap[heightMapY, heightMapX];
                    
                    // Get biome for coloring
                    BiomeType biome = biomeMap[heightMapY, heightMapX];

                    // World position
                    float worldX = (chunkCoord.x * chunkSize + x) * tileSize;
                    float worldY = (chunkCoord.y * chunkSize + y) * tileSize;

                    // Displace Y based on height
                    float displacement = height * config.splineShape.heightMultiplier * 0.1f;

                    // Vertex position (3D with height displacement)
                    vertices[vertIndex] = new Vector3(
                        worldX,
                        worldY + displacement,  // ← HEIGHT DISPLACEMENT
                        height * 0.5f           // Z for isometric depth
                    );

                    // UV mapping
                    uv[vertIndex] = new Vector2(
                        x / (float)chunkSize,
                        y / (float)chunkSize
                    );

                    // Vertex color based on biome (for quick biome visualization)
                    vertexColors[vertIndex] = GetBiomeColor(biome, height);

                    vertIndex++;
                }
            }
            #endregion

            #region Generate Triangles
            int triIndex = 0;
            for (int y = 0; y < chunkSize; y++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    int v0 = y * resolution + x;
                    int v1 = y * resolution + (x + 1);
                    int v2 = (y + 1) * resolution + x;
                    int v3 = (y + 1) * resolution + (x + 1);

                    // Triangle 1 (CCW for front face)
                    triangles[triIndex++] = v0;
                    triangles[triIndex++] = v2;
                    triangles[triIndex++] = v1;

                    // Triangle 2
                    triangles[triIndex++] = v1;
                    triangles[triIndex++] = v2;
                    triangles[triIndex++] = v3;
                }
            }
            #endregion

            // Apply data
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors = vertexColors;
            mesh.triangles = triangles;

            // Recalculate normals and tangents for proper lighting
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            // Optimize
            mesh.Optimize();

            return mesh;
        }
        #endregion

        #region ===== BIOME COLOR MAPPING =====
        private static Color GetBiomeColor(BiomeType biome, float height)
        {
            // Colors for visualization/shading
            switch (biome)
            {
                case BiomeType.Water:
                    return Color.Lerp(new Color(0.1f, 0.3f, 0.6f), new Color(0.2f, 0.5f, 0.8f), height * 2f);

                case BiomeType.Path:  // Sand/Beach
                    return Color.Lerp(new Color(0.9f, 0.8f, 0.5f), new Color(0.8f, 0.7f, 0.4f), height * 2f);

                case BiomeType.Brush:
                    return Color.Lerp(new Color(0.6f, 0.6f, 0.3f), new Color(0.5f, 0.5f, 0.2f), height * 2f);

                case BiomeType.Grass:
                    return Color.Lerp(new Color(0.2f, 0.6f, 0.2f), new Color(0.3f, 0.7f, 0.3f), height * 2f);

                case BiomeType.Stone:
                    return Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.6f, 0.6f, 0.6f), height * 2f);

                default:
                    return Color.gray;
            }
        }
        #endregion

        #region ===== MESH COLLIDER SETUP =====
        public static void SetupMeshCollider(GameObject meshObject, Mesh mesh)
        {
            MeshCollider collider = meshObject.GetComponent<MeshCollider>();
            if (collider == null)
                collider = meshObject.AddComponent<MeshCollider>();

            collider.sharedMesh = null;
            collider.sharedMesh = mesh;
            collider.convex = false;
        }
        #endregion

        #region ===== HEIGHT INTERPOLATION =====
        public static float SampleHeight(float[,] heightMap, float x, float y)
        {
            int width = heightMap.GetLength(0);
            int height = heightMap.GetLength(1);

            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);

            int x0 = (int)x;
            int y0 = (int)y;
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);

            float fx = x - x0;
            float fy = y - y0;

            float h00 = heightMap[y0, x0];
            float h10 = heightMap[y0, x1];
            float h01 = heightMap[y1, x0];
            float h11 = heightMap[y1, x1];

            float h0 = Mathf.Lerp(h00, h10, fx);
            float h1 = Mathf.Lerp(h01, h11, fx);

            return Mathf.Lerp(h0, h1, fy);
        }
        #endregion
    }
}