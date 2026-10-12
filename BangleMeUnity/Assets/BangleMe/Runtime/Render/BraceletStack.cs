using System;
using System.Collections.Generic;
using UnityEngine;

namespace BangleMe
{
    // The bracelets worn on one arm, in order from the wrist toward the elbow.
    public sealed class BraceletStack : MonoBehaviour
    {
        public const int MaxCount = 50;
        public const float MinLooseness = 0.9f;
        public const float MaxLooseness = 1.3f;

        const int HighDetailLimit = 8;
        const int OccluderSegments = 28;
        const int OccluderRings = 12;
        const float OccluderMargin = 0.03f;
        // The stand-in also covers the heel of the hand, which flares out from the wrist.
        const float HandSideLength = 0.05f;
        const float HandFlarePerMeter = 9f;
        // Past this the arm is the elbow; bracelets that would sit beyond it are not drawn.
        const float MaxArmLength = 0.26f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int StackStartId = Shader.PropertyToID("_StackStart");
        static readonly int StackEndId = Shader.PropertyToID("_StackEnd");

        [Tooltip("One material per entry of BraceletDesign.Catalog.")]
        [SerializeField] Material[] designMaterials;
        [SerializeField] Material occluderMaterial;
        [SerializeField] int initialDesign;
        [SerializeField, Range(MinLooseness, MaxLooseness)] float looseness = 1f;
        [SerializeField] bool occludeBehindWrist = true;

        [Header("Placement (meters)")]
        [Tooltip("How far up the forearm from the wrist joint the first bracelet sits.")]
        [SerializeField] float wristOffset = 0.026f;
        [SerializeField] float gapBetweenBracelets = 0.0006f;

        readonly List<int> designs = new List<int>();
        readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
        readonly Dictionary<int, Mesh> meshCache = new Dictionary<int, Mesh>();
        MaterialPropertyBlock finishBlock;
        MaterialPropertyBlock occluderBlock;
        MeshRenderer occluder;
        Mesh occluderMesh;
        Vector3[] occluderVertices;
        Vector3[] occluderNormals;
        int finishIndex;
        bool dirty = true;

        public event Action Changed;

        public int Count => designs.Count;
        public int LastDesign => designs.Count > 0 ? designs[designs.Count - 1] : initialDesign;

        public int FinishIndex
        {
            get => finishIndex;
            set
            {
                finishIndex = Mathf.Clamp(value, 0, BraceletFinish.Catalog.Length - 1);
                MarkChanged();
            }
        }

        // 1 hugs the wrist; larger values leave room like a loose bangle.
        public float Looseness
        {
            get => looseness;
            set => looseness = Mathf.Clamp(value, MinLooseness, MaxLooseness);
        }

        void Awake()
        {
            designs.Add(Mathf.Clamp(initialDesign, 0, BraceletDesign.Catalog.Length - 1));
        }

        public void Add(int designIndex)
        {
            if (designs.Count >= MaxCount) return;
            designs.Add(Mathf.Clamp(designIndex, 0, BraceletDesign.Catalog.Length - 1));
            MarkChanged();
        }

        public void RemoveLast()
        {
            if (designs.Count == 0) return;
            designs.RemoveAt(designs.Count - 1);
            MarkChanged();
        }

        public void Clear()
        {
            designs.Clear();
            MarkChanged();
        }

        public void Apply(WristFit fit, bool visible)
        {
            if (dirty) Rebuild();

            transform.localPosition = fit.Position;
            transform.localRotation = fit.Rotation;

            var distance = wristOffset;
            for (var i = 0; i < designs.Count; i++)
            {
                var width = BraceletDesign.Catalog[designs[i]].Width;
                var center = distance + width * 0.5f;
                distance += width + gapBetweenBracelets;

                var ring = renderers[i];
                ring.enabled = visible && distance < MaxArmLength;
                var radial = fit.HalfWidthAt(center) * looseness / BraceletMeshFactory.ReferenceInnerRadius;
                ring.transform.localPosition = Vector3.up * center;
                ring.transform.localScale = new Vector3(radial, 1f, radial);
            }

            var stackEnd = Mathf.Min(distance, MaxArmLength);
            occluder.enabled = visible && occludeBehindWrist && designs.Count > 0;
            if (occluder.enabled) UpdateOccluder(fit, stackEnd);
        }

