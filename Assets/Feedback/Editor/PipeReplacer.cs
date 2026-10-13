using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor tool: replaces the LOOK of old straight pipes with the modular straight pipe prefab.
/// For every selected old pipe it measures the pipe's direction, length and thickness from its mesh,
/// places straight pipe pieces end to end along it, and switches off the old Mesh Renderer.
/// The old object (collider, tag, layer) is left untouched, so the level plays exactly the same.
///
/// This file MUST live in a folder called "Editor" (for example Assets/Feedback/Editor).
/// Open it from the menu: Tools > Pipes > Pipe Replacer.
/// </summary>
public class PipeReplacer : EditorWindow
{
    GameObject straightPrefab;
    Transform newParent;
    bool autoThickness = true;
    float flangeFactor = 1.4f;          // flange radius of the new pipe compared with the old pipe's radius
    float manualScaleMultiplier = 1f;
    bool hideOldRenderers = true;
    bool fitLength = true;              // stretch/squash the pieces so the run matches the old pipe's length

    [MenuItem("Tools/Pipes/Pipe Replacer")]
    static void Open()
    {
        GetWindow<PipeReplacer>("Pipe Replacer");
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1. Drag a straight pipe prefab into the first slot.\n" +
            "2. Select the OLD pipes in the Hierarchy.\n" +
            "3. Click Replace. Use Ctrl+Z to undo if it looks wrong.",
            MessageType.Info);

        straightPrefab = (GameObject)EditorGUILayout.ObjectField("Straight pipe prefab", straightPrefab, typeof(GameObject), false);
        newParent = (Transform)EditorGUILayout.ObjectField("Parent for new pipes", newParent, typeof(Transform), true);

        autoThickness = EditorGUILayout.Toggle("Match old pipe thickness", autoThickness);
        if (autoThickness)
            flangeFactor = EditorGUILayout.FloatField("Flange / old pipe radius", flangeFactor);
        else
            manualScaleMultiplier = EditorGUILayout.FloatField("Scale multiplier", manualScaleMultiplier);

        fitLength = EditorGUILayout.Toggle("Fit length to old pipe", fitLength);
        hideOldRenderers = EditorGUILayout.Toggle("Hide old Mesh Renderer", hideOldRenderers);

