using Unity.Mathematics;

namespace IVUnity.ECS
{
    /// <summary>Pure cell-coordinate math. Side-effect-free for easy unit testing.</summary>
    public static class CellMath
    {
        /// <summary>World-space position → cell index given cellSize. Rounded toward -∞ so negative coords work.</summary>
        public static int2 PositionToCell(float3 position, float cellSize)
        {
            return new int2(
                (int)math.floor(position.x / cellSize),
                (int)math.floor(position.z / cellSize));
        }

        /// <summary>Cell center in world space.</summary>
        public static float3 CellCenter(int2 cell, float cellSize)
        {
            return new float3(
                (cell.x + 0.5f) * cellSize,
                0f,
                (cell.y + 0.5f) * cellSize);
        }

        /// <summary>XZ distance from cell center to world-space point. Y ignored.</summary>
        public static float DistanceToCellCenterXZ(int2 cell, float cellSize, float3 point)
        {
            float3 c = CellCenter(cell, cellSize);
            float dx = c.x - point.x;
            float dz = c.z - point.z;
            return math.sqrt(dx * dx + dz * dz);
        }
    }
}
