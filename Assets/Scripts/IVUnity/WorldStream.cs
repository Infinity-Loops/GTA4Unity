using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

[System.Serializable]
public class Cell : IDisposable
{
    public Vector2Int cellPosition;
    public List<Ipl_INST> objectsInCell = new List<Ipl_INST>();
    public int loadedObjects;

    private bool loaded;

    private NativeArray<int> staticGeometriesInstances;
    private NativeArray<int> staticGeometriesTransformInstances;
    private StaticGeometry[] staticGeometriesComponents = new StaticGeometry[] { };

    private Vector3Int worldOrigin = new Vector3Int(-3000, 0, -3000);

    public Cell(Vector2Int cellPosition)
    {
        this.cellPosition = cellPosition;
    }

    public void Dispose()
    {
        staticGeometriesInstances.Dispose();
        staticGeometriesTransformInstances.Dispose();
    }

    public bool IsPositionInCell(Vector3 position,Vector3 loadRange, float cellSize)
    {
        float xMin = worldOrigin.x + cellPosition.x * cellSize - loadRange.x;
        float xMax = worldOrigin.x + (cellPosition.x + 1) * cellSize + loadRange.x;
        float zMin = worldOrigin.z + cellPosition.y * cellSize - loadRange.z;
        float zMax = worldOrigin.z + (cellPosition.y + 1) * cellSize + loadRange.z;

        return position.x >= xMin && position.x < xMax && position.z >= zMin && position.z < zMax;
    }

    public async void LoadCellItems(WorldComposerMachine composer, GTADatLoader loader, int staticGeometryInstanceID)
    {
        if (!loaded)
        {
            loaded = true;

            composer.AllocateGameObjects(staticGeometryInstanceID, objectsInCell.Count, ref staticGeometriesInstances, ref staticGeometriesTransformInstances, ref staticGeometriesComponents);

            await composer.ProcessItems(objectsInCell, loader, staticGeometriesInstances, staticGeometriesTransformInstances, staticGeometriesComponents);

            for (int i = 0; i < staticGeometriesComponents.Length; i++)
            {
                staticGeometriesComponents[i].LoadModel();
            }
        }
        else
        {
            GameObject.SetGameObjectsActive(staticGeometriesInstances, true);
        }
    }

    public void UnloadCellItems()
    {
        if (staticGeometriesInstances != null && loaded)
        {
            GameObject.SetGameObjectsActive(staticGeometriesInstances, false);
        }
    }

    public void DrawCellBounds(float cellSize, Color color)
    {
        float xMin = worldOrigin.x + cellPosition.x * cellSize;
        float zMin = worldOrigin.z + cellPosition.y * cellSize;
        float xMax = worldOrigin.x + (cellPosition.x + 1) * cellSize;
        float zMax = worldOrigin.z + (cellPosition.y + 1) * cellSize;

        Vector3 topLeft = new Vector3(xMin, 0, zMin);
        Vector3 topRight = new Vector3(xMax, 0, zMin);
        Vector3 bottomRight = new Vector3(xMax, 0, zMax);
        Vector3 bottomLeft = new Vector3(xMin, 0, zMax);

        Gizmos.color = color;
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);
    }
}