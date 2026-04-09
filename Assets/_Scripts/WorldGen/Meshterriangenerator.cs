// ==================== FILENAME: MeshTerrainGenerator.cs ====================
using UnityEngine;

namespace ProceduralWorld.Generation
{
    /// <summary>
    /// Mesh Terrain Generator
    /// Creates a displaced mesh for each chunk based on height data
    /// Includes normals, tangents, and UVs for proper lighting
    /// 
    /// Architecture:
    ///   • Creates quad grid of vertices
    ///   • Displaces vertices by height
    ///   • Generates proper topology
    ///   • Calculates normals + tangents for lighting
    /// </summary>
    public static class MeshTerrainGenerator
    {
        #region ===== MESH GENERATION =====
        /// <summary>
        /// Generate terrain mesh from height grid
        /// </summary>
        public static Mesh GenerateTerrainMesh(
            float[,] heightMap,
            Vector2Int chunkCoord,
            int chunkSize,
            float tileSize,
            WorldGenerationConfig config)
        {
            Mesh mesh = new Mesh();
            mesh.name = $"TerrainMesh_Chunk_{chunkCoord.x}_{chunkCoord.y}";

            int resolution = config.meshResolution;
            int vertexCount = resolution * resolution;

            // Arrays
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];

            #region Generate Vertices
            int vertIndex = 0;
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    // Interpolate height from heightMap
                    float mapX = x / (float)(resolution - 1) * (chunkSize - 1);
                    float mapY = y / (float)(resolution - 1) * (chunkSize - 1);

                    float height = SampleHeightBilinear(heightMap, mapX, mapY);

                    // World position
                    float worldX = (chunkCoord.x * chunkSize + mapX) * tileSize;
                    float worldY = (chunkCoord.y * chunkSize + mapY) * tileSize;

                    // Vertex with displacement
                    vertices[vertIndex] = new Vector3(
                        worldX,
                        worldY + (height * config.meshDisplacement),
                        height * 0.5f  // Z for isometric depth
                    );

                    // UV
                    uv[vertIndex] = new Vector2(x / (float)(resolution - 1), y / (float)(resolution - 1));

                    vertIndex++;
                }
            }
            #endregion

            #region Generate Triangles
            int triIndex = 0;
            for (int y = 0; y < resolution - 1; y++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    int v0 = y * resolution + x;
                    int v1 = y * resolution + (x + 1);
                    int v2 = (y + 1) * resolution + x;
                    int v3 = (y + 1) * resolution + (x + 1);

                    // Triangle 1
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

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;

            // Critical: Calculate normals and tangents for lighting
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            return mesh;
        }
        #endregion

        #region ===== HEIGHT INTERPOLATION =====
        /// <summary>
        /// Bilinear interpolation for smooth height sampling
        /// </summary>
        private static float SampleHeightBilinear(float[,] heightMap, float x, float y)
        {
            int width = heightMap.GetLength(0);
            int height = heightMap.GetLength(1);

            // Clamp to bounds
            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);

            int x0 = (int)x;
            int y0 = (int)y;
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);

            float fx = x - x0;
            float fy = y - y0;

            // Bilinear interpolation
            float h00 = heightMap[x0, y0];
            float h10 = heightMap[x1, y0];
            float h01 = heightMap[x0, y1];
            float h11 = heightMap[x1, y1];

            float h0 = Mathf.Lerp(h00, h10, fx);
            float h1 = Mathf.Lerp(h01, h11, fx);

            return Mathf.Lerp(h0, h1, fy);
        }
        #endregion

        #region ===== MESH COLLIDER =====
        /// <summary>
        /// Create mesh collider from terrain mesh
        /// </summary>
        public static void ApplyMeshCollider(GameObject meshObject, Mesh mesh)
        {
            MeshCollider collider = meshObject.GetComponent<MeshCollider>();
            if (collider == null)
                collider = meshObject.AddComponent<MeshCollider>();

            collider.sharedMesh = null;  // Force reset
            collider.sharedMesh = mesh;
            collider.convex = false;
        }
        #endregion
    }
}