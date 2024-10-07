using UnityEngine;

namespace oojjrs.onav
{
    [RequireComponent(typeof(MyRvoAgent))]
    public class MyRvoDirector : MonoBehaviour, MyRvoDirectorInterface
    {
        bool MyRvoDirectorInterface.Working => Working;

        private MyRvoAgentInterface Agent { get; set; }
        private Vector3? LastAvoidance { get; set; }
        private float LastTime { get; set; }
        public bool Working { get; set; }

        private void Start()
        {
            Agent = GetComponent<MyRvoAgentInterface>();
        }

        Vector3 MyRvoDirectorInterface.Modify(Vector3 velocity, float time)
        {
            if (Agent == default)
                Agent = GetComponent<MyRvoAgentInterface>();

            if (Agent == default)
            {
                Debug.LogWarning($"이게 왜 없지");
                return velocity;
            }

            if (Agent.Container == default)
            {
                Debug.LogWarning($"이건 또 왜 없지");
                return velocity;
            }

            var vdir = velocity.normalized;
            var nextPosition = Agent.Position + velocity;
            var obstacle = Agent.Container.GetObstacle(nextPosition, Agent);
            if (obstacle == default)
                return velocity;

            if (LastAvoidance.HasValue && time - LastTime < 0.2f)
                return LastAvoidance.Value;

            // 우측 회피를 우선으로 하겠다.
            var (leftAvoidance, leftCount) = Avoid(velocity, vdir, obstacle, Vector3.left);
            var (rightAvoidance, rightCount) = Avoid(velocity, vdir, obstacle, Vector3.right);
            if (leftCount == rightCount)
            {
                if (Vector3.Angle(vdir, rightAvoidance) < Vector3.Angle(vdir, leftAvoidance))
                    LastAvoidance = rightAvoidance * velocity.magnitude;
                else
                    LastAvoidance = leftAvoidance * velocity.magnitude;
            }
            else
            {
                if (rightCount < leftCount)
                    LastAvoidance = rightAvoidance * velocity.magnitude;
                else
                    LastAvoidance = leftAvoidance * velocity.magnitude;
            }

            LastTime = time;
            return LastAvoidance.Value;
        }

        private (Vector3 dir, int count) Avoid(Vector3 velocity, Vector3 vdir, MyRvoObstacleInterface obstacle, Vector3 dir)
        {
            var speed = velocity.magnitude;
            var totalVelocity = velocity;
            var count = 0;
            while (obstacle != default)
            {
                // N번 이상 길 찾고 있으면 못 찾았다고 생각하자구
                if (count > 1)
                    break;

                ++count;
                totalVelocity += Quaternion.FromToRotation(vdir, (obstacle.Position - Agent.Position).normalized) * Quaternion.FromToRotation(Vector3.forward, dir) * velocity * (obstacle.Radius + Agent.Radius);

                obstacle = Agent.Container.GetObstacle(Agent.Position + velocity + Vector3.ClampMagnitude(totalVelocity, speed * count), Agent);
            }

            return (totalVelocity.normalized, count);
        }
    }
}
