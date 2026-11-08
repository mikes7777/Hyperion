using UnityEngine;

/// <summary>Bank-aware chase, close chase and bonnet cameras. C cycles the race views.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class StellarFollowCamera : MonoBehaviour
{
    [Header("Race chase")]
    [SerializeField] private float chaseDistance = 7.4f;
    [SerializeField] private float chaseHeight = 3.0f;
    [SerializeField] private float lookAhead = 8.5f;
    [SerializeField] private float positionSmoothTime = 0.085f;
    [SerializeField] private float rotationSharpness = 12f;
    [Header("Showroom")]
    [SerializeField] private float showroomDistance = 6.4f;
    [SerializeField] private float showroomHeight = 1.65f;
    [SerializeField] private float showroomOrbitSpeed = 7f;
    private Camera cameraComponent;
    private Material finishMaterial;
    private Transform target;
    private ArcadeCarController carController;
    private Rigidbody targetBody;
    private Vector3 positionVelocity, smoothedUp = Vector3.up, previousTargetPosition;
    private Vector3 travelDirection = Vector3.forward;
    private bool showroom = true;
    private float orbitAngle;
    private int raceView;
    public Transform Target { get { return target; } }
    public bool IsShowroom { get { return showroom; } }
    public string ViewName { get { return raceView == 0 ? "CHASE" : raceView == 1 ? "CLOSE" : "BONNET"; } }

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        cameraComponent.nearClipPlane = 0.08f;
        cameraComponent.farClipPlane = 6000f;
        cameraComponent.fieldOfView = 62f;
        cameraComponent.allowHDR = true;
        Shader finishShader = Resources.Load<Shader>("HyperionFinish");
        if (finishShader != null && finishShader.isSupported)
            finishMaterial = new Material(finishShader) { hideFlags = HideFlags.HideAndDontSave };
    }

    public void SetTarget(Transform newTarget, bool showroomMode)
    {
        target = newTarget;
        showroom = showroomMode;
        positionVelocity = Vector3.zero;
        carController = target != null ? target.GetComponent<ArcadeCarController>() : null;
        targetBody = target != null ? target.GetComponent<Rigidbody>() : null;
        if (target == null) return;
        smoothedUp = SafeUp();
        travelDirection = target.forward;
        previousTargetPosition = target.position;
        orbitAngle = Mathf.Atan2(target.forward.x, target.forward.z) * Mathf.Rad2Deg + 140f;
        SnapToCurrentMode();
    }

    public void SetShowroom(bool enabled)
    {
        if (showroom == enabled) return;
        showroom = enabled;
        if (target != null) SnapToCurrentMode();
    }

    public void SnapToCurrentMode()
    {
        if (target == null) return;
        positionVelocity = Vector3.zero;
        previousTargetPosition = target.position;
        smoothedUp = SafeUp();
        travelDirection = target.forward;
        CalculatePose(out Vector3 position, out Vector3 look, 0f);
        transform.position = position;
        transform.rotation = Quaternion.LookRotation((look - position).normalized, showroom ? Vector3.up : smoothedUp);
    }

    private void LateUpdate()
    {
        if (target == null) return;
        bool frozen = !showroom && carController != null && carController.CanDrive && targetBody != null && targetBody.isKinematic;
        if (frozen) return;
        if (!showroom && Input.GetKeyDown(KeyCode.C))
        {
            raceView = (raceView + 1) % 3;
            SnapToCurrentMode();
        }
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        // Warps and manual recovery should not send the chase camera flying across the circuit.
        if ((target.position - previousTargetPosition).sqrMagnitude > 24f * 24f) SnapToCurrentMode();
        previousTargetPosition = target.position;
        smoothedUp = Vector3.Slerp(smoothedUp, SafeUp(), 1f - Mathf.Exp(-5f * dt));
        CalculatePose(out Vector3 desiredPosition, out Vector3 lookPoint, dt);
        if (!showroom && raceView == 2) transform.position = desiredPosition;
        else transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity,
            showroom ? 0.24f : positionSmoothTime, Mathf.Infinity, dt);
        Vector3 direction = lookPoint - transform.position;
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion rotation = Quaternion.LookRotation(direction.normalized, showroom ? Vector3.up : smoothedUp);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation,
                1f - Mathf.Exp(-(showroom ? 5f : rotationSharpness) * dt));
        }
        float speedFactor = Mathf.InverseLerp(35f, 340f, GetSpeedKph());
        bool boosting = carController != null && carController.IsBoosting;
        float fov = showroom ? 49f : Mathf.Lerp(raceView == 2 ? 68f : 61f, raceView == 2 ? 84f : 78f, speedFactor) + (boosting ? 4f : 0f);
        cameraComponent.fieldOfView = Mathf.Lerp(cameraComponent.fieldOfView, fov, 1f - Mathf.Exp(-4.5f * dt));
        // A tiny angular vibration conveys thrust without moving the camera through the road.
        float vibration = !showroom && boosting ? 0.13f * StellarSettings.Shake : 0f;
        if (vibration > 0f)
            transform.rotation *= Quaternion.Euler(Mathf.Sin(Time.unscaledTime * 57f) * vibration,
                Mathf.Sin(Time.unscaledTime * 43f) * vibration, 0f);
    }

    private Vector3 SafeUp()
    {
        if (target == null || Vector3.Dot(target.up, Vector3.up) < 0.2f) return Vector3.up;
        // Retain a little horizon stability but follow the banking enough to keep the road readable.
        return Vector3.Slerp(Vector3.up, target.up, 0.72f).normalized;
    }

    private void CalculatePose(out Vector3 desiredPosition, out Vector3 lookPoint, float deltaTime)
    {
        if (showroom)
        {
            orbitAngle += showroomOrbitSpeed * deltaTime;
            float radians = orbitAngle * Mathf.Deg2Rad;
            desiredPosition = target.position + new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * showroomDistance + Vector3.up * showroomHeight;
            Vector3 center = target.position + Vector3.up * 0.05f;
            Vector3 screenRight = Vector3.Cross(Vector3.up, (center - desiredPosition).normalized).normalized;
            lookPoint = center - screenRight * 1.9f;
            return;
        }
        float speedFactor = Mathf.InverseLerp(25f, 340f, GetSpeedKph());
        Vector3 forward = target.forward;
        if (targetBody != null && targetBody.velocity.sqrMagnitude > 225f)
            forward = Vector3.Slerp(target.forward, targetBody.velocity.normalized, 0.16f).normalized;
        travelDirection = Vector3.Slerp(travelDirection, forward, 1f - Mathf.Exp(-8f * deltaTime));
        if (raceView == 2)
        {
            desiredPosition = target.position + target.up * 0.72f + target.forward * 0.9f;
            lookPoint = desiredPosition + target.forward * 35f;
            return;
        }
        float distance = (raceView == 1 ? 5.4f : chaseDistance) + speedFactor * 1.2f;
        float height = (raceView == 1 ? 1.85f : chaseHeight) + speedFactor * 0.2f;
        desiredPosition = target.position - travelDirection * distance + smoothedUp * height;
        lookPoint = target.position + forward * (lookAhead + speedFactor * 5f) + smoothedUp * 0.2f;
    }

    // Built-in pipeline only. Quarter-resolution bloom keeps the cost small and does not blur the HUD.
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (finishMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }
        if (!StellarSettings.HighQuality)
        {
            finishMaterial.SetTexture("_BloomTex", Texture2D.blackTexture);
            Graphics.Blit(source, destination, finishMaterial, 3);
            return;
        }
        int width = Mathf.Max(1, source.width / 4), height = Mathf.Max(1, source.height / 4);
        RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR)
            ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        RenderTexture a = null, b = null;
        try
        {
            a = RenderTexture.GetTemporary(width, height, 0, format);
            b = RenderTexture.GetTemporary(width, height, 0, format);
            a.filterMode = b.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, a, finishMaterial, 0);
            Graphics.Blit(a, b, finishMaterial, 1);
            Graphics.Blit(b, a, finishMaterial, 2);
            finishMaterial.SetTexture("_BloomTex", a);
            Graphics.Blit(source, destination, finishMaterial, 3);
        }
        finally
        {
            finishMaterial.SetTexture("_BloomTex", Texture2D.blackTexture);
            if (a != null) RenderTexture.ReleaseTemporary(a);
            if (b != null) RenderTexture.ReleaseTemporary(b);
        }
    }

    private void OnDestroy()
    {
        if (finishMaterial != null) Destroy(finishMaterial);
    }

    private float GetSpeedKph()
    {
        return carController != null ? carController.SpeedKph : targetBody != null ? targetBody.velocity.magnitude * 3.6f : 0f;
    }
}
