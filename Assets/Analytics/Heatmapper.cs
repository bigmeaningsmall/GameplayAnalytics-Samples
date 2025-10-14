using System.IO;
using UnityEngine;

[ExecuteAlways]
public class Heatmapper : MonoBehaviour
{
    [Header("Sampling")]
    [Tooltip("Objects to sample (e.g., Player, enemies). Add at least one.")]
    public Transform[] sampleTargets;
    [Tooltip("Sample interval in seconds (0.1–0.5 is typical).")]
    [Range(0.02f, 2f)] public float sampleInterval = 0.25f;
    [Tooltip("Weight per sample (e.g., 1; or increase for important events).")]
    public int sampleWeight = 1;

    [Header("Area & Grid")]
    [Tooltip("World-space XZ origin (bottom-left corner of heatmap).")]
    public Vector2 originXZ = Vector2.zero;
    [Tooltip("World-space size of the heatmap plane in meters (X,Z).")]
    public Vector2 sizeXZ = new Vector2(50, 50);
    [Tooltip("Cell size (meters). Smaller = more detail, heavier to compute.")]
    [Min(0.05f)] public float cellSize = 0.5f;

    [Header("Appearance")]
    [Tooltip("Color ramp from cool (low) to hot (high).")]
    public Gradient gradient;
    [Tooltip("Opacity of the heatmap overlay.")]
    [Range(0f, 1f)] public float opacity = 0.85f;
    [Tooltip("Optional simple blur (0 = off).")]
    [Range(0, 8)] public int blurRadius = 2;
    [Header("Tone Mapping")]
    [Tooltip("Apply logarithmic mapping to boost mid-range density.")]
    public bool useLogScale = true;
    [Tooltip("Exponent for contrast if not using log scale (1 = linear, 2 = more contrast).")]
    [Range(0.5f, 4f)] public float gamma = 1.6f;

    [Tooltip("Lock max normalization (0 = auto). Use to compare runs consistently).")]
    public int fixedMaxCount = 0;

    [Tooltip("Don’t draw cells below this normalized level (0..1).")]
    [Range(0f, 0.5f)] public float minVisibleT = 0.02f;

    [Tooltip("Use additive blending for a punchier overlay (runtime only).")]
    public bool additiveBlend = true;


    [Header("Controls")]
    [Tooltip("Toggle overlay on/off at runtime.")]
    public KeyCode toggleKey = KeyCode.H;
    public bool visible = true;
    [Tooltip("Automatically bake texture every N seconds while playing.")]
    public float rebakeInterval = 0.5f;

    // --- Internals ---
    int width, height;
    int[] counts;
    int maxCount;
    Texture2D tex;
    Material mat;
    GameObject quad;
    float tSample, tBake;
    bool dirty;

    // Cache for blur
    float[] workA, workB;
    
    

    void Reset()
    {
        // Nice default gradient: blue -> cyan -> green -> yellow -> red
        gradient = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.0f, 0.1f, 0.9f), 0f),
                new GradientColorKey(new Color(0.0f, 0.8f, 1.0f), 0.25f),
                new GradientColorKey(new Color(0.1f, 0.9f, 0.2f), 0.5f),
                new GradientColorKey(new Color(1.0f, 0.9f, 0.0f), 0.75f),
                new GradientColorKey(new Color(1.0f, 0.2f, 0.0f), 1f)
            },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        };
    }

    void OnEnable()
    {
        BuildGrid();
        EnsureRenderObjects();
        BakeTexture(); // show something in edit mode
        UpdateQuadVisibility();
    }

    void OnDisable()
    {
        if (Application.isPlaying) return;
        CleanupRuntimeObjects();
    }

    void Update()
    {
        // press 0 key to save PNG
        if (Input.GetKeyDown(KeyCode.P))
        {
            SavePngExact(2048, 2048, false);   
        }
        
        // Hotkey
        if (Application.isPlaying && Input.GetKeyDown(toggleKey))
        {
            visible = !visible;
            UpdateQuadVisibility();
        }

        // Resize grid if parameters changed in edit mode
        if (!Application.isPlaying)
        {
            var (w, h) = CalcGridDims();
            if (w != width || h != height) BuildGrid();
            EnsureRenderObjects();
            BakeTexture();
            return;
        }

        // Sample
        tSample += Time.deltaTime;
        if (tSample >= sampleInterval)
        {
            tSample = 0f;
            SampleTargets();
        }

        // Re-bake periodically
        tBake += Time.deltaTime;
        if (tBake >= rebakeInterval)
        {
            tBake = 0f;
            if (dirty) BakeTexture();
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw bounds in Scene view
        Gizmos.color = Color.cyan;
        Vector3 bl = new Vector3(originXZ.x, 0f, originXZ.y);
        Vector3 tr = bl + new Vector3(sizeXZ.x, 0f, sizeXZ.y);
        Vector3 br = new Vector3(tr.x, 0f, bl.z);
        Vector3 tl = new Vector3(bl.x, 0f, tr.z);
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);
    }

    // --- Public API ---

    /// <summary>Adds a sample at a given world position (useful for event pings).</summary>
    public void AddSample(Vector3 worldPos, int weight = 1)
    {
        if (counts == null) return;
        int ix = Mathf.FloorToInt((worldPos.x - originXZ.x) / cellSize);
        int iy = Mathf.FloorToInt((worldPos.z - originXZ.y) / cellSize);
        if ((uint)ix >= (uint)width || (uint)iy >= (uint)height) return;
        int idx = iy * width + ix;
        int newVal = counts[idx] + Mathf.Max(1, weight);
        counts[idx] = newVal;
        if (newVal > maxCount) maxCount = newVal;
        dirty = true;
    }

    /// <summary>Clears all accumulated samples.</summary>
    [ContextMenu("Clear Heatmap")]
    public void ClearHeatmap()
    {
        if (counts == null) return;
        System.Array.Clear(counts, 0, counts.Length);
        maxCount = 0;
        dirty = true;
        BakeTexture();
    }

    /// <summary>Rebuilds grid (use after changing size/cellSize/origin heavily).</summary>
    [ContextMenu("Rebuild Grid")]
    public void RebuildGrid()
    {
        BuildGrid();
        BakeTexture();
    }

    /// <summary>Save current heatmap texture to PNG (persistentDataPath).</summary>
    [ContextMenu("Save PNG")]
