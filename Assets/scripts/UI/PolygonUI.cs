using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PolygonUI : Image
{
    public Vector2[] points; // 다각형 꼭짓점

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points == null || points.Length < 3) return;

        Vector2 center = Vector2.zero;
        foreach (var p in points) center += p;
        center /= points.Length;

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        // 중심점 추가
        vertex.position = center;
        vh.AddVert(vertex);

        // 꼭짓점 추가
        for (int i = 0; i < points.Length; i++)
        {
            vertex.position = points[i];
            vh.AddVert(vertex);
        }

        // 삼각형 생성
        for (int i = 0; i < points.Length; i++)
        {
            int next = i + 1;
            if (next >= points.Length) next = 0;
            vh.AddTriangle(0, i + 1, next + 1);
        }
    }
}
