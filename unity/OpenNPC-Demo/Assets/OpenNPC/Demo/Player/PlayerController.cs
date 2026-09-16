using OpenNPC.Config;
using OpenNPC.Demo.NPC;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenNPC.Demo.Player
{
    /// <summary>
    /// WASD / arrow keys on the street plane. The player is the same stickman with
    /// inverted colours (white body, black rim): a person without a persona.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NPCAnimation))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Vector2 depthLimits = new Vector2(-0.2f, 4.0f);
        [SerializeField] private float acceleration = 12f;
        [SerializeField] private float yawSharpness = 12f;

        private OpenNPCConfig _config;
        private NPCAnimation _animation;
        private Vector3 _velocity;

        /// <summary>False while typing in a conversation.</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>Optional scripted input (autopilot, tests); overrides the keyboard when set.</summary>
        public Vector2? ScriptedInput { get; set; }

        public float Speed => new Vector2(_velocity.x, _velocity.z).magnitude;

        private void Awake()
        {
            _config = OpenNPCConfig.Load();
            _animation = GetComponent<NPCAnimation>();
        }

        private void Update()
        {
            Vector2 input = ScriptedInput ?? (InputEnabled ? ReadKeyboard() : Vector2.zero);
            if (input.sqrMagnitude > 1f)
                input.Normalize();

            Vector3 wanted = new Vector3(input.x, 0f, input.y) * _config.playerSpeed;
            _velocity = Vector3.MoveTowards(_velocity, wanted, acceleration * Time.deltaTime);

            float half = _config.streetLength * 0.5f - 1f;
            Vector3 p = transform.position + _velocity * Time.deltaTime;
            p.x = Mathf.Clamp(p.x, -half, half);
            p.z = Mathf.Clamp(p.z, depthLimits.x, depthLimits.y);
            p.y = 0f;
            transform.position = p;

            if (_velocity.sqrMagnitude > 0.04f)
            {
                float targetYaw = Mathf.Atan2(_velocity.x, _velocity.z) * Mathf.Rad2Deg;
                float yaw = Mathf.LerpAngle(transform.eulerAngles.y, targetYaw, 1f - Mathf.Exp(-yawSharpness * Time.deltaTime));
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            _animation.SetSpeed(Speed);
        }

        /// <summary>Turn to face a point (e.g. the NPC being talked to).</summary>
        public void Face(Vector3 worldPosition)
        {
            Vector3 d = worldPosition - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        private static Vector2 ReadKeyboard()
        {
            Keyboard k = Keyboard.current;
            if (k == null)
                return Vector2.zero;
            float x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1f : 0f);
            float y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1f : 0f) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
        }
    }
}
