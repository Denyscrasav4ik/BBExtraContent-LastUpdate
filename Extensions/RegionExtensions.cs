

namespace BBTimes.Extensions;

public static class RegionExtensions
{
    public static bool InsideRegion(this Region region, IntVector2 position) =>
        region.min.x <= position.x && region.min.z <= position.z && region.max.x >= position.x && region.max.z >= position.z;

    public static bool Intersects(this Region regA, Region regB) =>
        regA.min.x <= regB.max.x && regA.max.x >= regB.min.x && regA.min.z <= regB.max.z && regA.max.z >= regB.min.z;
}