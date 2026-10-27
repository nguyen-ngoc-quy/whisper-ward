using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Objectives;
using WhisperWard.UI.Menu;

namespace WhisperWard.Tests.Unit.UI
{
    [TestFixture]
    public class PauseMenuControllerTest
    {
        private GameObject _hostObj;
        private GameObject _panelObj;
        private PauseMenuController _controller;

        [SetUp]
        public void SetUp()
        {
            _hostObj = new GameObject("PauseMenu_TestHost");
            _panelObj = new GameObject("PausePanel");
            _panelObj.transform.SetParent(_hostObj.transform);

            _controller = _hostObj.AddComponent<PauseMenuController>();
            _controller.PausePanel = _panelObj;

            Time.timeScale = 1.0f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1.0f;

            if (_hostObj != null)
            {
                Object.DestroyImmediate(_hostObj);
            }
        }

        [Test]
        public void test_pause_menu_toggles_pause_state_and_timescale()
        {
            // Arrange
            bool eventFired = false;
            bool recordedState = false;
            _controller.OnPauseStateChanged += isPaused =>
            {
                eventFired = true;
                recordedState = isPaused;
            };

            // Act 1: Pause game
            _controller.Pause();

            // Assert
            Assert.IsTrue(_controller.IsPaused);
            Assert.AreEqual(0.0f, Time.timeScale, 0.001f);
            Assert.IsTrue(_panelObj.activeSelf);
            Assert.IsTrue(eventFired);
            Assert.IsTrue(recordedState);

            // Act 2: Resume game
            eventFired = false;
            _controller.Resume();

            // Assert
            Assert.IsFalse(_controller.IsPaused);
            Assert.AreEqual(1.0f, Time.timeScale, 0.001f);
            Assert.IsFalse(_panelObj.activeSelf);
            Assert.IsTrue(eventFired);
            Assert.IsFalse(recordedState);
        }

        [Test]
        public void test_pause_menu_resume_restores_normal_timescale()
        {
            // Arrange
            _controller.Pause();
            Assert.AreEqual(0.0f, Time.timeScale, 0.001f);

            // Act
            _controller.Resume();

            // Assert
            Assert.IsFalse(_controller.IsPaused);
            Assert.AreEqual(1.0f, Time.timeScale, 0.001f);
            Assert.IsFalse(_panelObj.activeSelf);
        }

        [Test]
        public void test_pause_menu_restart_dispatches_restart_event()
        {
            // Arrange
            bool restartFired = false;
            _controller.OnRestartRequested += () => restartFired = true;
            _controller.Pause();

            // Act
            _controller.RestartMission();

            // Assert: Resumed timescale and dispatched restart event
            Assert.IsFalse(_controller.IsPaused);
            Assert.AreEqual(1.0f, Time.timeScale, 0.001f);
            Assert.IsTrue(restartFired);
        }

        [Test]
        public void test_pause_menu_inactive_when_game_over()
        {
            // Arrange
            _controller.AllowPause = false;

            // Act: Try to pause when disallowed
            _controller.Pause();

            // Assert: Did not freeze or show panel
            Assert.IsFalse(_controller.IsPaused);
            Assert.AreEqual(1.0f, Time.timeScale, 0.001f);
            Assert.IsFalse(_panelObj.activeSelf);
        }
    }
}
