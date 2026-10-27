using NUnit.Framework;
using UnityEngine;
using WhisperWard.AI.FSM;
using WhisperWard.AI.Perception;
using WhisperWard.Core.Rendering;
using WhisperWard.UI.Menu;

namespace WhisperWard.Tests.Unit.Integration
{
    [TestFixture]
    public class PlaygroundFeatureIntegrationTest
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("PlaygroundIntegration_Root");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void test_playground_all_expansion_systems_cross_wire_cleanly_without_exceptions()
        {
            // 1. Settings Menu & Audio
            var uiHost = new GameObject("UI_Host");
            uiHost.transform.SetParent(_root.transform);
            var settingsMenu = uiHost.AddComponent<SettingsMenuController>();
            settingsMenu.SetMasterVolume(0.75f);
            Assert.AreEqual(0.75f, settingsMenu.CurrentSettings.MasterVolume, 0.001f);

            // 2. Security Camera
            var camHost = new GameObject("Camera_Host");
            camHost.transform.SetParent(_root.transform);
            var cameraSensor = camHost.AddComponent<SecurityCameraSensor>();
            var cameraVisualizer = camHost.AddComponent<SecurityCameraVisualizer>();
            cameraSensor.ConfigureSweep(0f, 45f, 30f);
            cameraSensor.SimulateTick(0.1f);
            Assert.AreEqual(SecurityCameraState.IdleSweeping, cameraSensor.State);

            // 3. Game Feel & Post-Process
            var feelHost = new GameObject("Feel_Host");
            feelHost.transform.SetParent(_root.transform);
            var feelController = feelHost.AddComponent<StealthGameFeelController>();
            bool shakeTriggered = false;
            feelController.OnCameraShakeRequested += (i, d) => shakeTriggered = true;
            feelController.HandleGuardStateChanged(GuardState.Patrol, GuardState.Chase);
            Assert.IsTrue(shakeTriggered);

            // 4. Volumetric Light Cone
            Mesh coneMesh = VolumetricConeMeshBuilder.BuildConeMesh(60f, 8f, 12);
            Assert.IsNotNull(coneMesh);
            Assert.IsTrue(coneMesh.vertexCount > 0);
        }
    }
}