// --- High-res PNG export helpers ---
// Usage examples:
//   SavePngExact(1024, 1024);             // exact size, bilinear
//   SavePngExact(2048, 2048, false);      // exact size, nearest-neighbour
//   SavePngScaled(8);                      // 8x the current grid size (e.g., 13x13 -> 104x104)

[ContextMenu("Save PNG (x4 upscale, bilinear)")]
public void SavePngx4Bilinear() => SavePngScaled(4, true);

public void SavePngScaled(int scale, bool bilinear = true)
{
    if (tex == null || scale <= 1) { SavePngExact(tex.width, tex.height, bilinear); return; }
    SavePngExact(tex.width * scale, tex.height * scale, bilinear);
}

public void SavePngExact(int exportWidth, int exportHeight, bool bilinear = true)
{
    if (tex == null) return;

    // Early out if same size
    if (exportWidth <= 0 || exportHeight <= 0) { Debug.LogWarning("Export size must be > 0"); return; }

    // Create target texture
    var scaled = new Texture2D(exportWidth, exportHeight, TextureFormat.RGBA32, false, false);
    scaled.wrapMode = TextureWrapMode.Clamp;
    scaled.filterMode = FilterMode.Point; // we'll set pixels manually

    // Choose sampler
    Color Sample(float u, float v)
    {
        if (!bilinear)
        {
            // Nearest-neighbour (keeps crisp cell edges)
            int sx = Mathf.Clamp(Mathf.FloorToInt(u * tex.width), 0, tex.width - 1);
            int sy = Mathf.Clamp(Mathf.FloorToInt(v * tex.height), 0, tex.height - 1);
            return tex.GetPixel(sx, sy);
        }
        else
        {
            // Bilinear (smooth). Manual because EncodeToPNG ignores GPU filtering.
            float x = u * tex.width - 0.5f;
            float y = v * tex.height - 0.5f;
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = Mathf.Min(x0 + 1, tex.width - 1);
            int y1 = Mathf.Min(y0 + 1, tex.height - 1);
            x0 = Mathf.Clamp(x0, 0, tex.width - 1);
            y0 = Mathf.Clamp(y0, 0, tex.height - 1);
            float tx = Mathf.Clamp01(x - x0);
            float ty = Mathf.Clamp01(y - y0);

            Color c00 = tex.GetPixel(x0, y0);
            Color c10 = tex.GetPixel(x1, y0);
            Color c01 = tex.GetPixel(x0, y1);
            Color c11 = tex.GetPixel(x1, y1);

            Color cx0 = Color.Lerp(c00, c10, tx);
            Color cx1 = Color.Lerp(c01, c11, tx);
            return Color.Lerp(cx0, cx1, ty);
        }
    }

    // Resample
    for (int y = 0; y < exportHeight; y++)
    {
        float v = (y + 0.5f) / exportHeight;
        for (int x = 0; x < exportWidth; x++)
        {
            float u = (x + 0.5f) / exportWidth;
            scaled.SetPixel(x, y, Sample(u, v));
        }
    }
    scaled.Apply(false, false);

    // Save
    // Encode to PNG
    byte[] png = scaled.EncodeToPNG();

    // 🗂 Use different paths for Editor vs Build
    #if UNITY_EDITOR
        string dir = Path.Combine(Application.dataPath, "Heatmaps");
    #else
    string dir = Application.persistentDataPath;
    #endif

    // Ensure directory exists
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

    // Compose file name
        string file = System.IO.Path.Combine(
            dir,
            $"heatmap_{exportWidth}x{exportHeight}_{System.DateTime.UtcNow:yyyyMMdd_HHmmss}.png"
        );

    // Write file
        System.IO.File.WriteAllBytes(file, png);

    #if UNITY_EDITOR
        Debug.Log($"[HeatmapOverlay] Saved PNG to: {file}");
    #else
    Debug.Log($"[HeatmapOverlay] Heatmap saved to persistent data path: {file}");
    #endif


    Destroy(scaled);
}


    // --- Core ---

    void SampleTargets()
    {
        if (sampleTargets == null) return;
        for (int i = 0; i < sampleTargets.Length; i++)
        {
            var t = sampleTargets[i];
            if (t == null) continue;
            AddSample(t.position, sampleWeight);
        }
    }

    (int w, int h) CalcGridDims()
    {
        int w = Mathf.Max(1, Mathf.CeilToInt(sizeXZ.x / cellSize));
        int h = Mathf.Max(1, Mathf.CeilToInt(sizeXZ.y / cellSize));
        return (w, h);
    }

    void BuildGrid()
    {
        var dims = CalcGridDims();
        width = dims.w; height = dims.h;

        counts = new int[width * height];
        workA = new float[counts.Length];
        workB = new float[counts.Length];
        maxCount = 0;

        EnsureRenderObjects();
        AllocateTexture();
        PositionQuad();
        dirty = true;
    }

    void EnsureRenderObjects()
    {
        if (quad == null)
        {
            // Create a child quad to display the texture
            quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "HeatmapQuad";
            quad.transform.SetParent(transform, false);
            // Face up (rotate to XZ)
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // Remove collider
            var col = quad.GetComponent<Collider>();
            if (col) DestroyImmediate(col);
        }

        if (mat == null)
        {
            // Unlit transparent material
            var shader = Shader.Find("Unlit/Transparent");
            mat = new Material(shader);
            mat.name = "Heatmap_Mat";
        }
        
        if (Application.isPlaying && additiveBlend) {
            // Additive blend setup for Unlit/Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }


        var mr = quad.GetComponent<MeshRenderer>();
        if (mr.sharedMaterial != mat) mr.sharedMaterial = mat;

        PositionQuad();
        UpdateQuadVisibility();
    }

    void PositionQuad()
    {
        if (quad == null) return;
        // Center the quad over the area
        Vector3 center = new Vector3(originXZ.x + sizeXZ.x * 0.5f, 0.01f, originXZ.y + sizeXZ.y * 0.5f);
        quad.transform.position = center;
        quad.transform.localScale = new Vector3(sizeXZ.x, sizeXZ.y, 1f);
    }

    void AllocateTexture()
    {
        if (tex != null && (tex.width != width || tex.height != height))
        {
            DestroyImmediate(tex);
            tex = null;
        }
        if (tex == null)
        {
            tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
        }
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_Color", new Color(1f, 1f, 1f, opacity));
    }

void BakeTexture()
{
    if (tex == null || counts == null) return;

    // ---------------------------
    // 0) Choose normalization max
    // ---------------------------
    int normMax = fixedMaxCount > 0 ? fixedMaxCount : maxCount;
    float invMax = normMax > 0 ? 1f / normMax : 0f;

    // ---------------------------
    // 1) Linear normalize to 0..1
    // ---------------------------
    for (int i = 0; i < counts.Length; i++)
        workA[i] = counts[i] * invMax; // 0..1

    // ---------------------------
    // 2) Tone mapping (log or gamma)
    // ---------------------------
    if (useLogScale)
    {
        // Log compression boosts mid-range
        const float k = 8f; // tweak 4..12 to taste
        float denom = Mathf.Log(1f + k);
        for (int i = 0; i < workA.Length; i++)
        {
            float t = Mathf.Clamp01(workA[i]);
            workA[i] = Mathf.Log(1f + k * t) / denom; // 0..1
        }
    }
    else if (Mathf.Abs(gamma - 1f) > 0.001f)
    {
        // Gamma curve: t' = t^(1/gamma). gamma>1 lifts mids; <1 crushes mids.
        float invGamma = 1f / gamma;
        for (int i = 0; i < workA.Length; i++)
        {
            float t = Mathf.Clamp01(workA[i]);
            workA[i] = Mathf.Pow(t, invGamma); // 0..1
        }
    }

    // ---------------------------
    // 3) Optional separable blur
    // ---------------------------
    if (blurRadius > 0)
    {
        int r = blurRadius;
        int w = width, h = height;
        int windowSize = r * 2 + 1;

        // Horizontal pass: workA -> workB
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            float sum = 0f;
            int wsize = Mathf.Min(windowSize, w);

            // Prime initial window
            for (int x = 0; x < wsize; x++)
                sum += workA[row + x];

            for (int x = 0; x < w; x++)
            {
                int left = x - r - 1;
                int right = x + r;
                if (left >= 0) sum -= workA[row + left];
                if (right < w) sum += workA[row + right];
                workB[row + x] = sum / wsize;
            }
        }

        // Vertical pass: workB -> workA (final)
        for (int x = 0; x < width; x++)
        {
            float sum = 0f;
            int wsize = Mathf.Min(windowSize, height);

            // Prime initial window
            for (int y = 0; y < wsize; y++)
                sum += workB[y * width + x];

            for (int y = 0; y < height; y++)
            {
                int up = y - r - 1;
                int down = y + r;
                if (up >= 0) sum -= workB[up * width + x];
                if (down < height) sum += workB[down * width + x];
                workA[y * width + x] = sum / wsize;
            }
        }
    }

    // ---------------------------
    // 4) Write pixels with cutoff
    // ---------------------------
    Color clear = new Color(0f, 0f, 0f, 0f);
    for (int y = 0; y < height; y++)
    {
        int row = y * width;
        for (int x = 0; x < width; x++)
        {
            float t = Mathf.Clamp01(workA[row + x]);
            if (t < minVisibleT)
            {
                tex.SetPixel(x, y, clear); // fully transparent below threshold
            }
            else
            {
                Color c = gradient.Evaluate(t);
                c.a = opacity;            // control overlay opacity
                tex.SetPixel(x, y, c);
            }
        }
    }

    tex.Apply(false, false);
    dirty = false;
}


    void UpdateQuadVisibility()
    {
        if (quad)
        {
            var mr = quad.GetComponent<MeshRenderer>();
            if (mr) mr.enabled = visible;
        }
    }

    void CleanupRuntimeObjects()
    {
        if (quad) DestroyImmediate(quad);
        if (mat) DestroyImmediate(mat);
        if (tex) DestroyImmediate(tex);
        quad = null; mat = null; tex = null;
    }
    
    //--------------------
    
