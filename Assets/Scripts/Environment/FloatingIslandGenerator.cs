using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class FloatingIslandGenerator : MonoBehaviour
{
    [Header("Seed")]
    public int seed = 12345;
    public bool randomizeSeedOnGenerate = false;

    [Header("Shape & Size")]
    [Tooltip("Base size of the island on X/Y/Z before variation is applied.")]
    public Vector3 baseSize = new Vector3(10f, 6f, 10f);
    [Range(0f, 1f)]
    [Tooltip("How much the size is randomly varied per axis, driven by the seed.")]
    public float sizeVariation = 0.25f;
    [Range(0, 4)]
    [Tooltip("Icosphere subdivision level. 3 is a good default; 4 is heavy.")]
    public int subdivisions = 3;
    [Range(-0.6f, 0.4f)]
    [Tooltip("Height (in unit-sphere space, -1 to 1) where the grassy top meets the rocky underside.")]
    public float equatorHeight = 0f;

    [Header("Top Surface")]
    [Range(0f, 1f)]
    [Tooltip("0 = fully round dome (like a sphere). 1 = flat mesa/plateau on top with the curve pushed out to the rim.")]
    public float topFlatness = 0.7f;
    [Range(0.5f, 8f)]
    [Tooltip("Higher values keep the plateau flatter for longer and make the rim drop off more sharply. Only matters when Top Flatness > 0.")]
    public float topEdgeSharpness = 3.5f;
    public float topNoiseScale = 1.2f;
    [Range(0f, 1f)]
    public float topNoiseStrength = 0.25f;
    [Range(1, 4)]
    [Tooltip("Number of noise layers for detail. Higher = more detailed mountain ridges.")]
    public int topOctaves = 3;

    [Header("Rim Distortion")]
    [Range(0f, 0.8f)]
    [Tooltip("Distorts the circular outline of the island to make it look organic instead of a round disc.")]
    public float rimJaggedness = 0.4f;
    public float rimNoiseScale = 1.5f;

    [Header("Erosion & Canyons")]
    [Range(0f, 0.5f)]
    [Tooltip("Carves deep crevices and valleys into the side and top of the island.")]
    public float erosionStrength = 0.2f;
    public float erosionScale = 2.0f;

    [Header("Natural Bottom")]
    [Tooltip("How far the rocky underside tapers down, relative to baseSize.y.")]
    [Range(0.5f, 4f)]
    public float bottomDepth = 1.6f;
    [Tooltip("Higher = the underside pinches to points faster (more stalactite-like).")]
    [Range(1f, 4f)]
    public float taperSharpness = 2.2f;
    public float bottomNoiseScale = 2.5f;
    [Range(0f, 0.5f)]
    public float bottomRoughness = 0.12f;
    [Range(0f, 0.7f)]
    [Tooltip("How tightly the underside pulls inward as it descends.")]
    public float bottomContraction = 0.85f;

    [Header("Direct Sculpt Cage")]
    [Tooltip("Enables the draggable ring/point cage in the Scene view for direct shape sculpting.")]
    public bool useCage = true;
    [Tooltip("Rings of control points used to directly sculpt the island's silhouette. The top and bottom entries are single pole points; rings in between can have multiple draggable points spaced around the island.")]
    public List<CageRing> cageRings = new List<CageRing>();

    [Header("Colors")]
    public Gradient colorGradient;
    [Range(0f, 0.3f)]
    public float colorNoiseJitter = 0.06f;

    [Header("Extras")]
    public bool generateMeshCollider = true;
    public Material overrideMaterial;

    [System.Serializable]
    public class CageRing
    {
        [Range(-1f, 1f)]
        [Tooltip("Height along the island, from -1 (bottom tip) to 1 (top), same space as Equator Height.")]
        public float heightT;
        [Range(1, 16)]
        [Tooltip("Number of draggable points around this ring. Use 1 for a single pole point (e.g. the very top or bottom tip).")]
        public int pointCount = 8;
        [Tooltip("Per-point radial offset. 1 = unchanged, greater than 1 bulges outward, less than 1 pinches inward.")]
        public List<float> radiusOffsets = new List<float>();
        [Tooltip("Per-point vertical nudge, added on top of this ring's base height.")]
        public List<float> heightOffsets = new List<float>();
    }

    private Mesh _mesh;
    private System.Random _rng;
    private List<CageRing> _sortedCage;

    private static readonly int[] IcoFaces = {
        0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
        1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
        3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
        4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
    };

    private void OnEnable()
    {
        if (colorGradient == null || colorGradient.colorKeys.Length == 0)
            SetDefaultGradient();

        if (cageRings == null || cageRings.Count == 0)
            GenerateDefaultCage();
    }

    /// <summary>Resets the cage to a default: a top pole, three 8-point rings, and a bottom pole.</summary>
    public void GenerateDefaultCage()
    {
        cageRings = new List<CageRing>
        {
            MakeRing(1f, 1),
            MakeRing(0.45f, 8),
            MakeRing(0f, 8),
            MakeRing(-0.45f, 8),
            MakeRing(-1f, 1),
        };
    }

    private static CageRing MakeRing(float heightT, int pointCount)
    {
        CageRing ring = new CageRing { heightT = heightT, pointCount = pointCount };
        EnsureRingArrays(ring);
        return ring;
    }

    /// <summary>Pads or trims a ring's offset lists so they match its point count. Safe to call after inspector edits.</summary>
    public static void EnsureRingArrays(CageRing ring)
    {
        ring.pointCount = Mathf.Max(1, ring.pointCount);
        if (ring.radiusOffsets == null) ring.radiusOffsets = new List<float>();
        if (ring.heightOffsets == null) ring.heightOffsets = new List<float>();

        while (ring.radiusOffsets.Count < ring.pointCount) ring.radiusOffsets.Add(1f);
        while (ring.radiusOffsets.Count > ring.pointCount) ring.radiusOffsets.RemoveAt(ring.radiusOffsets.Count - 1);

        while (ring.heightOffsets.Count < ring.pointCount) ring.heightOffsets.Add(0f);
        while (ring.heightOffsets.Count > ring.pointCount) ring.heightOffsets.RemoveAt(ring.heightOffsets.Count - 1);
    }

    private void SetDefaultGradient()
    {
        colorGradient = new Gradient();
        var colorKeys = new GradientColorKey[4];
        colorKeys[0] = new GradientColorKey(new Color(0.29f, 0.55f, 0.22f), 0.85f);
        colorKeys[1] = new GradientColorKey(new Color(0.42f, 0.32f, 0.20f), 0.65f);
        colorKeys[2] = new GradientColorKey(new Color(0.35f, 0.34f, 0.33f), 0.35f);
        colorKeys[3] = new GradientColorKey(new Color(0.15f, 0.14f, 0.14f), 0.0f);
        var alphaKeys = new GradientAlphaKey[2] {
            new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f)
        };
        colorGradient.SetKeys(colorKeys, alphaKeys);
    }

    [ContextMenu("Generate Island")]
    public void Generate()
    {
        if (randomizeSeedOnGenerate)
            seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

        _rng = new System.Random(seed);

        if (cageRings == null) cageRings = new List<CageRing>();
        foreach (CageRing ring in cageRings) EnsureRingArrays(ring);
        _sortedCage = new List<CageRing>(cageRings);
        _sortedCage.Sort((a, b) => a.heightT.CompareTo(b.heightT));

        Vector3 sizeMul = new Vector3(
            1f + ((float)_rng.NextDouble() * 2f - 1f) * sizeVariation,
            1f + ((float)_rng.NextDouble() * 2f - 1f) * sizeVariation,
            1f + ((float)_rng.NextDouble() * 2f - 1f) * sizeVariation
        );
        Vector3 finalSize = Vector3.Scale(baseSize, sizeMul);

        Vector3 noiseOffset = new Vector3(
            (float)_rng.NextDouble() * 1000f,
            (float)_rng.NextDouble() * 1000f,
            (float)_rng.NextDouble() * 1000f
        );

        List<Vector3> verts;
        List<int> tris;
        BuildIcosphere(subdivisions, out verts, out tris);

        int count = verts.Count;
        Vector3[] shaped = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            Vector3 dir = verts[i];
            shaped[i] = ShapeVertex(dir, noiseOffset);
        }

        for (int i = 0; i < count; i++)
        {
            shaped[i] = new Vector3(
                shaped[i].x * finalSize.x,
                shaped[i].y * finalSize.y,
                shaped[i].z * finalSize.z
            );
        }

        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < count; i++)
        {
            if (shaped[i].y < minY) minY = shaped[i].y;
            if (shaped[i].y > maxY) maxY = shaped[i].y;
        }

        Color[] colors = new Color[count];
        for (int i = 0; i < count; i++)
        {
            float t = Mathf.InverseLerp(minY, maxY, shaped[i].y);
            float jitter = (Noise3D(verts[i] * bottomNoiseScale * 1.7f + noiseOffset) - 0.5f) * colorNoiseJitter;
            Color c = colorGradient.Evaluate(Mathf.Clamp01(t + jitter));
            colors[i] = c;
        }

        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.name = "FloatingIsland";
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }
        else
        {
            _mesh.Clear();
        }

        _mesh.SetVertices(new List<Vector3>(shaped));
        _mesh.SetTriangles(tris, 0);
        _mesh.SetColors(colors);
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
        _mesh.RecalculateTangents();

        var mf = GetComponent<MeshFilter>();
        mf.sharedMesh = _mesh;

        var mr = GetComponent<MeshRenderer>();
        if (overrideMaterial != null)
        {
            mr.sharedMaterial = overrideMaterial;
        }
        else if (mr.sharedMaterial == null)
        {
            Shader sh = Shader.Find("Custom/IslandVertexColor");
            if (sh != null) mr.sharedMaterial = new Material(sh);
        }

        if (generateMeshCollider)
        {
            var col = GetComponent<MeshCollider>();
            if (col == null) col = gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = null;
            col.sharedMesh = _mesh;
        }
    }

    private Vector3 ShapeVertex(Vector3 dir, Vector3 noiseOffset)
    {
        float y = dir.y;
        float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(equatorHeight - 0.12f, equatorHeight + 0.12f, y));

        float horiz = Mathf.Sqrt(dir.x * dir.x + dir.z * dir.z);
        Vector2 horizDir = horiz > 0.0001f ? new Vector2(dir.x / horiz, dir.z / horiz) : Vector2.zero;

        float rimNoise = Noise3D(new Vector3(dir.x, 0f, dir.z) * rimNoiseScale + noiseOffset);
        float rimDistort = 1f + (rimNoise - 0.5f) * 2f * rimJaggedness;

        float domeY = dir.y;
        float flatY = 1f - Mathf.Pow(Mathf.Clamp01(horiz), topEdgeSharpness);
        float shapedY = Mathf.Lerp(domeY, flatY, topFlatness);

        float normalizedHoriz = horiz;
        if (dir.y > 0.0001f)
        {
            float cylinderHoriz = horiz / dir.y;
            normalizedHoriz = Mathf.Lerp(horiz, Mathf.Min(cylinderHoriz, 1f), topFlatness);
        }

        float topN = OctaveNoise3D(dir * topNoiseScale + noiseOffset, topOctaves);

        float erosionNoise = Noise3D(dir * erosionScale + noiseOffset * 2f);
        float erosionFactor = Mathf.Abs(erosionNoise - 0.5f) * 2f;
        float erosionInvert = Mathf.Lerp(1f, erosionFactor, erosionStrength);

        float topHeightModifier = 1f + (topN - 0.3f) * topNoiseStrength;

        Vector3 topVert = new Vector3(
            horizDir.x * normalizedHoriz * rimDistort * topHeightModifier,
            shapedY * topHeightModifier * erosionInvert,
            horizDir.y * normalizedHoriz * rimDistort * topHeightModifier
        );

        float t = Mathf.Clamp01(Mathf.InverseLerp(equatorHeight, -1f, y));
        float depthT = Mathf.Pow(t, taperSharpness);
        float depth = depthT * bottomDepth;

        float contraction = Mathf.Clamp01(1f - t * bottomContraction);

        float roughN1 = Noise3D(dir * bottomNoiseScale + noiseOffset) - 0.5f;
        float roughN2 = Noise3D(dir * bottomNoiseScale * 2.3f + noiseOffset * 1.3f) - 0.5f;

        float bx = dir.x * contraction * rimDistort + roughN1 * bottomRoughness * t;
        float bz = dir.z * contraction * rimDistort + roughN2 * bottomRoughness * t;
        float by = equatorHeight - depth;

        Vector3 bottomVert = new Vector3(bx, by, bz);

        Vector3 result = Vector3.Lerp(bottomVert, topVert, blend);

        if (useCage && _sortedCage != null && _sortedCage.Count > 0)
        {
            float angleDeg = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
            SampleCage(y, angleDeg, out float radiusMul, out float heightAdd);
            result.x *= radiusMul;
            result.z *= radiusMul;
            result.y += heightAdd;
        }

        return result;
    }

    /// <summary>Samples the cage at a given unit-sphere height and angle, blending between the two nearest rings.</summary>
    private void SampleCage(float y, float angleDeg, out float radiusMul, out float heightAdd)
    {
        radiusMul = 1f;
        heightAdd = 0f;

        if (_sortedCage.Count == 1)
        {
            SampleRing(_sortedCage[0], angleDeg, out radiusMul, out heightAdd);
            return;
        }

        CageRing lower = null;
        CageRing upper = null;
        for (int i = 0; i < _sortedCage.Count; i++)
        {
            if (_sortedCage[i].heightT <= y) lower = _sortedCage[i];
            if (upper == null && _sortedCage[i].heightT >= y) upper = _sortedCage[i];
        }
        if (lower == null) lower = _sortedCage[0];
        if (upper == null) upper = _sortedCage[_sortedCage.Count - 1];

        if (lower == upper)
        {
            SampleRing(lower, angleDeg, out radiusMul, out heightAdd);
            return;
        }

        float t = Mathf.InverseLerp(lower.heightT, upper.heightT, y);
        SampleRing(lower, angleDeg, out float rl, out float hl);
        SampleRing(upper, angleDeg, out float ru, out float hu);
        radiusMul = Mathf.Lerp(rl, ru, t);
        heightAdd = Mathf.Lerp(hl, hu, t);
    }

    /// <summary>Interpolates a single ring's per-point offsets at a given angle, wrapping around the circle.</summary>
    private static void SampleRing(CageRing ring, float angleDeg, out float radiusOffset, out float heightOffset)
    {
        int n = Mathf.Max(1, ring.pointCount);
        if (n <= 1 || ring.radiusOffsets == null || ring.radiusOffsets.Count == 0)
        {
            radiusOffset = (ring.radiusOffsets != null && ring.radiusOffsets.Count > 0) ? ring.radiusOffsets[0] : 1f;
            heightOffset = (ring.heightOffsets != null && ring.heightOffsets.Count > 0) ? ring.heightOffsets[0] : 0f;
            return;
        }

        float a = ((angleDeg % 360f) + 360f) % 360f;
        float step = 360f / n;
        float idxF = a / step;
        int i0 = Mathf.FloorToInt(idxF) % n;
        int i1 = (i0 + 1) % n;
        float frac = idxF - Mathf.Floor(idxF);

        radiusOffset = Mathf.Lerp(ring.radiusOffsets[i0], ring.radiusOffsets[i1], frac);
        heightOffset = Mathf.Lerp(ring.heightOffsets[i0], ring.heightOffsets[i1], frac);
    }

    private static float Noise3D(Vector3 p)
    {
        float ab = Mathf.PerlinNoise(p.x, p.y);
        float bc = Mathf.PerlinNoise(p.y, p.z);
        float ca = Mathf.PerlinNoise(p.z, p.x);
        return (ab + bc + ca) / 3f;
    }

    private static float OctaveNoise3D(Vector3 p, int octaves)
    {
        float total = 0f;
        float frequency = 1f;
        float amplitude = 1f;
        float maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            total += Noise3D(p * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.0f;
        }

        return total / maxValue;
    }

    private void BuildIcosphere(int subdiv, out List<Vector3> vertices, out List<int> triangles)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();

        float t = (1f + Mathf.Sqrt(5f)) / 2f;

        void AddV(Vector3 p) => verts.Add(p.normalized);

        AddV(new Vector3(-1, t, 0));
        AddV(new Vector3(1, t, 0));
        AddV(new Vector3(-1, -t, 0));
        AddV(new Vector3(1, -t, 0));

        AddV(new Vector3(0, -1, t));
        AddV(new Vector3(0, 1, t));
        AddV(new Vector3(0, -1, -t));
        AddV(new Vector3(0, 1, -t));

        AddV(new Vector3(t, 0, -1));
        AddV(new Vector3(t, 0, 1));
        AddV(new Vector3(-t, 0, -1));
        AddV(new Vector3(-t, 0, 1));

        tris.AddRange(IcoFaces);

        var cache = new Dictionary<long, int>();

        int GetMiddle(int p1, int p2)
        {
            bool firstSmaller = p1 < p2;
            long smaller = firstSmaller ? p1 : p2;
            long greater = firstSmaller ? p2 : p1;
            long key = (smaller << 32) + greater;

            if (cache.TryGetValue(key, out int existing))
                return existing;

            Vector3 mid = (verts[p1] + verts[p2]) * 0.5f;
            verts.Add(mid.normalized);
            int idx = verts.Count - 1;
            cache[key] = idx;
            return idx;
        }

        for (int s = 0; s < subdiv; s++)
        {
            var newTris = new List<int>(tris.Count * 4);
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i];
                int b = tris[i + 1];
                int c = tris[i + 2];

                int ab = GetMiddle(a, b);
                int bc = GetMiddle(b, c);
                int ca = GetMiddle(c, a);

                newTris.Add(a); newTris.Add(ab); newTris.Add(ca);
                newTris.Add(b); newTris.Add(bc); newTris.Add(ab);
                newTris.Add(c); newTris.Add(ca); newTris.Add(bc);
                newTris.Add(ab); newTris.Add(bc); newTris.Add(ca);
            }
            tris = newTris;
            cache.Clear();
        }

        vertices = verts;
        triangles = tris;
    }
}