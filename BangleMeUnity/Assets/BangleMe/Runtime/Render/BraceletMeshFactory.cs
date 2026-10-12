using System.Collections.Generic;
using UnityEngine;

namespace BangleMe
{
    public static class BraceletMeshFactory
    {
        // Every bracelet mesh is built around local +Y with its inner surface at this radius; the
        // stack scales it radially to the measured wrist.
        public const float ReferenceInnerRadius = 0.03f;

        public static Mesh Create(BraceletDesign design, bool highDetail)
        {
            var mesh = design.Shape == BraceletShape.Strands
                ? CreateStrands(design, highDetail ? 192 : 96, highDetail ? 12 : 6)
                : CreateBand(design, highDetail ? 128 : 64, highDetail ? 32 : 14);
            mesh.name = design.Name;
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh CreateStrands(BraceletDesign design, int ringSegments, int tubeSegments)
        {
            var strandRadius = design.StrandRadius;
            var coreOffset = strandRadius * BraceletDesign.StrandCoreOffset;
            var coreRadius = ReferenceInnerRadius + design.Thickness * 0.5f;

            var columns = ringSegments + 1;
            var rows = tubeSegments + 1;
            var vertices = new List<Vector3>(design.StrandCount * columns * rows);
            var normals = new List<Vector3>(vertices.Capacity);
            var triangles = new List<int>(design.StrandCount * ringSegments * tubeSegments * 6);

            for (var strand = 0; strand < design.StrandCount; strand++)
            {
                var phase = strand * Mathf.PI * 2f / design.StrandCount;
                var firstVertex = vertices.Count;
                for (var i = 0; i < columns; i++)
                {
                    var ringAngle = i / (float)ringSegments * Mathf.PI * 2f;
                    var center = StrandCenter(coreRadius, coreOffset, design.Twists, phase, ringAngle);
                    const float step = 1e-3f;
                    var tangent = StrandCenter(coreRadius, coreOffset, design.Twists, phase, ringAngle + step)
                                - StrandCenter(coreRadius, coreOffset, design.Twists, phase, ringAngle - step);
                    tangent /= Mathf.Sqrt(tangent.sqrMagnitude);

                    var outward = new Vector3(Mathf.Cos(ringAngle), 0f, Mathf.Sin(ringAngle));
                    var side = Vector3.Cross(tangent, outward);
                    side /= Mathf.Sqrt(side.sqrMagnitude);
                    var up = Vector3.Cross(side, tangent);

                    for (var j = 0; j < rows; j++)
                    {
                        var tubeAngle = j / (float)tubeSegments * Mathf.PI * 2f;
                        var normal = up * Mathf.Cos(tubeAngle) + side * Mathf.Sin(tubeAngle);
                        vertices.Add(center + normal * strandRadius);
                        normals.Add(normal);
                    }
                }
                AddGrid(triangles, firstVertex, ringSegments, tubeSegments, rows);
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        static Vector3 StrandCenter(float coreRadius, float coreOffset, int twists, float phase, float ringAngle)
        {
            var twist = twists * ringAngle + phase;
            var radius = coreRadius + coreOffset * Mathf.Cos(twist);
            return new Vector3(Mathf.Cos(ringAngle) * radius, coreOffset * Mathf.Sin(twist), Mathf.Sin(ringAngle) * radius);
        }

        static Mesh CreateBand(BraceletDesign design, int ringSegments, int outerSamples)
        {
            var profile = BandOutline(design, outerSamples);
            var pointCount = profile.Count;
            var profileNormals = new Vector2[pointCount];
            for (var j = 0; j < pointCount; j++)
            {
                // The outline is closed: its last point repeats the first so UVs can wrap.
                var previous = profile[j == 0 ? pointCount - 2 : j - 1];
                var next = profile[j == pointCount - 1 ? 1 : j + 1];
                var along = new Vector2(next.x - previous.x, next.y - previous.y);
                var normal = new Vector2(along.y, -along.x);
                profileNormals[j] = normal / Mathf.Sqrt(normal.sqrMagnitude);
            }

            var circumference = 2f * Mathf.PI * (ReferenceInnerRadius + design.Thickness);
            var repeats = design.PatternLength > 0 ? Mathf.Max(1, Mathf.RoundToInt(circumference / design.PatternLength)) : 1;

            var columns = ringSegments + 1;
            var vertices = new Vector3[columns * pointCount];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            for (var i = 0; i < columns; i++)
            {
                var fraction = i / (float)ringSegments;
                var ringAngle = fraction * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(ringAngle), 0f, Mathf.Sin(ringAngle));
                for (var j = 0; j < pointCount; j++)
                {
                    var index = i * pointCount + j;
                    var point = profile[j];
                    vertices[index] = outward * (ReferenceInnerRadius + point.x) + Vector3.up * point.y;
                    normals[index] = outward * profileNormals[j].x + Vector3.up * profileNormals[j].y;
                    uvs[index] = new Vector2(fraction * repeats, point.z);
                }
            }

            var triangles = new List<int>(ringSegments * (pointCount - 1) * 6);
            AddGrid(triangles, 0, ringSegments, pointCount - 1, pointCount);

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            return mesh;
        }

        // Closed outline of the band's cross-section: x = height above the inner surface,
        // y = position along the arm, z = texture V (0..1 across the outer face).
        static List<Vector3> BandOutline(BraceletDesign design, int outerSamples)
        {
            var halfWidth = design.Width * 0.5f;
            var outline = new List<Vector3>(outerSamples + 4);
            for (var k = 0; k <= outerSamples; k++)
            {
                // Cosine spacing puts more samples at the edges, where the outline turns fastest.
                var t = k / (float)outerSamples;
                var across = -Mathf.Cos(t * Mathf.PI);
                outline.Add(new Vector3(OuterHeight(design, across), across * halfWidth, t));
            }
            outline.Add(new Vector3(0f, halfWidth * 0.5f, 0.67f));
            outline.Add(new Vector3(0f, -halfWidth * 0.5f, 0.33f));
            outline.Add(outline[0]);
            return outline;
        }

        // across: -1..1 over the band's width.
        static float OuterHeight(BraceletDesign design, float across)
        {
            var distance = Mathf.Abs(across);
            switch (design.Profile)
            {
                case BandProfile.DomedWithRims:
                {
                    const float rimWidth = 0.16f;
                    var rimCenter = 1f - rimWidth * 0.5f;
                    var rim = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((distance - rimCenter) / (rimWidth * 0.5f), 2f)));
                    var dome = 0.5f + 0.38f * (1f - Mathf.Pow(distance / (1f - rimWidth), 2f));
                    var body = distance < 1f - rimWidth ? dome : 0f;
                    return design.Thickness * Mathf.Max(body, rim * 0.78f);
                }
                case BandProfile.Flat:
                {
                    const float corner = 0.1f;
                    if (distance < 1f - corner) return design.Thickness;
                    var t = (distance - (1f - corner)) / corner;
                    return design.Thickness * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
                }
                default:
                    return design.Thickness * Mathf.Sqrt(Mathf.Max(0f, 1f - distance * distance));
            }
        }

        public static void AddGrid(List<int> triangles, int firstVertex, int columns, int rows, int verticesPerColumn)
        {
            for (var i = 0; i < columns; i++)
            {
                for (var j = 0; j < rows; j++)
                {
                    var a = firstVertex + i * verticesPerColumn + j;
                    var b = a + verticesPerColumn;
                    triangles.Add(a);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(b + 1);
                    triangles.Add(b);
                }
            }
        }
    }
}
