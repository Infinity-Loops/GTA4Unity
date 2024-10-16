using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Strip
{
    private List<Polygon> poly = new List<Polygon>();
    private List<Vertex> vert = new List<Vertex>();
    private int polyCount;
    private int materialIndex; // shader index

    public Vertex max = new Vertex(0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
    public Vertex min = new Vertex(0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
    public Vertex center = new Vertex(0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
    public Vector4 sphere = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

    public Strip(int polyCount, int materialIndex)
    {
        this.polyCount = polyCount;
        this.materialIndex = materialIndex;
    }

    public void addPoly(Polygon polygon)
    {
        poly.Add(polygon);
    }

    public int addVertex(Vertex vertex)
    {
        int ret = -1;
        if (!vert.Contains(vertex))
        {
            ret = vert.Count;
            vert.Add(vertex);
            // //Message.displayMsgLow("Added vertex at " + ret);
        }
        else
        {
            ret = vert.IndexOf(vertex);
            // //Message.displayMsgLow("Vertex bestond al in deze strip " + ret);
        }
        return ret;
    }

    public void addVertexToStrip(Vertex vertex)
    {
        vert.Add(vertex);
        checkBounds(vertex);
    }

    public Polygon getPoly(int id)
    {
        return poly[id];
    }

    public int getPolyCount()
    {
        return poly.Count;
    }

    public int getShaderNumber()
    {
        return materialIndex;
    }

    public int getVertexCount()
    {
        return vert.Count;
    }

    public Vertex getVertex(int i)
    {
        return vert[i];
    }

    public void checkBounds(Vertex vertex)
    {
        if (vertex.getVertexX() > max.getVertexX())
            max.setVertexX(vertex.getVertexX());
        if (vertex.getVertexY() > max.getVertexY())
            max.setVertexY(vertex.getVertexY());
        if (vertex.getVertexZ() > max.getVertexZ())
            max.setVertexZ(vertex.getVertexZ());
        if (vertex.getVertexX() < min.getVertexX())
            min.setVertexX(vertex.getVertexX());
        if (vertex.getVertexY() < min.getVertexY())
            min.setVertexY(vertex.getVertexY());
        if (vertex.getVertexZ() < min.getVertexZ())
            min.setVertexZ(vertex.getVertexZ());
        center.setVertexX((max.getVertexX() + min.getVertexX()) / 2);
        center.setVertexY((max.getVertexY() + min.getVertexY()) / 2);
        center.setVertexZ((max.getVertexZ() + min.getVertexZ()) / 2);
        sphere.x = center.x;
        sphere.y = center.y;
        sphere.z = center.z;
        sphere.w = getMax();
    }

    private float getMax()
    {
        float value = max.x;
        if (value < max.y)
            value = max.y;
        if (value < max.z)
            value = max.z;
        if (value < 0 - min.x)
            value = 0 - min.x;
        if (value < 0 - min.y)
            value = 0 - min.y;
        if (value < 0 - min.z)
            value = 0 - min.z;
        // System.out.println("Max is: " + value);
        return value;
    }
}
