using System;
using OpenNPC.Config;
using OpenNPC.Personas;
using UnityEngine;

namespace OpenNPC.Demo.NPC
{
    /// <summary>
    /// The NPC's small brain for everything that is not dialogue: which state it is
    /// in and when to stop, turn or change pace. It coordinates the sibling
    /// components but owns none of their work.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NPCPersona), typeof(NPCMovement), typeof(NPCAnimation))]
    [RequireComponent(typeof(NPCInteraction), typeof(NPCDialogue))]
    public sealed class NPCController : MonoBehaviour
    {
        [SerializeField] private float leavingDuration = 2.5f;

        private OpenNPCConfig _config;
        private System.Random _rng;
        private float _decisionTimer;
        private float _stateTimer;
        private float _preferredSpeed;
        private float _speakTimer;

        public NPCPersona Persona { get; private set; }
        public NPCMovement Movement { get; private set; }
        public NPCAnimation Animation { get; private set; }
        public NPCInteraction Interaction { get; private set; }
        public NPCDialogue Dialogue { get; private set; }

        public NPCState State { get; private set; } = NPCState.Walking;
        public bool IsBackground { get; private set; }

        public event Action<NPCController, NPCState> StateChanged;

        private void Awake()
        {
            Persona = GetComponent<NPCPersona>();
            Movement = GetComponent<NPCMovement>();
            Animation = GetComponent<NPCAnimation>();
            Interaction = GetComponent<NPCInteraction>();
            Dialogue = GetComponent<NPCDialogue>();
            _config = OpenNPCConfig.Load();
            Movement.TurnStarted += left => Animation.PlayTurn(left);
            Movement.WrappedAround += OnWrapped;
        }

        /// <summary>Called once by the population system.</summary>
        public void Initialize(Persona persona, bool background, int seed)
        {
            Persona.Assign(persona);
            IsBackground = background;
            _rng = new System.Random(seed);
            _preferredSpeed = Movement.TargetSpeed;
            Interaction.Configure(_config.interactionRadius);
            ScheduleDecision();
        }

        // ------------------------------------------------------------------ //
        public void BeginConversation(Transform player)
        {
            SetState(NPCState.Talking);
            Movement.Pause(true);
            Movement.Face(player.position);
            Animation.SetTalking(false);
        }

        public void EndConversation(Transform player)
        {
            if (State != NPCState.Talking)
                return;
            Animation.SetTalking(false);
            Interaction.StartCooldown(_config.conversationCooldown);
            Movement.Pause(false);
            Movement.WalkAwayFrom(player.position);
            Movement.SetTargetSpeed(_preferredSpeed);
            SetState(NPCState.Leaving);
            _stateTimer = leavingDuration;
        }

        /// <summary>Gesture for roughly as long as the line takes to read.</summary>
        public void SpeakFor(float seconds)
        {
            if (State != NPCState.Talking)
                return;
            _speakTimer = seconds;
            Animation.SetTalking(true);
        }

        // ------------------------------------------------------------------ //
        private void Update()
        {
            Animation.SetSpeed(Movement.CurrentSpeed);
            if (_rng == null)
                return;

            if (_speakTimer > 0f)
            {
                _speakTimer -= Time.deltaTime;
                if (_speakTimer <= 0f)
                    Animation.SetTalking(false);
            }

            switch (State)
            {
                case NPCState.Walking:
                    _decisionTimer -= Time.deltaTime;
                    if (_decisionTimer <= 0f)
                        Decide();
                    break;
                case NPCState.Idle:
                case NPCState.Leaving:
                    _stateTimer -= Time.deltaTime;
                    if (_stateTimer <= 0f)
                    {
                        Movement.Pause(false);
                        Movement.SetTargetSpeed(_preferredSpeed);
                        SetState(NPCState.Walking);
                        ScheduleDecision();
                    }
                    break;
            }
        }

        private void Decide()
        {
            double roll = _rng.NextDouble();
            float stop = _config.stopChance;
            float turn = stop + _config.turnChance;
            float pace = turn + _config.paceChangeChance;
            float lane = pace + _config.laneChangeChance;

            if (roll < stop && !IsBackground)
            {
                Movement.Pause(true);
                Animation.PlayStop();
                SetState(NPCState.Idle);
                _stateTimer = Range(_config.idleDuration);
            }
            else if (roll < turn)
            {
                Movement.TurnAround();
            }
            else if (roll < pace)
            {
                _preferredSpeed = Range(_config.walkSpeedRange);
                Movement.SetTargetSpeed(_preferredSpeed);
            }
            else if (roll < lane)
            {
                float[] lanes = IsBackground ? _config.backgroundLaneDepths : _config.laneDepths;
                if (lanes.Length > 0)
                    Movement.SetLane(lanes[_rng.Next(lanes.Length)]);
            }
            ScheduleDecision();
        }

        private void OnWrapped()
        {
            if (State == NPCState.Talking)
                return;
            // A "new" pedestrian enters: fresh pace so the street does not look looped.
            _preferredSpeed = Range(_config.walkSpeedRange);
            Movement.SetTargetSpeed(_preferredSpeed);
        }

        private void ScheduleDecision() => _decisionTimer = Range(_config.decisionInterval);

        private float Range(Vector2 r) => Mathf.Lerp(r.x, r.y, (float)_rng.NextDouble());

        private void SetState(NPCState state)
        {
            if (State == state)
                return;
            State = state;
            StateChanged?.Invoke(this, state);
        }
    }
}