        GUI.enabled = straightPrefab != null && Selection.gameObjects.Length > 0;
        if (GUILayout.Button("Replace selected pipes"))
            Replace();
        GUI.enabled = true;
    }

    void Replace()
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Replace pipes");

        GameObject[] selection = Selection.gameObjects;

        if (newParent == null)
        {
            GameObject found = GameObject.Find("Pipe System (New)");
            if (found == null)
            {
                found = new GameObject("Pipe System (New)");
                Undo.RegisterCreatedObjectUndo(found, "Create pipe parent");
            }
            newParent = found.transform;
        }

        // ---- Measure the new pipe prefab once, at its own scale ----
        GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(straightPrefab);
        probe.transform.position = Vector3.zero;

        MeshFilter probeMesh = probe.GetComponentInChildren<MeshFilter>();
        if (probeMesh == null || probeMesh.sharedMesh == null)
        {
            Debug.LogError("PipeReplacer: the prefab has no mesh.");
            DestroyImmediate(probe);
            return;
        }

        Analyze(probeMesh, out Vector3 pCentroid, out Vector3 pAxis, out float pMin, out float pMax, out float pRadius);
        Vector3 probeCenter = pCentroid + pAxis * ((pMin + pMax) * 0.5f);   // geometric centre, pivot is at the origin
        float baseLength = pMax - pMin;
        Quaternion baseRotation = probe.transform.rotation;
        Vector3 baseScale = probe.transform.localScale;

        // Which of the prefab's own X/Y/Z axes runs along the pipe? That is the one we stretch to fit.
        Vector3 localAxis = probe.transform.InverseTransformDirection(pAxis);
        int axisIndex = 0;
        if (Mathf.Abs(localAxis.y) > Mathf.Abs(localAxis[axisIndex])) axisIndex = 1;
        if (Mathf.Abs(localAxis.z) > Mathf.Abs(localAxis[axisIndex])) axisIndex = 2;

        DestroyImmediate(probe);

        if (baseLength < 0.0001f || pRadius < 0.0001f)
        {
            Debug.LogError("PipeReplacer: could not measure the prefab.");
            return;
        }

        // ---- Replace each selected old pipe ----
        int replaced = 0;
        int piecesPlaced = 0;

        foreach (GameObject old in selection)
        {
            MeshFilter mf = old.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogWarning($"PipeReplacer: skipped '{old.name}' (no mesh).");
                continue;
            }

            Analyze(mf, out Vector3 centroid, out Vector3 axis, out float tMin, out float tMax, out float radius);
            float length = tMax - tMin;
            Vector3 center = centroid + axis * ((tMin + tMax) * 0.5f);

            float m = autoThickness ? (radius * flangeFactor) / pRadius : manualScaleMultiplier;
            float pieceLength = baseLength * m;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / pieceLength));

            // Stretch (or squash) every piece a little along the pipe so the run fits the old pipe exactly.
            float stretch = fitLength ? length / (count * pieceLength) : 1f;
            float slot = pieceLength * stretch;
            Vector3 pieceScale = baseScale * m;
            pieceScale[axisIndex] *= stretch;
            Vector3 pivotToCenter = probeCenter + pAxis * (Vector3.Dot(probeCenter, pAxis) * (stretch - 1f));

            Quaternion align = Quaternion.FromToRotation(pAxis, axis);

            for (int i = 0; i < count; i++)
            {
                float along = (i - (count - 1) * 0.5f) * slot;
                Vector3 desiredCenter = center + axis * along;

                GameObject piece = (GameObject)PrefabUtility.InstantiatePrefab(straightPrefab);
                Undo.RegisterCreatedObjectUndo(piece, "Place pipe");
                piece.transform.SetParent(newParent, false);
                piece.transform.localScale = pieceScale;
                piece.transform.rotation = align * baseRotation;
                piece.transform.position = desiredCenter - align * (pivotToCenter * m);
                piece.name = $"{straightPrefab.name}_{old.name}_{i}";
                piecesPlaced++;
            }

            if (hideOldRenderers)
            {
                MeshRenderer r = old.GetComponent<MeshRenderer>();
                if (r != null)
                {
                    Undo.RecordObject(r, "Hide old pipe");
                    r.enabled = false;
                }
            }

            Debug.Log($"PipeReplacer: '{old.name}' length {length:F2}, radius {radius:F3}, scale x{m:F2}, stretch x{stretch:F2}, {count} piece(s).");
            replaced++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(newParent.gameObject.scene);
        Debug.Log($"PipeReplacer: replaced {replaced} pipe(s) with {piecesPlaced} piece(s).");
    }

    /// <summary>
    /// Finds a mesh's main direction (axis), its extent along that axis, and its radius around it.
    /// Works for a cylinder at any angle, because it uses the shape of the vertices and ignores
    /// how the object itself is rotated.
    /// </summary>
    static void Analyze(MeshFilter mf, out Vector3 centroid, out Vector3 axis,
                        out float tMin, out float tMax, out float radius)
    {
        Vector3[] local = mf.sharedMesh.vertices;
        Vector3[] w = new Vector3[local.Length];
        centroid = Vector3.zero;

        for (int i = 0; i < local.Length; i++)
        {
            w[i] = mf.transform.TransformPoint(local[i]);
            centroid += w[i];
        }
        centroid /= Mathf.Max(1, w.Length);

        // Covariance matrix of the vertices.
        float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
        for (int i = 0; i < w.Length; i++)
        {
            Vector3 d = w[i] - centroid;
            xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
            yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
        }

        // Power iteration finds the direction the vertices are spread out along the most.
        Vector3 a = new Vector3(0.577f, 0.578f, 0.579f).normalized;
        for (int it = 0; it < 64; it++)
        {
            Vector3 n = new Vector3(
                xx * a.x + xy * a.y + xz * a.z,
                xy * a.x + yy * a.y + yz * a.z,
                xz * a.x + yz * a.y + zz * a.z);
            if (n.sqrMagnitude < 1e-12f) break;
            a = n.normalized;
        }
        axis = a;

        tMin = float.MaxValue;
        tMax = float.MinValue;
        radius = 0f;
        for (int i = 0; i < w.Length; i++)
        {
            Vector3 d = w[i] - centroid;
            float t = Vector3.Dot(d, axis);
            float perp = (d - axis * t).magnitude;
            if (t < tMin) tMin = t;
            if (t > tMax) tMax = t;
            if (perp > radius) radius = perp;
        }
    }
}