using UnityEngine;

namespace oojjrs.onav
{
    public interface MyRvoDirectorInterface
    {
        bool Working { get; }

        Vector3 Modify(Vector3 v, float time);
    }
}
