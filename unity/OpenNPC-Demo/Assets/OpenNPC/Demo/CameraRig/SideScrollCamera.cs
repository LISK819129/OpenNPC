using OpenNPC.Config;
using UnityEngine;

namespace OpenNPC.Demo.CameraRig
{
    /// <summary>
    /// Side-on follow camera for the 2.5D street. Tracks the target along x only,
    /// and eases in a little during conversations so bubbles and poses read.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class SideScrollCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.35f, -12f);
        [SerializeField] private float pitch = 4.5f;
        [SerializeField] private float followSharpness = 3.5f;
        [SerializeField] private float fieldOfView = 31f;
        [SerializeField] private float conversationFieldOfView = 25f;
        [Tooltip("How far the view may scroll, measured from the street ends.")]
        [SerializeField] private float edgeMargin = 9f;

        private Camera _camera;
        private OpenNPCConfig _config;
        private float _focusX;
        private bool _hasFocus;

        public Transform Target { get => target; set => target = value; }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _config = OpenNPCConfig.Load();
        }

        /// <summary>While set, the camera centres between the target and this x.</summary>
        public void SetConversationFocus(Vector3? worldPosition)
        {
            _hasFocus = worldPosition.HasValue;
            if (_hasFocus)
                _focusX = worldPosition.Value.x;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;
            float half = _config.streetLength * 0.5f - edgeMargin;
            float x = _hasFocus ? (target.position.x + _focusX) * 0.5f : target.position.x;
            x = Mathf.Clamp(x, -half, half);

            Vector3 wanted = new Vector3(x, 0f, 0f) + offset;
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, wanted, t);
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _hasFocus ? conversationFieldOfView : fieldOfView, t);
        }

        public void SnapToTarget()
        {
            if (target == null)
                return;
            transform.position = new Vector3(target.position.x, 0f, 0f) + offset;
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            if (_camera != null)
                _camera.fieldOfView = fieldOfView;
        }
    }
}
