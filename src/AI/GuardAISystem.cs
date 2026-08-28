// Guard AI System - Entry point for Unity
using UnityEngine;
using WhisperWard.AI.Core;
using WhisperWard.AI.FSM;
using WhisperWard.AI.Perception;

public class GuardAISystem : MonoBehaviour
{
    [Header("System Configuration")]
    [SerializeField] private GuardFSM fsm;
    [SerializeField] private PerceptionDriver perceptionDriver;
    [SerializeField] private DecisionTap decisionTap;

    [Header("Navigation Dependencies")]
    [SerializeField] private GuardNavigator navigator;
    [SerializeField] private CatchEvaluator catchEvaluator;

    private void Awake()
    {
        // Verify all required components exist
        Debug.Assert(fsm != null, "GuardFSM not assigned to GuardAISystem");
        Debug.Assert(perceptionDriver != null, "PerceptionDriver not assigned");
        Debug.Assert(decisionTap != null, "DecisionTap not assigned");
    }

    private void Start()
    {
        // Initialize the system
        Debug.Log("[GuardAISystem] Initialized");
    }

    public GuardFSM FSM => fsm;
    public DecisionTap DecisionTap => decisionTap;
}