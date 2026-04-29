namespace RageLib.Collision
{
    // rage::phBound::BoundType (GTA IV values from GTAIV.exe.c switch at line 444812)
    // NOTE: values 8+ differ from Rage/GTA V
    public enum BoundType : byte
    {
        Sphere = 0,
        Capsule = 1,
        Box = 3,
        Geometry = 4,
        CurvedGeometry = 5,
        Grid = 6,
        Ribbon = 7,
        BVH = 10,
        Surface = 11,
        Composite = 12,
        Invalid = 255
    }
}
