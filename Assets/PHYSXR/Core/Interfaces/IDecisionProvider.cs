using PHYSXR.Core.Data;
using PHYSXR.Core.Enums;

namespace PHYSXR.Core.Interfaces
{
    public interface IDecisionProvider
    {
        GhostAction Decide(WorldState worldState);
    }
}