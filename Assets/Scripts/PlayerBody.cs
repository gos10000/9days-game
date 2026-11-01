using System.Collections;
using UnityEngine;

public class PlayerBody : MonoBehaviour
{
    private bool isPressed;
    public Rigidbody2D rb;
    public float releaseDelay = 0.1f;

    void OnMouseDown()
    {
        isPressed = true;
    }

    IEnumerator Release()
    {
        yield return new WaitForSeconds(releaseDelay);

        rb.bodyType = RigidbodyType2D.Dynamic;
    }

    void OnMouseUp()
    {
        isPressed = false;

        StartCoroutine(Release());
    }
}
