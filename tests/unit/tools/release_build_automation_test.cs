#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using WhisperWard.Editor;

namespace WhisperWard.Tests.Unit.Tools
{
    [TestFixture]
    public class ReleaseBuildAutomationTest
    {
        [Test]
        public void test_release_builder_validates_scene_inclusion_list()
        {
            // Case 1: Valid list containing CorePlayground -> Passes
            string[] validScenes = new[] { "Assets/Scenes/CorePlayground.unity" };
            bool passValid = ReleaseBuildAutomation.ValidateSceneList(validScenes, out string err1);
            Assert.IsTrue(passValid, err1);

            // Case 2: Empty list -> Fails
            bool failEmpty = ReleaseBuildAutomation.ValidateSceneList(new string[0], out string err2);
            Assert.IsFalse(failEmpty);
            Assert.IsNotEmpty(err2);

            // Case 3: List missing CorePlayground -> Fails
            string[] wrongScenes = new[] { "Assets/Scenes/TestScene.unity" };
            bool failWrong = ReleaseBuildAutomation.ValidateSceneList(wrongScenes, out string err3);
            Assert.IsFalse(failWrong);
            Assert.IsNotEmpty(err3);
        }

        [Test]
        public void test_release_builder_configures_windows_standalone_target()
        {
            // Act
            BuildPlayerOptions options = ReleaseBuildAutomation.GetWindowsBuildOptions("Builds/Test/Test.exe");

            // Assert
            Assert.AreEqual(BuildTarget.StandaloneWindows64, options.target);
            Assert.AreEqual("Builds/Test/Test.exe", options.locationPathName);
            Assert.Contains(ReleaseBuildAutomation.DefaultScenePath, options.scenes);
        }

        [Test]
        public void test_release_builder_configures_webgl_target_settings()
        {
            // Act
            BuildPlayerOptions options = ReleaseBuildAutomation.GetWebGLBuildOptions("Builds/TestWebGL");

            // Assert
            Assert.AreEqual(BuildTarget.WebGL, options.target);
            Assert.AreEqual("Builds/TestWebGL", options.locationPathName);
            Assert.Contains(ReleaseBuildAutomation.DefaultScenePath, options.scenes);
        }
    }
}
#endif
