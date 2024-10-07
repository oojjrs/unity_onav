using UnityEngine;

namespace oojjrs.onav
{
    public interface MyRvoObstacleInterface
    {
        bool Alive { get; }
        Vector3 Position { get; }
        float Radius { get; }

        bool IsCollidedInXZ(Vector3 pos);
    }
}
