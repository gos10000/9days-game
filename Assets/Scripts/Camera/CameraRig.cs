using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 相机执行层：平滑追目标姿态，并在最终位置叠加 trauma 震动。
    /// 不决定看谁；目标由 CameraDirector 提供。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class CameraRig : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Camera targetCamera;

        [Header("平滑")]
        [SerializeField] private float positionSmoothTime = 0.25f;
        [SerializeField] private float sizeSmoothTime = 0.35f;
        [SerializeField] private bool useUnscaledTime;

        [Header("震动")]
        [SerializeField] private float shakeAmplitude = 0.25f;
        [SerializeField] private float shakeFrequency = 18f;
        [SerializeField] private float shakeDecay = 1.5f;
        [Range(0f, 1f)] [SerializeField] private float shakeScale = 1f;

        private float baseZ;
        private Vector2 currentPosition;
        private Vector2 targetPosition;
        private Vector2 positionVelocity;
        private float currentSize;
        private float targetSize;
        private float sizeVelocity;
        private float trauma;

        public Camera TargetCamera => targetCamera;
        public CameraPose CurrentPose => new CameraPose(currentPosition, currentSize);
        public float Trauma => trauma;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
            {
                Debug.LogError("[Camera] CameraRig 缺少 Camera");
                enabled = false;
                return;
            }

            targetCamera.orthographic = true;
            baseZ = transform.position.z;
            currentPosition = transform.position;
            targetPosition = currentPosition;
            currentSize = targetCamera.orthographicSize;
            targetSize = currentSize;
            ApplyToCamera(0f);
        }

        /// <summary>设置目标姿态；snap 为 true 时立即到位。</summary>
        public void SetPose(CameraPose pose, bool snap = false)
        {
            targetPosition = pose.Center;
            targetSize = Mathf.Max(0.01f, pose.OrthographicSize);

            if (snap)
            {
                currentPosition = targetPosition;
                targetSize = Mathf.Max(0.01f, pose.OrthographicSize);
                currentSize = targetSize;
                positionVelocity = Vector2.zero;
                sizeVelocity = 0f;
                ApplyToCamera(0f);
            }
        }

        /// <summary>增加 0~1 的震动强度。</summary>
        public void AddShake(float strength)
        {
            trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, strength));
        }

        /// <summary>以后接玩家设置里的“屏幕震动强度”。</summary>
        public void SetShakeScale(float scale)
        {
            shakeScale = Mathf.Clamp01(scale);
        }

        private void LateUpdate()
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (deltaTime <= 0f) return;

            currentPosition = Vector2.SmoothDamp(currentPosition, targetPosition, ref positionVelocity,
                                                 Mathf.Max(0f, positionSmoothTime), Mathf.Infinity, deltaTime);
            currentSize = Mathf.SmoothDamp(currentSize, targetSize, ref sizeVelocity,
                                           Mathf.Max(0f, sizeSmoothTime), Mathf.Infinity, deltaTime);
            ApplyToCamera(deltaTime);
        }

        private void ApplyToCamera(float deltaTime)
        {
            if (targetCamera == null) return;

            trauma = Mathf.Max(0f, trauma - Mathf.Max(0f, shakeDecay) * deltaTime);
            float shake = trauma * trauma * shakeScale;
            float time = useUnscaledTime ? Time.unscaledTime : Time.time;

            Vector2 offset = Vector2.zero;
            if (shake > 0f)
            {
                float x = (Mathf.PerlinNoise(time * shakeFrequency, 0.1f) * 2f - 1f) * shakeAmplitude * shake;
                float y = (Mathf.PerlinNoise(0.3f, time * shakeFrequency) * 2f - 1f) * shakeAmplitude * shake;
                offset = new Vector2(x, y);
            }

            transform.position = new Vector3(currentPosition.x + offset.x, currentPosition.y + offset.y, baseZ);
            targetCamera.orthographicSize = currentSize;
        }
    }
}
