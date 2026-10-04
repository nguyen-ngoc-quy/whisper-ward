using System;
using UnityEngine;
using WhisperWard.AI.FSM;

namespace WhisperWard.AI.Alert
{
    /// <summary>
    /// Immutable payload carrying coordinated radio bark alert telemetry.
    /// Zero heap allocations on hot-path.
    /// </summary>
    public readonly struct GuardAlertEvent
    {
        public readonly int EmitterId;
        public readonly Vector3 EmitterPosition;
        public readonly Vector3 TargetLkp;
        public readonly float RadioRadius;

        public GuardAlertEvent(int emitterId, Vector3 emitterPosition, Vector3 targetLkp, float radioRadius = 25.0f)
        {
            EmitterId = emitterId;
            EmitterPosition = emitterPosition;
            TargetLkp = targetLkp;
            RadioRadius = radioRadius;
        }
    }

    /// <summary>
    /// Guard Radio Alert Transceiver component.
    /// Automatically broadcasts acoustic radio barks when entering Chase state,
    /// enforces a 5.0s rate-limit cooldown, and propagates alerts across a 25.0m radius.
    /// Unalerted/patrolling peers receiving the bark redirect to Investigate at the LKP
    /// with an applied bounded spatial error ($r \le 1.50\text{ m}$).
    /// Guards actively in Chase are immune to peer alerts per Chase-wins rule R13.
    /// Conforms to GDD Guard AI FSM (C3, lines 307 & 495) and Story ALERT-01.
    /// </summary>
    /// <example>
    /// <code>
    /// var transceiver = guardObject.AddComponent&lt;GuardRadioAlertTransceiver&gt;();
    /// transceiver.Configure(fsm, guardId: 1, radioRadius: 25.0f, cooldownDuration: 5.0f, investigateErrorRadius: 1.50f);
    /// transceiver.OnAlertReceived += (alert, target) =&gt; Debug.Log($"Received alert at {target}");
    /// </code>
    /// </example>
    public sealed class GuardRadioAlertTransceiver : MonoBehaviour
    {
        [Header("Radio Bark Parameters")]
        [Tooltip("Effective communication radius in meters (GDD: 25.0m).")]
        [SerializeField] private float _radioRadius = 25.00f;

        [Tooltip("Minimum cooldown duration between radio broadcasts in seconds (GDD: 5.0s).")]
        [SerializeField] private float _cooldownDuration = 5.00f;

        [Tooltip("Spatial error scatter offset applied to investigation target in meters (GDD: 1.50m).")]
        [SerializeField] private float _investigateErrorRadius = 1.50f;

        [Header("Component References")]
        [SerializeField] private GuardFSMRuntimeController _fsm;

        private int _guardId;
        private float _lastBroadcastTime = -100.0f;

        /// <summary>
        /// Global event bus broadcast when any guard emits a radio bark.
        /// Zero managed allocations.
        /// </summary>
        public static event Action<GuardAlertEvent> OnAnyGuardAlertBroadcast;

        /// <summary>Instance event fired when this guard transmits an alert.</summary>
        public event Action<GuardAlertEvent> OnAlertBroadcast;

        /// <summary>Instance event fired when this guard receives and acts on an alert.</summary>
        public event Action<GuardAlertEvent, Vector3> OnAlertReceived;

        public float RadioRadius => _radioRadius;
        public float CooldownDuration => _cooldownDuration;
        public float InvestigateErrorRadius => _investigateErrorRadius;
        public float LastBroadcastTime => _lastBroadcastTime;
        public int GuardId => _guardId;
        public GuardFSMRuntimeController FSM => _fsm;

        private void Awake()
        {
            if (_fsm == null)
            {
                _fsm = GetComponent<GuardFSMRuntimeController>();
            }

            if (_guardId == 0)
            {
                _guardId = GetInstanceID();
            }
        }

        private void OnEnable()
        {
            OnAnyGuardAlertBroadcast += HandleGlobalAlertBroadcast;

            if (_fsm != null)
            {
                _fsm.OnStateChanged += HandleFsmStateChanged;
            }
        }

        private void OnDisable()
        {
            OnAnyGuardAlertBroadcast -= HandleGlobalAlertBroadcast;

            if (_fsm != null)
            {
                _fsm.OnStateChanged -= HandleFsmStateChanged;
            }
        }

