using UnityEngine;

namespace OpenNPC.Demo.NPC
{
    /// <summary>
    /// Drives the stickman Animator (shared by NPCs and the player). Movement code
    /// only reports speed and events; which clip plays is decided here and in the
    /// generated controller (Assets/OpenNPC/Art/Characters/Stickman.controller).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NPCAnimation : MonoBehaviour
    {
        public static readonly int SpeedParam = Animator.StringToHash("Speed");
        public static readonly int TalkingParam = Animator.StringToHash("Talking");
        public static readonly int TurnLeftParam = Animator.StringToHash("TurnLeft");
        public static readonly int TurnRightParam = Animator.StringToHash("TurnRight");
        public static readonly int StopParam = Animator.StringToHash("Stop");
        private static readonly int LocomotionState = Animator.StringToHash("Locomotion");

        [SerializeField] private Animator animator;
        [SerializeField] private float speedDamping = 0.15f;

        private Transform _headBone;
        private float _headScale = 1f;

        public Animator Animator => animator;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            _headBone = FindDeep(transform, "head");
        }

        /// <summary>Individual variation that clips must not override.</summary>
        public void SetIndividuality(float headScale, float playbackRate, float cycleOffset)
        {
            _headScale = headScale;
            if (animator == null)
                return;
            animator.speed = playbackRate;
            animator.Play(LocomotionState, 0, cycleOffset);
        }

        public void SetSpeed(float metresPerSecond, bool immediate = false)
        {
            if (animator == null)
                return;
            if (immediate)
                animator.SetFloat(SpeedParam, metresPerSecond);
            else
                animator.SetFloat(SpeedParam, metresPerSecond, speedDamping, Time.deltaTime);
        }

        public void SetTalking(bool talking)
        {
            if (animator != null)
                animator.SetBool(TalkingParam, talking);
        }

        public void PlayTurn(bool left)
        {
            if (animator != null)
                animator.SetTrigger(left ? TurnLeftParam : TurnRightParam);
        }

        public void PlayStop()
        {
            if (animator != null)
                animator.SetTrigger(StopParam);
        }

        private void LateUpdate()
        {
            // Clips key bone scale at 1; re-apply the individual head size after animation.
            if (_headBone != null && !Mathf.Approximately(_headScale, 1f))
                _headBone.localScale = Vector3.one * _headScale;
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            if (root.name == childName)
                return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, childName);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
