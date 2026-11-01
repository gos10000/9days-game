using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitBoxTrigger : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        Application.Quit();
    }
}
