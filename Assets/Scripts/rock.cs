using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;

public class Rock : MonoBehaviour
{
    private bool isPressed;
    public Rigidbody2D rb;
    public float releaseDelay = 0.1f;
    public float crashDelay = 0.5f;
    private bool isCrashable=false;

    [Header("发射者")]
    public Transform launcher;

    private Collider2D myCollider;
    private Collider2D[] launcherColliders;
    private bool isIgnoringLauncher;

    void Awake()
    {
        myCollider = GetComponent<Collider2D>();

        SetIgnoreLauncher(true);
    }

    void Update()
    {
        if (isPressed)
        {
            Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            rb.position = mousePosition;
        }
    }

    void FixedUpdate()
    {
        if (isIgnoringLauncher && isCrashable)
            CheckSeparatedFromLauncher();
    }


    void OnMouseDown()
    {
        rb.bodyType = RigidbodyType2D.Kinematic;
        isPressed = true;
    }

    void OnMouseUp()
    {
        // 拖拽途中被 R 重置过（isPressed 已被清掉），忽略这次松开
        if (!isPressed) return;

        isPressed = false;
        rb.bodyType = RigidbodyType2D.Dynamic;

        StartCoroutine(Release());
        StartCoroutine(Crash());
    }

    IEnumerator Release()
    {
        yield return new WaitForSeconds(releaseDelay);

        GetComponent<SpringJoint2D>().enabled = false;
    }

    IEnumerator Crash()
    {
        yield return new WaitForSeconds(crashDelay);

        isCrashable = true;
    }

    //忽略/恢复与发射者所有碰撞体的碰撞
    void SetIgnoreLauncher(bool ignore)
    {
        if (launcher == null || myCollider == null) return;

        if (launcherColliders == null)
            launcherColliders = launcher.GetComponentsInChildren<Collider2D>(true);

        foreach (var col in launcherColliders)
        {
            if (col == null || col == myCollider) continue;
            Physics2D.IgnoreCollision(myCollider, col, ignore);
        }

        if (isIgnoringLauncher != ignore)
            Debug.Log($"[rock] IgnoreCollision -> {ignore}（{launcherColliders.Length} 个碰撞体）");

        isIgnoringLauncher = ignore;
    }

    //恢复球的状态，位置和旋转由RockReset负责，这里只清状态
    public void ResetRock()
    {
        isPressed = false;
        isCrashable = false;   //回到“待命”，下次发射重新计时

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        var joint = GetComponent<SpringJoint2D>();
        if (joint != null) joint.enabled = true;

        //回到玩家身上，重新忽略碰撞
        SetIgnoreLauncher(true);
    }

    //一旦和发射者完全分开，就恢复碰撞
    void CheckSeparatedFromLauncher()
    {
        if (myCollider == null || launcherColliders == null)
        {
            SetIgnoreLauncher(false);
            return;
        }

        foreach (var col in launcherColliders)
        {
            if (col == null || col == myCollider) continue;
            if (!col.enabled || !col.gameObject.activeInHierarchy) continue;

            if (Overlaps2D(myCollider.bounds, col.bounds))
                return; //还有重叠，继续忽略
        }

        SetIgnoreLauncher(false);
    }

    //只比较xy两个轴的重叠
    static bool Overlaps2D(Bounds a, Bounds b)
    {
        return a.min.x < b.max.x && a.max.x > b.min.x &&
               a.min.y < b.max.y && a.max.y > b.min.y;
    }
}
