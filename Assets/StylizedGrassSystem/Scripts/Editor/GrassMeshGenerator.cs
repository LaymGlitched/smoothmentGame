#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace StylizedGrassSystem
{
    public static class GrassMeshGenerator
    {
        [MenuItem("Tools/Stylized Grass/Generate Blade Mesh")]
        public static void GenerateMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "GrassBlade";

            // Single tapered quad: 4 vertices, 2 triangles
            // This is the production-standard approach for massive instanced grass.
            // The bottom edge is the full width, the top two vertices pinch to form a pointed blade.
            float width = 0.1f;
            float height = 1.0f;
            float tipWidth = 0.01f; // Slight tip width prevents z-fighting artifacts
            float curve = 0.05f; // Subtle forward lean at the tip

            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-width * 0.5f, 0f, 0f),        // Bottom-left  (root)
                new Vector3( width * 0.5f, 0f, 0f),        // Bottom-right (root)
                new Vector3(-tipWidth * 0.5f, height, curve), // Top-left   (tip)
                new Vector3( tipWidth * 0.5f, height, curve), // Top-right  (tip)
            };

            Vector2[] uvs = new Vector2[4]
            {
                new Vector2(0f, 0f),    // Bottom-left
                new Vector2(1f, 0f),    // Bottom-right
                new Vector2(0f, 1f),    // Top-left
                new Vector2(1f, 1f),    // Top-right
            };

            Vector3[] normals = new Vector3[4]
            {
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back,
            };

            int[] triangles = new int[6]
            {
                0, 2, 1,    // Lower-left triangle
                1, 2, 3,    // Upper-right triangle
            };

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = triangles;
            
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Save asset
            if (!AssetDatabase.IsValidFolder("Assets/StylizedGrassSystem/Meshes"))
            {
                System.IO.Directory.CreateDirectory(Application.dataPath + "/StylizedGrassSystem/Meshes");
                AssetDatabase.Refresh();
            }

            string path = "Assets/StylizedGrassSystem/Meshes/GrassBlade.asset";
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Generated grass blade mesh at {path} (4 verts, 2 tris)");
        }
    }
}
#endif
