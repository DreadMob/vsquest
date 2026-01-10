using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class ActionObjectiveUtil
    {
        public static int countBlockEntities(Vec3i pos, IBlockAccessor blockAccessor, System.Func<BlockEntity, bool> matcher)
        {
            return BlockEntitySearchUtils.CountBlockEntities(pos, blockAccessor, matcher);
        }
    }
}