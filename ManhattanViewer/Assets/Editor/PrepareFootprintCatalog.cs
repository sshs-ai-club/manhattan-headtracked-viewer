// Disposable cloud-test setup only. Do not commit this helper or generated catalog.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Manhattan.Viewer;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Manhattan.TestFixtureSetup
{
    public static class PrepareFootprintCatalog
    {
        private const string AssetPath = "Assets/Resources/CityCatalog.asset";
        private const string Digest = "9bd3a981f30e6efcc783325389211219a47d47730bfb50d3d83469ebd548d091";
        public static void Run()
        {
            if (Environment.GetEnvironmentVariable("MANHATTAN_DISPOSABLE_FOOTPRINT_TEST") != "1")
                throw new InvalidOperationException("Only run in an explicitly disposable test checkout.");
            if (AssetDatabase.LoadAssetAtPath<CityCatalog>(AssetPath) != null || File.Exists(AssetPath) || File.Exists(AssetPath + ".meta"))
                throw new InvalidOperationException("Refusing to overwrite an existing catalog or metadata.");
            string source = Environment.GetEnvironmentVariable("MANHATTAN_FOOTPRINT_TEST_SOURCE");
            if (string.IsNullOrEmpty(source)) throw new InvalidOperationException("Set MANHATTAN_FOOTPRINT_TEST_SOURCE to the verified buildings_metric.json.");
            byte[] bytes = File.ReadAllBytes(source);
            if (bytes.LongLength != 19186837) throw new InvalidDataException("Unexpected canonical source size.");
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() != Digest)
                    throw new InvalidDataException("Canonical source SHA-256 mismatch.");
            JArray rows = JArray.Parse(System.Text.Encoding.UTF8.GetString(bytes));
            if (rows.Count != 44567) throw new InvalidDataException("Canonical source row count changed.");
            var result = new List<BuildingRecord>();
            foreach (JObject row in rows)
            {
                var ring = (JArray)row["rings"][0];
                Vector2[] polygon = ring.Select(p => new Vector2(-(float)p[0], -(float)p[1])).ToArray();
                if (polygon.Length > 1 && polygon[0] == polygon[polygon.Length - 1]) polygon = polygon.Take(polygon.Length - 1).ToArray();
                if (polygon.Length < 3) continue;
                float signedArea=0;for(int i=0;i<polygon.Length;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Length];signedArea+=a.x*b.y-b.x*a.y;}if(signedArea<0)Array.Reverse(polygon);
                float height = (float)row["height_m"];
                if (!float.IsFinite(height) || height <= .22f) throw new InvalidDataException("Invalid height for building " + row["id"]);
                Bounds bounds = new Bounds(new Vector3(polygon[0].x, height * .5f, polygon[0].y), Vector3.zero);
                foreach (Vector2 p in polygon) { bounds.Encapsulate(new Vector3(p.x, .22f, p.y)); bounds.Encapsulate(new Vector3(p.x, height, p.y)); }
                result.Add(new BuildingRecord { id = (string)row["id"], footprint = polygon, height = height, groundHeight = .22f, cityBounds = bounds });
            }
            if (result.Count != 44567 || result.Select(b => b.id).Distinct().Count() != result.Count)
                throw new InvalidDataException("Unexpected converted building count or duplicate IDs.");
            var catalog = ScriptableObject.CreateInstance<CityCatalog>();
            catalog.name = "CityCatalog";
            catalog.sourceVersion = "manhattan-2026-10-10-v2";
            catalog.buildings = result.ToArray();
            catalog.initialEyePosition = new Vector3(1225f, 1.85f, 3585f);
            catalog.initialEulerAngles = Quaternion.LookRotation(new Vector3(1180f, 20f, 3502f) - catalog.initialEyePosition).eulerAngles;
            // Empty tiles are intentional: these tests consume only original building records.
            // This catalog is NOT a playable/rendered full-city build artifact.
            Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.Refresh();
            AssetDatabase.CreateAsset(catalog, AssetPath);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var loaded = Resources.Load<CityCatalog>("CityCatalog");
            if (loaded == null || loaded.buildings.Length != result.Count || loaded.tiles.Length != 0)
                throw new InvalidDataException("Persisted footprint catalog failed validation.");
            for (int i = 0; i < result.Count; i++)
            {
                var a = result[i]; var b = loaded.buildings[i];
                if (a.id != b.id || a.height != b.height || a.groundHeight != b.groundHeight ||
                    a.cityBounds.center != b.cityBounds.center || a.cityBounds.extents != b.cityBounds.extents ||
                    !a.footprint.SequenceEqual(b.footprint))
                    throw new InvalidDataException("Persisted record differs at row " + i);
            }
            Debug.Log("MANHATTAN_FOOTPRINT_TEST_FIXTURE_READY rows=" + loaded.buildings.Length + " sha256=" + Digest + " geometry=canonical-building-footprints tiles=0");
        }
    }
}
