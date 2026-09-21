using System;
using NUnit.Framework;
using UnityEngine;
using WhisperWard.Foundation.Input;

namespace WhisperWard.Tests.EditMode.Foundation
{
    public sealed class InputSystemTests
    {
        [Test]
        public void test_radial_deadzone_below_inner_threshold_returns_zero()
        {
            // Arrange: Vector with magnitude = 0.08 (< 0.10 inner deadzone)
            var rawInput = new Vector2(0.06f, 0.0529f); // sqrt(0.0036 + 0.0028) approx 0.08
            Assert.That(rawInput.magnitude, Is.LessThan(0.10f));

            // Act
            Vector2 result = RadialDeadzoneProcessor.Process(rawInput);

            // Assert
            Assert.That(result, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void test_radial_deadzone_above_outer_threshold_clamps_magnitude_to_one()
        {
            // Arrange: Vector with magnitude = 1.20 (> 0.95 outer deadzone)
            var rawInput = new Vector2(1.20f, 0f);

            // Act
            Vector2 result = RadialDeadzoneProcessor.Process(rawInput);

            // Assert
            Assert.That(result.magnitude, Is.EqualTo(1.0f).Within(1e-5f));
            Assert.That(result.x, Is.EqualTo(1.0f).Within(1e-5f));
            Assert.That(result.y, Is.EqualTo(0f));
        }

        [Test]
        public void test_radial_deadzone_between_thresholds_scales_linearly()
        {
            // Arrange: Midpoint between 0.10 and 0.95: (0.10 + 0.95) / 2 = 0.525
            const float midMagnitude = 0.525f;
            var rawInput = new Vector2(0f, midMagnitude);

            // Act
            Vector2 result = RadialDeadzoneProcessor.Process(rawInput);

            // Assert: Normalized magnitude should be exactly 0.50
            Assert.That(result.magnitude, Is.EqualTo(0.50f).Within(1e-4f));
            Assert.That(result.y, Is.EqualTo(0.50f).Within(1e-4f));
        }

        [Test]
        public void test_input_buffer_service_action_consumed_within_window()
        {
            // Arrange
            var buffer = new InputBufferService();
            const double recordTime = 1.00;
            const double queryTime = 1.10; // Delta = 0.10s <= 0.15s window

            buffer.BufferAction(BufferedActionType.Crouch, recordTime);

            // Act
            bool consumed = buffer.TryConsumeAction(BufferedActionType.Crouch, queryTime);

            // Assert
            Assert.That(consumed, Is.True);
        }

        [Test]
        public void test_input_buffer_service_cannot_consume_action_twice()
        {
            // Arrange
            var buffer = new InputBufferService();
            buffer.BufferAction(BufferedActionType.Throw, 1.00);

            // Act: First consumption succeeds
            bool firstConsume = buffer.TryConsumeAction(BufferedActionType.Throw, 1.08);

            // Second consumption on next tick fails
            bool secondConsume = buffer.TryConsumeAction(BufferedActionType.Throw, 1.09);

            // Assert
            Assert.That(firstConsume, Is.True);
            Assert.That(secondConsume, Is.False);
        }

        [Test]
        public void test_input_buffer_service_expired_action_returns_false()
        {
            // Arrange: Request older than 150ms (0.15s)
            var buffer = new InputBufferService();
            const double recordTime = 2.00;
            const double queryTime = 2.16; // Delta = 0.16s > 0.15s

            buffer.BufferAction(BufferedActionType.Crouch, recordTime);

            // Act
            bool consumed = buffer.TryConsumeAction(BufferedActionType.Crouch, queryTime);

            // Assert
            Assert.That(consumed, Is.False);
        }

        [Test]
        public void test_input_system_switching_to_ui_map_flushes_movement_and_look()
        {
            // Arrange
            var service = new InputSystemService();
            service.SetRawMoveInput(new Vector2(0.8f, 0.6f));
            service.SetRawLookDelta(new Vector2(15f, -5f));
            service.SetSprintHeld(true);

            Assert.That(service.MoveInput.magnitude, Is.GreaterThan(0f));
            Assert.That(service.IsSprintHeld, Is.True);

            // Act: Switch to UI map
            service.SwitchToUIMap();

            // Assert: All motion and look vectors immediately flushed to zero
            Assert.That(service.IsPlayerMapActive, Is.False);
            Assert.That(service.IsUIMapActive, Is.True);
            Assert.That(service.MoveInput, Is.EqualTo(Vector2.zero));
            Assert.That(service.LookDelta, Is.EqualTo(Vector2.zero));
            Assert.That(service.IsSprintHeld, Is.False);
        }

        [Test]
        public void test_input_system_pause_automatically_switches_to_ui_and_flushes()
        {
            // Arrange
            var service = new InputSystemService();
            service.SetRawMoveInput(new Vector2(0.5f, 0.5f));

            // Act: Trigger pause
            service.TriggerPause();

            // Assert
            Assert.That(service.PauseTriggered, Is.True);
            Assert.That(service.IsUIMapActive, Is.True);
            Assert.That(service.MoveInput, Is.EqualTo(Vector2.zero));
        }
    }
}