        void Rebuild()
        {
            dirty = false;
            finishBlock ??= new MaterialPropertyBlock();
            if (occluder == null) CreateOccluder();

            while (renderers.Count < designs.Count)
            {
                renderers.Add(CreateChild($"Bracelet {renderers.Count + 1}"));
            }

            var highDetail = designs.Count <= HighDetailLimit;
            var finish = BraceletFinish.Catalog[finishIndex];
            for (var i = 0; i < renderers.Count; i++)
            {
                var ring = renderers[i];
                if (i >= designs.Count)
                {
                    ring.enabled = false;
                    continue;
                }

                var design = BraceletDesign.Catalog[designs[i]];
                ring.GetComponent<MeshFilter>().sharedMesh = MeshFor(designs[i], highDetail);
                ring.sharedMaterial = designMaterials[designs[i]];
                finishBlock.SetColor(BaseColorId, finish.Color);
                finishBlock.SetFloat(SmoothnessId, design.Smoothness * finish.SmoothnessScale);
                ring.SetPropertyBlock(finishBlock);
            }
        }

        Mesh MeshFor(int designIndex, bool highDetail)
        {
            var key = designIndex * 2 + (highDetail ? 1 : 0);
            if (!meshCache.TryGetValue(key, out var mesh))
            {
                mesh = BraceletMeshFactory.Create(BraceletDesign.Catalog[designIndex], highDetail);
                meshCache[key] = mesh;
            }
            return mesh;
        }

        void CreateOccluder()
        {
            occluder = CreateChild("Arm Occluder");
            occluder.sharedMaterial = occluderMaterial;
            occluderBlock = new MaterialPropertyBlock();

            var columns = OccluderSegments + 1;
            occluderVertices = new Vector3[columns * (OccluderRings + 1)];
            occluderNormals = new Vector3[occluderVertices.Length];
            var triangles = new List<int>(OccluderSegments * OccluderRings * 6);
            BraceletMeshFactory.AddGrid(triangles, 0, OccluderSegments, OccluderRings, OccluderRings + 1);

            occluderMesh = new Mesh { name = "Arm Occluder" };
            occluderMesh.MarkDynamic();
            occluderMesh.SetVertices(occluderVertices);
            occluderMesh.SetTriangles(triangles, 0);
            occluder.GetComponent<MeshFilter>().sharedMesh = occluderMesh;
        }

        // Stands in for the arm: hides the far half of each bracelet and carries the contact
        // shadow, so it has to follow the measured arm rather than a fixed shape.
        void UpdateOccluder(WristFit fit, float stackEnd)
        {
            var start = -HandSideLength;
            var end = stackEnd + OccluderMargin;
            for (var i = 0; i <= OccluderSegments; i++)
            {
                var angle = i / (float)OccluderSegments * Mathf.PI * 2f;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);
                for (var j = 0; j <= OccluderRings; j++)
                {
                    var y = Mathf.Lerp(start, end, j / (float)OccluderRings);
                    var halfWidth = fit.HalfWidthAt(y) * (1f + HandFlarePerMeter * Mathf.Max(0f, wristOffset * 0.5f - y));
                    var halfThickness = halfWidth * fit.HalfThickness / fit.HalfWidth;
                    var index = i * (OccluderRings + 1) + j;
                    occluderVertices[index] = new Vector3(cos * halfWidth, y, sin * halfThickness);
                    occluderNormals[index] = new Vector3(cos * halfThickness, 0f, sin * halfWidth).normalized;
                }
            }

            occluderMesh.SetVertices(occluderVertices);
            occluderMesh.SetNormals(occluderNormals);
            occluderMesh.RecalculateBounds();

            occluderBlock.SetFloat(StackStartId, wristOffset);
            occluderBlock.SetFloat(StackEndId, stackEnd);
            occluder.SetPropertyBlock(occluderBlock);
        }

        MeshRenderer CreateChild(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>();
            var meshRenderer = child.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
            return meshRenderer;
        }

        void MarkChanged()
        {
            dirty = true;
            Changed?.Invoke();
        }

        void OnDestroy()
        {
            foreach (var mesh in meshCache.Values) Destroy(mesh);
            if (occluderMesh != null) Destroy(occluderMesh);
        }
    }
}
