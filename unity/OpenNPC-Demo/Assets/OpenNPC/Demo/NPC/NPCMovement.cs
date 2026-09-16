using System;
using UnityEngine;

namespace OpenNPC.Demo.NPC
{
    /// <summary>
    /// Lightweight pedestrian movement along waypoint lanes: the street is a set of
    /// lines at fixed depths (z) running along x. No NavMesh, no physics. The
    /// stickman model faces +Z, so walking +X is a yaw of 90 degrees.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NPCMovement : MonoBehaviour
    {
        [SerializeField] private float acceleration = 2.2f;
        [SerializeField] private float laneChangeSpeed = 0.7f;
        [SerializeField] private float turnDuration = 0.45f;
        [SerializeField] private float yawSharpness = 8f;
        [Tooltip("Degrees an NPC turns toward the camera while talking, so the pose reads.")]
        [SerializeField] private float talkYawTowardCamera = 40f;

        private float _laneZ;
        private float _targetSpeed;
        private float _halfLength;
        private float _turnTimer;
        private float _targetYaw;
        private bool _paused;

        public float CurrentSpeed { get; private set; }
        public float TargetSpeed => _targetSpeed;
        public int Direction { get; private set; } = 1;
        public float LaneZ => _laneZ;
        public bool IsTurning => _turnTimer > 0f;

        /// <summary>Raised when the NPC walks off one end of the street and re-enters at the other.</summary>
        public event Action WrappedAround;
        /// <summary>Raised when a turn-around starts; argument is true for a left turn.</summary>
        public event Action<bool> TurnStarted;

        public void Initialize(float x, float laneZ, float speed, int direction, float streetLength)
        {
            _laneZ = laneZ;
            _targetSpeed = speed;
            CurrentSpeed = speed;
            Direction = direction >= 0 ? 1 : -1;
            _halfLength = streetLength * 0.5f;
            _targetYaw = WalkYaw;
            transform.SetPositionAndRotation(new Vector3(x, 0f, laneZ), Quaternion.Euler(0f, _targetYaw, 0f));
        }

        private float WalkYaw => Direction > 0 ? 90f : -90f;

        public void SetTargetSpeed(float speed) => _targetSpeed = Mathf.Max(0f, speed);
        public void SetLane(float z) => _laneZ = z;

        public void Pause(bool paused)
        {
            _paused = paused;
            if (!paused)
                _targetYaw = WalkYaw;
        }

        public void TurnAround()
        {
            if (IsTurning)
                return;
            Direction = -Direction;
            _turnTimer = turnDuration;
            // Facing +X (yaw 90) to -X (yaw -90) through the camera side is a right turn.
            TurnStarted?.Invoke(Direction > 0);
            _targetYaw = WalkYaw;
        }

        /// <summary>Stand and face a world position, three-quarter toward the camera.</summary>
        public void Face(Vector3 worldPosition)
        {
            float side = Mathf.Sign(worldPosition.x - transform.position.x);
            if (side == 0f) side = 1f;
            _targetYaw = side > 0f ? 90f + talkYawTowardCamera : -90f - talkYawTowardCamera;
        }

        /// <summary>Walk away from a position (used after a conversation).</summary>
        public void WalkAwayFrom(Vector3 worldPosition)
        {
            int away = worldPosition.x > transform.position.x ? -1 : 1;
            Direction = away;
            _targetYaw = WalkYaw;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_turnTimer > 0f)
                _turnTimer -= dt;

            float wanted = _paused || IsTurning ? 0f : _targetSpeed;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, wanted, acceleration * dt);

            Vector3 p = transform.position;
            p.x += Direction * CurrentSpeed * dt;
            if (!_paused)
                p.z = Mathf.MoveTowards(p.z, _laneZ, laneChangeSpeed * dt);

            if (p.x > _halfLength + 1.5f || p.x < -_halfLength - 1.5f)
            {
                p.x = -Mathf.Sign(p.x) * (_halfLength + 1.2f);
                WrappedAround?.Invoke();
            }
            transform.position = p;

            float yaw = Mathf.LerpAngle(transform.eulerAngles.y, _targetYaw, 1f - Mathf.Exp(-yawSharpness * dt));
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
