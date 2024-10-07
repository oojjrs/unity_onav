using UnityEngine;

namespace oojjrs.onav
{
    public class MyRvoAgent : MonoBehaviour, MyRvoAgentInterface
    {
        bool MyRvoObstacleInterface.Alive => Alive;
        MyRvoAgentContainer MyRvoAgentInterface.Container { get; set; }
        Vector3 MyRvoObstacleInterface.Position => Position;
        float MyRvoObstacleInterface.Radius => _radius;

        // 극한의 최적화
        private Vector3 _previousPosition;
        private float _radius;
        private float _sqrRadius;

        // TODO : 올바른 방법은 아니지만, Start 호출 시점을 보장할 수 없으니까 true로 시작한다.
        private bool Alive { get; set; } = true;
        public Vector3 Forward => Velocity.normalized;
        private Vector3 Position => transform.position;
        public float Radius
        {
            set
            {
                _radius = value;
                _sqrRadius = value * value;
            }
        }
        public Vector3 Velocity { get; set; }

        private void OnDestroy()
        {
            Alive = false;
        }

        private void Start()
        {
            _previousPosition = transform.position;
        }

        private void Update()
        {
            if (Position != _previousPosition)
            {
                Velocity = Position - _previousPosition;
                _previousPosition = Position;
            }
            else
            {
                Velocity = Vector3.zero;
            }
        }

        bool MyRvoObstacleInterface.IsCollidedInXZ(Vector3 pos)
        {
            var lx = _previousPosition.x - pos.x;
            var lz = _previousPosition.z - pos.z;
            return lx * lx + lz * lz <= _sqrRadius;
        }
    }
}
