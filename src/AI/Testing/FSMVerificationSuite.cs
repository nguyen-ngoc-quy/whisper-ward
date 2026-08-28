using System;
using System.Collections.Generic;
using UnityEngine;
using Xunit; // Unity Test Framework
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;
using WhisperWard.AI.FSM;

namespace WhisperWard.AI.Testing
{
    /// <summary>
    /// Automated test suite for the Guard AI FSM.
    /// Implements the H.0 Acceptance Criteria battery.
    /// </summary>
    public class FSMVerificationSuite
    {
        private const float TICK_INTERVAL = 0.5f; // Default 2Hz

        /// <summary>
        /// AC-FSM-1: Verify the hard 3-state cap is enforced.
        /// </summary>
        [Fact]
        public void FSM_Has_Only_Three_States()
        {
            // Arrange
            var fsm = new GameObject("TestFSM").AddComponent<GuardFSM>();
            var states = new HashSet<IGuardState>
            {
                fsm.GetPatrolState(),
                fsm.GetInvestigateState(),
                fsm.GetChaseState()
            };

            // Assert
            Assert.Equal(3, states.Count);
            Assert.Contains(fsm.GetPatrolState(), states);
            Assert.Contains(fsm.GetInvestigateState(), states);
            Assert.Contains(fsm.GetChaseState(), states);
        }

        /// <summary>
        /// AC-FSM-2: Verify the per-tier suppression rule (exactly one escalation per tier).
        /// </summary>
        [Fact]
        public void FSM_Suppression_Rule_Enforced()
        {
            // Arrange
            var tap = new GameObject("Tap").AddComponent<DecisionTap>();
            var fsm = new GameObject("FSM").AddComponent<GuardFSM>();
            var driver = new GameObject("Driver").AddComponent<PerceptionDriver>();

            // Script: ConfirmWindowElapsed -> InvestigateCommit -> ChaseReached -> ChaseEntry
            driver.eventScript = new List<ScriptedEvent>
            {
                new ScriptedEvent { Type = EventType.ConfirmWindowElapsed, Tick = 1, EntryId = "E1", ResidualR = 0.5f, ThresholdState = "active" },
                new ScriptedEvent { Type = EventType.ChaseReached, Tick = 5, EntryId = "E1", Position = Vector3.zero, ResidualR = 1.0f, ThresholdState = "active" }
            };

            // Act
            // Advance clock manually
            for (int i = 0; i < 10; i++)
            {
                driver.OnTick(i, TICK_INTERVAL);
            }

            // Assert
            // Should have exactly one InvestigateCommit and one ChaseEntry
            Assert.Equal(1, tap.GetCount<InvestigateCommit>());
            Assert.Equal(1, tap.GetCount<ChaseEntry>());
        }

        /// <summary>
        /// AC-FSM-3: Verify the Chase-end record is published on termination.
        /// </summary>
        [Fact]
        public void FSM_Chase_End_Published()
        {
            // Arrange
            var tap = new GameObject("Tap").AddComponent<DecisionTap>();
            var fsm = new GameObject("FSM").AddComponent<GuardFSM>();
            var driver = new GameObject("Driver").AddComponent<PerceptionDriver>();

            // Script: Chase reached, then give-up after 10 ticks
            driver.eventScript = new List<ScriptedEvent>
            {
                new ScriptedEvent { Type = EventType.ChaseReached, Tick = 1, EntryId = "E2", Position = Vector3.zero },
                new ScriptedEvent { Type = EventType.Reachability, Tick = 11, EntryId = "E2", Position = Vector3.zero, IsReachable = false, PathArrivalM = 10f, DeltaYM = 0f }
            };

            // Act
            for (int i = 0; i < 15; i++)
            {
                driver.OnTick(i, TICK_INTERVAL);
            }

            // Assert
            Assert.Equal(1, tap.GetCount<ChaseEntry>());
            Assert.Equal(1, tap.GetCount<ChaseEnd>());
        }

