using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineDays.Tests
{
    /// <summary>相机执行层与目标管理的最小 PlayMode 集成测试。</summary>
    public class CameraRigTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [UnityTest]
        public IEnumerator SetPose_SnapMovesCameraImmediately()
        {
            var cameraGo = new GameObject("TestCamera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            created.Add(cameraGo);

            var rig = cameraGo.AddComponent<CameraRig>();
            rig.SetPose(new CameraPose(new Vector2(3f, 2f), 4f), true);

            yield return null;

            Assert.AreEqual(3f, cameraGo.transform.position.x, 0.001f);
            Assert.AreEqual(2f, cameraGo.transform.position.y, 0.001f);
            Assert.AreEqual(-10f, cameraGo.transform.position.z, 0.001f);
            Assert.AreEqual(4f, camera.orthographicSize, 0.001f);
        }

        [UnityTest]
        public IEnumerator Director_FramesTargetsAndReturnsToOverview()
        {
            var boundsGo = new GameObject("TestBounds");
            boundsGo.AddComponent<ArenaBounds>();
            created.Add(boundsGo);

            var cameraGo = new GameObject("TestCamera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            created.Add(cameraGo);

            var rig = cameraGo.AddComponent<CameraRig>();
            var director = cameraGo.AddComponent<CameraDirector>();

            var targetA = new GameObject("TargetA");
            targetA.transform.position = new Vector2(-2f, 0f);
            var targetB = new GameObject("TargetB");
            targetB.transform.position = new Vector2(2f, 0f);
            created.Add(targetA);
            created.Add(targetB);

            yield return null; // 等 CameraDirector.Start 的全景初始化

            Assert.IsTrue(director.IsOverview);

            director.SetTargets(new[] { targetA.transform, targetB.transform }, true);
            yield return null;

            Assert.IsFalse(director.IsOverview);
            Assert.AreEqual(2, director.TargetCount);
            Assert.LessOrEqual(rig.CurrentPose.OrthographicSize, 5.5f);

            director.ShowOverview(true);
            yield return null;

            Assert.IsTrue(director.IsOverview);
        }

        [UnityTest]
        public IEnumerator AddShake_RaisesTrauma()
        {
            var cameraGo = new GameObject("TestCamera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            created.Add(cameraGo);

            var rig = cameraGo.AddComponent<CameraRig>();
            rig.SetPose(new CameraPose(Vector2.zero, 5f), true);

            rig.AddShake(1f);
            Assert.Greater(rig.Trauma, 0f);

            yield return null;
            Assert.Greater(rig.Trauma, 0f);
        }
    }
}