// ----- EDITOR GIZMO OUTLINE (robust) -----
    public bool showBoundsGizmo = true;
    public bool originIsCenter = false;   // set true if you want originXZ to be center
    public float gizmoYOffset = 0.05f;    // lift to avoid z-fighting
    public Color gizmoColor = new Color(0f, 1f, 1f, 0.95f);
    public bool debugProbeCube = true;    // draws a big cube at Transform to prove gizmos are on

    void OnDrawGizmos()
    {
        if (!showBoundsGizmo) return;

        // 1) Debug probe cube at this GameObject (helps confirm Gizmos are visible at all)
        if (debugProbeCube)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * gizmoYOffset, Vector3.one * 2f);
        }

        // 2) Our heatmap bounds on XZ
        Gizmos.color = gizmoColor;

        Vector3 center;
        if (originIsCenter)
        {
            center = new Vector3(originXZ.x, gizmoYOffset, originXZ.y);
        }
        else
        {
            // originXZ is bottom-left; convert to center
            center = new Vector3(originXZ.x + sizeXZ.x * 0.5f, gizmoYOffset, originXZ.y + sizeXZ.y * 0.5f);
        }

        Vector3 size = new Vector3(Mathf.Max(0.001f, sizeXZ.x), 0.001f, Mathf.Max(0.001f, sizeXZ.y));
        Gizmos.DrawWireCube(center, size);

        // Small cross at the *logical* origin for clarity
        Vector3 originPoint = originIsCenter
            ? center
            : new Vector3(originXZ.x, gizmoYOffset, originXZ.y);

        float s = 0.4f;
        Gizmos.DrawLine(originPoint + new Vector3(-s, 0, 0), originPoint + new Vector3(s, 0, 0));
        Gizmos.DrawLine(originPoint + new Vector3(0, 0, -s), originPoint + new Vector3(0, 0, s));
    }



    
}


