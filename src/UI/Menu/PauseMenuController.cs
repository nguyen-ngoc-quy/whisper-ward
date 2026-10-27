using System;
using UnityEngine;
using WhisperWard.Core.Objectives;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WhisperWard.UI.Menu
{
    /// <summary>
    /// In-game Pause and Settings Menu controller conforming to Tuần 7 UI/UX standards.
    /// Manages game simulation freezing (Time.timeScale), Escape key toggle,
    /// and dispatches lifecycle events for Resume, Restart, and Quit.
    /// Zero managed heap allocations in hot-path.
    /// </summary>
    /// <example>
    /// <code>
    /// var pauseMenu = gameObject.AddComponent<PauseMenuController>();
    /// pauseMenu.OnPauseStateChanged += isPaused => Debug.Log($"Game paused: {isPaused}");
    /// </code>
    /// </example>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _pausePanel;

        [Header("State")]
        [SerializeField] private bool _isPaused;
        [SerializeField] private bool _allowPause = true;

        /// <summary>Dispatched when pause state changes (isPaused).</summary>
        public event Action<bool> OnPauseStateChanged;

        /// <summary>Dispatched when restart action is requested.</summary>
        public event Action OnRestartRequested;

        /// <summary>Dispatched when quit action is requested.</summary>
        public event Action OnQuitRequested;

        public bool IsPaused => _isPaused;
        public bool AllowPause { get => _allowPause; set => _allowPause = value; }
        public GameObject PausePanel { get => _pausePanel; set => _pausePanel = value; }

        private void Awake()
        {
            if (_pausePanel != null)
            {
                _pausePanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            MissionLifecycleController.OnAnyMissionStateChanged += HandleMissionStateChanged;
        }

        private void OnDisable()
        {
            MissionLifecycleController.OnAnyMissionStateChanged -= HandleMissionStateChanged;
            if (_isPaused)
            {
                Resume();
            }
        }

        private void Update()
        {
            if (_allowPause && IsEscapePressed())
            {
                TogglePause();
            }
        }

        /// <summary>
        /// Toggles pause state between paused and active.
        /// </summary>
        public void TogglePause()
        {
            if (_isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        /// <summary>
        /// Freezes game simulation and displays the pause panel.
        /// </summary>
        public void Pause()
        {
            if (_isPaused || !_allowPause) return;

            _isPaused = true;
            Time.timeScale = 0f;

            if (_pausePanel != null)
            {
                _pausePanel.SetActive(true);
            }

            OnPauseStateChanged?.Invoke(true);
        }

        /// <summary>
        /// Unfreezes game simulation and hides the pause panel.
        /// </summary>
        public void Resume()
        {
            if (!_isPaused) return;

            _isPaused = false;
            Time.timeScale = 1f;

            if (_pausePanel != null)
            {
                _pausePanel.SetActive(false);
            }

            OnPauseStateChanged?.Invoke(false);
        }

        /// <summary>
        /// Restores time and requests mission restart.
        /// </summary>
        public void RestartMission()
        {
            Resume();
            OnRestartRequested?.Invoke();
        }

        /// <summary>
        /// Restores time and requests application quit.
        /// </summary>
        public void QuitGame()
        {
            Resume();
            OnQuitRequested?.Invoke();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleMissionStateChanged(MissionLifecycleState newState)
        {
            // Disallow pause when mission reaches terminal state (Completed or Captured)
            if (newState == MissionLifecycleState.Completed || newState == MissionLifecycleState.Captured)
            {
                _allowPause = false;
                if (_isPaused)
                {
                    Resume();
                }
            }
        }

        /// <summary>
        /// Detects Escape key press with zero GC across both new Input System and legacy fallbacks.
        /// </summary>
        public bool IsEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.escapeKey.wasPressedThisFrame;
            }
            return false;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
