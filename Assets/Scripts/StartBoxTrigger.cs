using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartBoxTrigger : MonoBehaviour
{
    [Header("目标场景")]
    public string targetScene;

    private bool isLoading;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isLoading) return;
        
        isLoading = true;
        SceneManager.LoadScene(targetScene);
    }
}
