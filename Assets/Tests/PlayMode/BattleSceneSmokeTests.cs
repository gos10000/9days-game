using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NineDays.Tests.PlayMode
{
    /// <summary>Battle Scene 集成冒烟：默认关卡 + UIRoot + 首回合选弹页。</summary>
    public class BattleSceneSmokeTests
    {
        [UnityTest]
        public IEnumerator BattleScene_OpensAmmoSelectPage()
        {
            SceneManager.LoadScene("Battle Scene", LoadSceneMode.Additive);
            yield return null;
            yield return null;

            AmmoSelectPage page = UnityEngine.Object.FindAnyObjectByType<AmmoSelectPage>(
                FindObjectsInactive.Include);
            Assert.IsNotNull(page, "首回合没有打开选弹页");
            Assert.IsTrue(page.IsVisible, "选弹页存在但未显示");

            Transform optionRoot = page.transform.Find("OptionRoot");
            Assert.IsNotNull(optionRoot, "选弹页缺少 OptionRoot");
            Assert.GreaterOrEqual(optionRoot.childCount, 1, "选弹页没有生成选项按钮");

            Scene scene = SceneManager.GetSceneByName("Battle Scene");
            if (scene.IsValid() && scene.isLoaded)
                SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }
    }
}