        /// <summary>
        /// Configures transceiver dependencies and tuning parameters.
        /// </summary>
        public void Configure(
            GuardFSMRuntimeController fsm,
            int guardId = 0,
            float radioRadius = 25.00f,
            float cooldownDuration = 5.00f,
            float investigateErrorRadius = 1.50f)
        {
            _fsm = fsm;
            _guardId = guardId != 0 ? guardId : GetInstanceID();
            _radioRadius = Mathf.Max(0.1f, radioRadius);
            _cooldownDuration = Mathf.Max(0.1f, cooldownDuration);
            _investigateErrorRadius = Mathf.Max(0.0f, investigateErrorRadius);
        }

        /// <summary>
        /// Listens to local FSM state transitions to broadcast radio bark upon entering Chase.
        /// </summary>
        private void HandleFsmStateChanged(GuardFSMRuntimeController.GuardState previous, GuardFSMRuntimeController.GuardState current)
        {
            if (current == GuardFSMRuntimeController.GuardState.Chase)
            {
                Vector3 lkp = _fsm != null && _fsm.PlayerTransform != null
                    ? _fsm.PlayerTransform.position
                    : transform.position;

                TryBroadcastRadioBark(lkp, Time.time, out _);
            }
        }

        /// <summary>
        /// Attempts to transmit a radio bark alert with rate-limit cooldown gating.
        /// Zero managed allocations.
        /// </summary>
        public bool TryBroadcastRadioBark(Vector3 targetLkp, float currentTime, out string reason)
        {
            if (_lastBroadcastTime >= 0f && (currentTime - _lastBroadcastTime) < _cooldownDuration)
            {
                reason = "ALERT_COOLDOWN_ACTIVE";
                return false;
            }

            _lastBroadcastTime = currentTime;
            var alert = new GuardAlertEvent(_guardId, transform.position, targetLkp, _radioRadius);

            reason = null;
            OnAlertBroadcast?.Invoke(alert);
            OnAnyGuardAlertBroadcast?.Invoke(alert);
            return true;
        }

        /// <summary>
        /// Receiver callback invoked when any guard broadcasts a radio alert.
        /// Enforces self-rejection, Chase-wins immunity (R13), distance threshold,
        /// and applies bounded spatial error offset to the resulting investigation target.
        /// Zero managed allocations.
        /// </summary>
        public void HandleGlobalAlertBroadcast(GuardAlertEvent alert)
        {
            // 1. Ignore own broadcast
            if (alert.EmitterId == _guardId)
            {
                return;
            }

            if (_fsm == null)
            {
                return;
            }

            // 2. Chase-wins rule R13: Guards in active Chase (or Captured) ignore peer alerts
            if (_fsm.CurrentState == GuardFSMRuntimeController.GuardState.Chase ||
                _fsm.CurrentState == GuardFSMRuntimeController.GuardState.Captured)
            {
                return;
            }

            // 3. Planar horizontal distance evaluation against radio radius
            float dx = transform.position.x - alert.EmitterPosition.x;
            float dz = transform.position.z - alert.EmitterPosition.z;
            float distSq = dx * dx + dz * dz;
            float radiusSq = alert.RadioRadius * alert.RadioRadius;

            if (distSq > radiusSq)
            {
                return; // Outside radio transmission range
            }

            // 4. Compute investigation position with bounded spatial error offset (AC-ALERT-03)
            Vector3 investigateTarget = ComputeInvestigateTarget(alert.TargetLkp, alert.EmitterPosition, _investigateErrorRadius);

            // 5. Transition to Investigate
            _fsm.TriggerInvestigate(investigateTarget, "radio_bark_alert");
            OnAlertReceived?.Invoke(alert, investigateTarget);
        }

        /// <summary>
        /// Pure deterministic calculation of investigation target with lateral spatial error offset.
        /// Offset is orthogonal to line from emitter to LKP, with magnitude <= errorRadius.
        /// </summary>
        public static Vector3 ComputeInvestigateTarget(Vector3 targetLkp, Vector3 emitterPosition, float errorRadius)
        {
            if (errorRadius <= 0.001f)
            {
                return targetLkp;
            }

            Vector3 dir = targetLkp - emitterPosition;
            dir.y = 0f;

            Vector3 lateral;
            if (dir.sqrMagnitude > 1e-4f)
            {
                lateral = Vector3.Cross(dir.normalized, Vector3.up).normalized;
            }
            else
            {
                lateral = Vector3.right;
            }

            return targetLkp + lateral * errorRadius;
        }

        /// <summary>
        /// Resets transceiver cooldown state.
        /// </summary>
        public void ResetCooldown()
        {
            _lastBroadcastTime = -100.0f;
        }
    }
}
