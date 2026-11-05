using UnityEngine;

/// <summary>
/// 按 R 把 Rock 重置回弹弓初始状态。
/// 挂在 Rock 物体上（和 rock.cs 同一个物体）。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class RockReset : MonoBehaviour
{
    [Header("重置按键")]
    public KeyCode resetKey = KeyCode.R;

    [Header("是否跟随弹弓锚点（玩家移动后也能正确复位）")]
    public bool followJointAnchor = true;

    [Header("指定复位点（留空则按上面两项自动决定）")]
    public Transform resetPoint;

    private Rigidbody2D rb;
    private SpringJoint2D joint;
    private Rock rockScript;

    private Vector3 homePosition;
    private float homeRotationZ;
    private RigidbodyType2D homeBodyType;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        joint = GetComponent<SpringJoint2D>();
        rockScript = GetComponent<Rock>();

        // 记录场景里摆放的初始状态
        homePosition = transform.position;
        homeRotationZ = transform.eulerAngles.z;
        homeBodyType = rb.bodyType;
    }

    void Update()
    {
        if (Input.GetKeyDown(resetKey))
            ResetRock();
    }

    public void ResetRock()
    {
        Vector3 pos = GetResetPosition();
        float rotZ = resetPoint != null ? resetPoint.eulerAngles.z : homeRotationZ;

        // 先恢复刚体类型，否则 Kinematic 状态下写速度会被忽略
        rb.bodyType = homeBodyType;

        // 用 Rigidbody2D 的接口移动，比改 transform 更可靠
        rb.position = new Vector2(pos.x, pos.y);
        rb.rotation = rotZ;

        // 清空速度，避免带着旧速度飞出去
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (joint != null)
            joint.enabled = true;

        // 交给 rock.cs 清理拖拽状态、并重新忽略与发射者的碰撞
        if (rockScript != null)
            rockScript.ResetRock();

        Debug.Log($"[RockReset] Rock 已重置到 {pos}");
    }

    Vector3 GetResetPosition()
    {
        if (resetPoint != null)
            return resetPoint.position;

        // 弹弓锚点会跟着玩家移动，所以优先回到锚点当前位置
        if (followJointAnchor && joint != null && joint.connectedBody != null)
            return joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);

        return homePosition;
    }
}
