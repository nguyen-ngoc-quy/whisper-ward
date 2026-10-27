using NUnit.Framework;
using UnityEngine;
using WhisperWard.AI.FSM;
using WhisperWard.Core.Rendering;

namespace WhisperWard.Tests.Unit.Rendering
{
    [TestFixture]
    public class StealthGameFeelTest
    {
        private GameObject _hostObj;
        private StealthGameFeelController _controller;

        [SetUp]
        public void SetUp()
        {
            _hostObj = new GameObject("StealthGameFeel_TestHost");
            _controller = _hostObj.AddComponent<StealthGameFeelController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hostObj != null)
            {
                Object.DestroyImmediate(_hostObj);
            }
        }

        [Test]
        public void test_stealth_game_feel_guard_chase_dispatches_camera_shake_request()
        {
            // Arrange
            float capturedIntensity = 0f;
            float capturedDuration = 0f;
            _controller.OnCameraShakeRequested += (intensity, duration) =>
            {
                capturedIntensity = intensity;
                capturedDuration = duration;
            };

            // Act - transition to Chase
            _controller.HandleGuardStateChanged(GuardState.Patrol, GuardState.Chase);

            // Assert
            Assert.AreEqual(0.40f, capturedIntensity, 0.001f);
            Assert.AreEqual(0.60f, capturedDuration, 0.001f);
        }

        [Test]
        public void test_stealth_game_feel_guard_investigate_does_not_shake_camera()
        {
            // Arrange
            bool shakeTriggered = false;
            _controller.OnCameraShakeRequested += (intensity, duration) => shakeTriggered = true;

            // Act - Patrol to Investigate
            _controller.HandleGuardStateChanged(GuardState.Patrol, GuardState.Investigate);

            // Assert
            Assert.IsFalse(shakeTriggered);
        }

        [Test]
        public void test_stealth_game_feel_hiding_modulates_vignette_focus_depth()
        {
            // Arrange
            float targetVignette = 0f;
            _controller.OnHidingVignetteFocus += v => targetVignette = v;

            // Act - Enter hiding
            _controller.HandleHidingStateChanged(true);
            Assert.AreEqual(0.55f, targetVignette, 0.001f);

            // Act - Exit hiding
            _controller.HandleHidingStateChanged(false);
            Assert.AreEqual(0.35f, targetVignette, 0.001f);
        }

        [Test]
        public void test_acoustic_ripple_telemetry_evaluates_correct_radius_and_alpha_fade()
        {
            // Arrange
            float maxRadius = 10.0f;
            float lifetime = 2.0f;

            // Act & Assert at t = 0
            AcousticRippleData dataStart = AcousticRippleRingTelegraph.Evaluate(0.0f, maxRadius, lifetime);
            Assert.AreEqual(0.0f, dataStart.Radius, 0.001f);
            Assert.AreEqual(1.0f, dataStart.Alpha, 0.001f);

            // Act & Assert at t = 1.0 (midway)
            AcousticRippleData dataMid = AcousticRippleRingTelegraph.Evaluate(1.0f, maxRadius, lifetime);
            Assert.AreEqual(5.0f, dataMid.Radius, 0.001f);
            Assert.AreEqual(0.5f, dataMid.Alpha, 0.001f);

            // Act & Assert at t = 2.0 (end)
            AcousticRippleData dataEnd = AcousticRippleRingTelegraph.Evaluate(2.0f, maxRadius, lifetime);
            Assert.AreEqual(10.0f, dataEnd.Radius, 0.001f);
            Assert.AreEqual(0.0f, dataEnd.Alpha, 0.001f);
        }
    }
}
