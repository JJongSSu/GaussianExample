// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Loads and manages reference images and their COLMAP camera poses for localization.
/// Reference data is stored in StreamingAssets/LocalizationDB/.
/// </summary>
public class ColmapReferenceDatabase : MonoBehaviour
{
    [Header("Database")]
    [Tooltip("Subfolder name under StreamingAssets")]
    public string databaseFolder = "LocalizationDB";

    [Header("Data")]
    public List<ReferenceFrame> referenceFrames = new List<ReferenceFrame>();

    [Serializable]
    public class ReferenceFrame
    {
        public string imageName;
        public Texture2D image;
        public Vector3 position;        // Unity coordinate space (already converted)
        public Quaternion rotation;     // Unity coordinate space (already converted)
        public float fov;
        public int width;
        public int height;
    }

    /// <summary>
    /// Load reference frames from a cameras.json file (same format as GSCameraLoader).
    /// Images are loaded from StreamingAssets/LocalizationDB/.
    /// </summary>
    public void LoadFromCamerasJson(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"[ColmapRefDB] JSON not found: {jsonPath}");
            return;
        }

        string jsonContent = File.ReadAllText(jsonPath);
        referenceFrames.Clear();

        // Parse using same regex pattern as GSCameraLoader
        string pattern = @"\{""id"":\s*(\d+),\s*""img_name"":\s*""([^""]+)"".*?""width"":\s*(\d+),\s*""height"":\s*(\d+).*?""position"":\s*\[([^\]]+)\],\s*""rotation"":\s*\[\[([^\]]+)\],\s*\[([^\]]+)\],\s*\[([^\]]+)\]\].*?""fy"":\s*([0-9\.]+).*?\}";

        foreach (Match match in Regex.Matches(jsonContent, pattern, RegexOptions.Singleline))
        {
            try
            {
                var frame = new ReferenceFrame();
                frame.imageName = match.Groups[2].Value;
                frame.width = int.Parse(match.Groups[3].Value);
                frame.height = int.Parse(match.Groups[4].Value);

                // Parse position (COLMAP space)
                string[] posStrs = match.Groups[5].Value.Split(',');
                Vector3 colmapPos = new Vector3(
                    float.Parse(posStrs[0].Trim()),
                    float.Parse(posStrs[1].Trim()),
                    float.Parse(posStrs[2].Trim())
                );

                // Parse rotation matrix (COLMAP World-to-Camera)
                string[] r0 = match.Groups[6].Value.Split(',');
                string[] r1 = match.Groups[7].Value.Split(',');
                string[] r2 = match.Groups[8].Value.Split(',');

                Matrix4x4 rotW2C = Matrix4x4.identity;
                rotW2C.m00 = float.Parse(r0[0].Trim()); rotW2C.m01 = float.Parse(r0[1].Trim()); rotW2C.m02 = float.Parse(r0[2].Trim());
                rotW2C.m10 = float.Parse(r1[0].Trim()); rotW2C.m11 = float.Parse(r1[1].Trim()); rotW2C.m12 = float.Parse(r1[2].Trim());
                rotW2C.m20 = float.Parse(r2[0].Trim()); rotW2C.m21 = float.Parse(r2[1].Trim()); rotW2C.m22 = float.Parse(r2[2].Trim());

                // Convert COLMAP → Unity coordinate system
                // COLMAP: RHS, Y-down, Z-forward
                // Unity: LHS, Y-up, Z-forward
                // Position: (x, -y, z)
                frame.position = new Vector3(colmapPos.x, -colmapPos.y, colmapPos.z);

                // Rotation: transpose W2C to get C2W, then fix Y axis
                Matrix4x4 camToWorld = rotW2C.transpose;
                Vector3 right = new Vector3(camToWorld.m00, camToWorld.m10, camToWorld.m20);
                Vector3 up = new Vector3(camToWorld.m01, camToWorld.m11, camToWorld.m21);
                Vector3 fwd = new Vector3(camToWorld.m02, camToWorld.m12, camToWorld.m22);

                // Flip Y components for coordinate system conversion
                right.y = -right.y;
                up.y = -up.y;
                fwd.y = -fwd.y;

                frame.rotation = Quaternion.LookRotation(fwd, -up);

                // FOV from fy
                float fy = float.Parse(match.Groups[9].Value.Trim());
                frame.fov = 2f * Mathf.Atan(frame.height / (2f * fy)) * Mathf.Rad2Deg;

                referenceFrames.Add(frame);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ColmapRefDB] Error parsing frame: {e.Message}");
            }
        }

        Debug.Log($"[ColmapRefDB] Loaded {referenceFrames.Count} reference frames.");
    }

    /// <summary>
    /// Load reference frame images from StreamingAssets.
    /// Call after LoadFromCamerasJson.
    /// </summary>
    public void LoadReferenceImages()
    {
        string dbPath = Path.Combine(Application.streamingAssetsPath, databaseFolder);

        foreach (var frame in referenceFrames)
        {
            string imgPath = Path.Combine(dbPath, frame.imageName);
            if (File.Exists(imgPath))
            {
                byte[] imgData = File.ReadAllBytes(imgPath);
                frame.image = new Texture2D(2, 2);
                frame.image.LoadImage(imgData);
            }
        }
    }

    /// <summary>
    /// Find the reference frame closest to a given position.
    /// </summary>
    public ReferenceFrame FindClosestFrame(Vector3 position)
    {
        ReferenceFrame closest = null;
        float minDist = float.MaxValue;

        foreach (var frame in referenceFrames)
        {
            float dist = Vector3.Distance(position, frame.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = frame;
            }
        }

        return closest;
    }

    /// <summary>
    /// Get a reference frame by index.
    /// </summary>
    public ReferenceFrame GetFrame(int index)
    {
        if (index >= 0 && index < referenceFrames.Count)
            return referenceFrames[index];
        return null;
    }

    void OnDestroy()
    {
        foreach (var frame in referenceFrames)
        {
            if (frame.image != null)
                Destroy(frame.image);
        }
    }
}
