#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Unity.Mathematics;

namespace IVUnity.ECS.Tests
{
    public class CellIndexTests
    {
        [Test]
        public void PositionToCell_Origin_ReturnsZero()
        {
            Assert.AreEqual(new int2(0, 0), CellMath.PositionToCell(new float3(0, 0, 0), 100f));
        }

        [Test]
        public void PositionToCell_InsideFirstCell_ReturnsZero()
        {
            Assert.AreEqual(new int2(0, 0), CellMath.PositionToCell(new float3(50, 0, 50), 100f));
        }

        [Test]
        public void PositionToCell_OnBoundary_ReturnsNextCell()
        {
            // Exactly at 100,100 → cell (1,1) because floor semantics.
            Assert.AreEqual(new int2(1, 1), CellMath.PositionToCell(new float3(100, 0, 100), 100f));
        }

        [Test]
        public void PositionToCell_NegativeCoord_FloorsCorrectly()
        {
            Assert.AreEqual(new int2(-1, -1), CellMath.PositionToCell(new float3(-1, 0, -1), 100f));
            Assert.AreEqual(new int2(-1, -1), CellMath.PositionToCell(new float3(-50, 0, -50), 100f));
            Assert.AreEqual(new int2(-2, -2), CellMath.PositionToCell(new float3(-101, 0, -101), 100f));
        }

        [Test]
        public void PositionToCell_YIsIgnored()
        {
            Assert.AreEqual(
                CellMath.PositionToCell(new float3(50, 0, 50), 100f),
                CellMath.PositionToCell(new float3(50, 500, 50), 100f));
        }

        [Test]
        public void CellCenter_ReturnsMidpoint()
        {
            float3 c = CellMath.CellCenter(new int2(0, 0), 100f);
            Assert.AreEqual(50f, c.x, 0.001f);
            Assert.AreEqual(50f, c.z, 0.001f);
        }

        [Test]
        public void DistanceToCellCenterXZ_IgnoresY()
        {
            float d1 = CellMath.DistanceToCellCenterXZ(new int2(0, 0), 100f, new float3(50, 0, 50));
            float d2 = CellMath.DistanceToCellCenterXZ(new int2(0, 0), 100f, new float3(50, 9999, 50));
            Assert.AreEqual(d1, d2, 0.001f);
            Assert.AreEqual(0f, d1, 0.001f);
        }
    }
}
#endif