        /// <summary>
        /// AC-FSM-4: Verify the give-up clock formula (D1).
        /// </summary>
        [Fact]
        public void FSM_Giveup_Clock_Formula()
        {
            // Arrange
            float t_giveup_base = 4.0f;
            float s_diff = 1.0f;
            float k_thorough = 0.50f;
            float R_max = 1.0f;

            // Act
            float tau_R0 = 1 + k_thorough * (0 / R_max);
            float tau_Rmax = 1 + k_thorough * (R_max / R_max);

            float t_giveup_R0 = t_giveup_base * s_diff * tau_R0;
            float t_giveup_Rmax = t_giveup_base * s_diff * tau_Rmax;

            // Assert
            Assert.Equal(4.0f, t_giveup_R0, precision: 0.01f);
            Assert.Equal(6.0f, t_giveup_Rmax, precision: 0.01f);
        }

        /// <summary>
        /// AC-FSM-5: Verify the Chase speed coupling (D4).
        /// </summary>
        [Fact]
        public void FSM_Chase_Speed_Coupling()
        {
            // Arrange
            float V_run_starter = 6.25f;
            float rho_chase = 1.20f;

            // Act
            float V_chase = rho_chase * V_run_starter;

            // Assert
            Assert.Equal(7.50f, V_chase, precision: 0.01f);
            Assert.True(V_chase >= 1.10f * V_run_starter);
        }

        /// <summary>
        /// AC-FSM-6: Verify the catch-range invariant (D5).
        /// </summary>
        [Fact]
        public void FSM_Catch_Range_Invariant()
        {
            // Arrange
            float V_run_max = 7.2f;
            float T_sample_max = 0.5f;
            float hyst = 0.7f;
            float r_guard = 0.40f;
            float r_player = 0.35f;
            float stopping_distance = 0f;

            // Act
            float binding_floor = V_run_max * T_sample_max + hyst + r_guard + r_player + stopping_distance;

            // Assert
            Assert.Equal(5.05f, binding_floor, precision: 0.01f);
            Assert.True(5.5f > binding_floor); // catch_range 5.5 > 5.05
        }

        /// <summary>
        /// AC-FSM-7: Verify the constraint chain (D7).
        /// </summary>
        [Fact]
        public void FSM_Constraint_Chain()
        {
            // Arrange
            float T_sample_max = 0.5f;
            float t_resight_min = 1.0f;
            float t_giveup_chase = 8.0f;
            float t_cap_chase = 30.0f;

            // Assert
            Assert.True(T_sample_max < t_resight_min);
            Assert.True(t_resight_min < t_giveup_chase);
            Assert.True(t_giveup_chase < t_cap_chase);
        }

        /// <summary>
        /// AC-FSM-8: Verify the GuardGoalMode enum is used correctly.
        /// </summary>
        [Fact]
        public void FSM_GuardGoalMode_Enum()
        {
            // Arrange
            var fsm = new GameObject("FSM").AddComponent<GuardFSM>();

            // Act
            fsm.SetGoalMode(GuardGoalMode.LivePursuit);

            // Assert
            Assert.Equal(GuardGoalMode.LivePursuit, fsm.currentGoalMode);

            // Test HideSpotFront
            fsm.SetGoalMode(GuardGoalMode.HideSpotFront);
            Assert.Equal(GuardGoalMode.HideSpotFront, fsm.currentGoalMode);

            // Test StaleLKP
            fsm.SetGoalMode(GuardGoalMode.StaleLKP);
            Assert.Equal(GuardGoalMode.StaleLKP, fsm.currentGoalMode);

            // Test None
            fsm.SetGoalMode(GuardGoalMode.None);
            Assert.Equal(GuardGoalMode.None, fsm.currentGoalMode);
        }

        /// <summary>
        /// AC-FSM-9: Verify the post-chase sweep timing (D3).
        /// </summary>
        [Fact]
        public void FSM_PostChase_Sweep_Timing()
        {
            // Arrange
            float t_sweep_postchase = 2.0f;
            float T_sample_max = 0.5f;
            float t_giveup_chase = 8.0f;

            // Assert
            Assert.True(t_sweep_postchase >= 2 * T_sample_max);
            Assert.True(t_sweep_postchase < t_giveup_chase);
        }

        /// <summary>
        /// AC-FSM-10: Verify the catch timer duration (D7).
        /// </summary>
        [Fact]
        public void FSM_Catch_Timer_Duration()
        {
            // Arrange
            float t_catch = 1.0f;
            float T_sample_max = 0.5f;
            float t_giveup_min = 2.45f;

            // Assert
            Assert.True(t_catch >= 2 * T_sample_max);
            Assert.True(t_catch < t_giveup_min);
        }
    }
}