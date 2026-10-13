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
        // The first bangles stand off the skin, so seen at an angle their shadow starts a little
        // way in from where they do.
        const float ShadowInset = 0.006f;
        // Bangles worn together are close to one size, so on an arm that widens they cannot all
        // hug it: the size is set by the widest place they reach, and they sit loose before it.
        const float SharedSizeShare = 0.45f;
        const float GravityAcceleration = 9.81f;
        // A frame that took longer than this is a stall, not motion to catch up on.
        const float MaxStepSeconds = 0.1f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int StackStartId = Shader.PropertyToID("_StackStart");
        static readonly int StackEndId = Shader.PropertyToID("_StackEnd");
        static readonly int LightAlongId = Shader.PropertyToID("_LightAlong");

        [Tooltip("One material per entry of BraceletDesign.Catalog.")]
        [SerializeField] Material[] designMaterials;
        [SerializeField] Material occluderMaterial;
        [SerializeField] int initialDesign;
        [SerializeField, Range(MinLooseness, MaxLooseness)] float looseness = 1f;
        [SerializeField] bool occludeBehindWrist = true;

        [Header("Placement (meters)")]
        [Tooltip("How far up the forearm from the wrist joint the first bracelet sits.")]
        [SerializeField] float wristOffset = 0.026f;
        [SerializeField] float gapBetweenBracelets = 0.0002f;

        readonly List<int> designs = new List<int>();
        readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
        readonly Dictionary<int, Mesh> meshCache = new Dictionary<int, Mesh>();
        BangleSlideSolver slide;
        float[] homes = Array.Empty<float>();
        float[] widths = Array.Empty<float>();
        float[] radii = Array.Empty<float>();
        float[] lowest = Array.Empty<float>();
        float[] highest = Array.Empty<float>();
        MaterialPropertyBlock finishBlock;
        MaterialPropertyBlock occluderBlock;
        MeshRenderer occluder;
        Mesh occluderMesh;
        Vector3[] occluderVertices;
        Vector3[] occluderNormals;
        int finishIndex;
        bool dirty = true;

        public event Action Changed;

        public Color FinishColor => BraceletFinish.Catalog[finishIndex].Color;

        // Toward the main light, in the space of this object's parent (the camera).
        public Vector3 LightDirection { get; set; } = Vector3.up;

        // Which way things fall, in the space of this object's parent (the camera).
        public Vector3 Gravity { get; set; } = Vector3.down;

        public int Count => designs.Count;

        // 0 for a bangle on its own, 1 once it has a neighbour on either side.
        public float Crowding => Mathf.Clamp01((designs.Count - 1) * 0.5f);
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

        public void Add(int designIndex, int count = 1)
        {
            count = Mathf.Min(count, MaxCount - designs.Count);
            if (count <= 0) return;
            var design = Mathf.Clamp(designIndex, 0, BraceletDesign.Catalog.Length - 1);
            for (var i = 0; i < count; i++) designs.Add(design);
            MarkChanged();
        }

        // A set is what one buys together: plain and beaded thin bangles mixed, or more of the
        // same for every other design.
        public void AddSet(int count)
        {
            count = Mathf.Min(count, MaxCount - designs.Count);
            if (count <= 0) return;
            var last = LastDesign;
            var mixed = BraceletDesign.IsThinSet(last);
            for (var i = 0; i < count; i++) designs.Add(mixed ? BraceletDesign.ThinSetDesign(designs.Count) : last);
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

        // seconds: how much time the bangles get to slide; the last frame's when left out.
        public void Apply(WristFit fit, bool visible, float seconds = -1f)
        {
            if (dirty) Rebuild();

            transform.localPosition = fit.Position;
            transform.localRotation = fit.Rotation;

            var gravity = Quaternion.Inverse(fit.Rotation) * Gravity.normalized;
            var thicknessRatio = fit.HalfThickness / fit.HalfWidth;

            Measure(fit);
            if (seconds < 0f) seconds = Application.isPlaying ? Mathf.Min(Time.deltaTime, MaxStepSeconds) : 0f;
            var press = new Vector2(gravity.x, gravity.z).magnitude * GravityAcceleration;
            slide.Step(seconds, widths, lowest, highest, gravity.y * GravityAcceleration, press);

            var stackStart = wristOffset;
            var stackEnd = wristOffset;
            for (var i = 0; i < designs.Count; i++)
            {
                var design = BraceletDesign.Catalog[designs[i]];
                var center = slide.Along(i);
                if (i == 0) stackStart = center - design.Width * 0.5f;
                stackEnd = center + design.Width * 0.5f;

                var ring = renderers[i];
                ring.enabled = visible && stackEnd < MaxArmLength;
                var jitter = StackJitter.At(i, design.Freedom);
                var armHalfWidth = HandHalfWidthAt(fit, center);
                var seat = BangleSeat.At(radii[i], armHalfWidth, armHalfWidth * thicknessRatio, gravity);
                var radial = radii[i] / BraceletMeshFactory.ReferenceInnerRadius;
                ring.transform.localPosition = new Vector3(jitter.ShiftMeters.x + seat.Hang.x, center, jitter.ShiftMeters.y + seat.Hang.y);
                ring.transform.localRotation = Quaternion.Euler(jitter.TiltDegrees.x * seat.TiltScale, 0f, jitter.TiltDegrees.y * seat.TiltScale)
                                             * Quaternion.AngleAxis(jitter.SpinDegrees, Vector3.up);
                ring.transform.localScale = new Vector3(radial, 1f, radial);
            }

            occluder.enabled = visible && occludeBehindWrist && designs.Count > 0;
            if (occluder.enabled) UpdateOccluder(fit, stackStart, Mathf.Min(stackEnd, MaxArmLength));
        }

        // Each bangle's size, and from that how far it can travel: toward the hand until the heel
        // of the hand is as wide as it is, toward the elbow until the arm is.
        void Measure(WristFit fit)
        {
            var count = designs.Count;
            if (homes.Length != count)
            {
                homes = new float[count];
                widths = new float[count];
                radii = new float[count];
                lowest = new float[count];
                highest = new float[count];
            }

            var distance = wristOffset;
            for (var i = 0; i < count; i++)
            {
                widths[i] = BraceletDesign.Catalog[designs[i]].Width;
                homes[i] = distance + widths[i] * 0.5f;
                distance += widths[i] + gapBetweenBracelets;
            }
            var widestHalfWidth = fit.HalfWidthAt(Mathf.Min(distance, MaxArmLength));

            for (var i = 0; i < count; i++)
            {
                var jitter = StackJitter.At(i, BraceletDesign.Catalog[designs[i]].Freedom);
                radii[i] = Mathf.Lerp(fit.HalfWidthAt(homes[i]), widestHalfWidth, SharedSizeShare) * looseness * jitter.RadiusScale;

                var room = Mathf.Max(0f, radii[i] / fit.HalfWidth - 1f);
                lowest[i] = Mathf.Min(homes[i], wristOffset * 0.5f - room / HandFlarePerMeter);
                highest[i] = fit.GrowthPerMeter > 1e-4f
                    ? Mathf.Max(homes[i], WristMeasurement.ReferenceDistanceMeters + room / fit.GrowthPerMeter)
                    : MaxArmLength;
            }

            slide ??= new BangleSlideSolver(gapBetweenBracelets);
            if (slide.Count != count) slide.Resize(homes);
        }

        // The arm's half width with the heel of the hand, which flares out from the wrist.
        float HandHalfWidthAt(WristFit fit, float distanceFromWrist)
        {
            return fit.HalfWidthAt(distanceFromWrist) * (1f + HandFlarePerMeter * Mathf.Max(0f, wristOffset * 0.5f - distanceFromWrist));
        }

        void Rebuild()
        {
            dirty = false;
            finishBlock ??= new MaterialPropertyBlock();
            if (occluder == null) CreateOccluder();

            while (renderers.Count < designs.Count)
            {
                renderers.Add(CreateChild($"Bracelet {renderers.Count + 1}", RenderLayers.Bracelets));
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

                var shown = finish.Plain ? BraceletDesign.PlainOf(designs[i]) : designs[i];
                var design = BraceletDesign.Catalog[shown];
                ring.GetComponent<MeshFilter>().sharedMesh = MeshFor(shown, highDetail);
                ring.sharedMaterial = designMaterials[shown];
                var jitter = StackJitter.At(i);
                finishBlock.SetColor(BaseColorId, finish.Color * jitter.Tone);
                finishBlock.SetFloat(SmoothnessId, design.Smoothness * finish.SmoothnessScale * jitter.Polish);
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
            occluder = CreateChild("Arm Occluder", RenderLayers.ArmOccluder);
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
        void UpdateOccluder(WristFit fit, float stackStart, float stackEnd)
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
                    var halfWidth = HandHalfWidthAt(fit, y);
                    var halfThickness = halfWidth * fit.HalfThickness / fit.HalfWidth;
                    var index = i * (OccluderRings + 1) + j;
                    occluderVertices[index] = new Vector3(cos * halfWidth, y, sin * halfThickness);
                    occluderNormals[index] = new Vector3(cos * halfThickness, 0f, sin * halfWidth).normalized;
                }
            }

            occluderMesh.SetVertices(occluderVertices);
            occluderMesh.SetNormals(occluderNormals);
            occluderMesh.RecalculateBounds();

            occluderBlock.SetFloat(StackStartId, stackStart + ShadowInset);
            occluderBlock.SetFloat(StackEndId, stackEnd);
            occluderBlock.SetFloat(LightAlongId, Vector3.Dot(fit.Rotation * Vector3.up, LightDirection));
            occluder.SetPropertyBlock(occluderBlock);
        }

        MeshRenderer CreateChild(string childName, int layer)
        {
            var child = new GameObject(childName) { layer = layer };
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
