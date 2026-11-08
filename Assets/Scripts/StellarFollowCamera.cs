using UnityEngine;

/// <summary>
/// Smooth showroom and race camera used by Hyperion. The component is
/// intentionally prefab-free: the bootstrap can add it to any Camera at run time.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class StellarFollowCamera : MonoBehaviour
{
    [Header("Race chase")]
    [SerializeField] private float chaseDistance = 8.2f;
    [SerializeField] private float chaseHeight = 3.7f;
    [SerializeField] private float lookAhead = 7.5f;
    [SerializeField] private float positionSmoothTime = 0.13f;
    [SerializeField] private float rotationSharpness = 9.5f;

    [Header("Showroom")]
    [SerializeField] private float showroomDistance = 7.5f;
    [SerializeField] private float showroomHeight = 2.2f;
    [SerializeField] private float showroomOrbitSpeed = 11f;

    private Camera cameraComponent;
    private Transform target;
    private ArcadeCarController carController;
    private Rigidbody targetBody;
    private Vector3 positionVelocity;
    private bool showroom = true;
    private float orbitAngle;

    public Transform Target { get { return target; } }
    public bool IsShowroom { get { return showroom; } }

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        cameraComponent.nearClipPlane = 0.08f;
        cameraComponent.farClipPlane = 1800f;
        cameraComponent.fieldOfView = 64f;
    }

    public void SetTarget(Transform newTarget, bool showroomMode)
    {
        target = newTarget;
        showroom = showroomMode;
        positionVelocity = Vector3.zero;

        carController = target != null ? target.GetComponent<ArcadeCarController>() : null;
        targetBody = target != null ? target.GetComponent<Rigidbody>() : null;

        if (target != null)
        {
            Vector3 flattenedForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
            if (flattenedForward.sqrMagnitude < 0.001f)
                flattenedForward = Vector3.forward;

            orbitAngle = Mathf.Atan2(flattenedForward.x, flattenedForward.z) * Mathf.Rad2Deg + 135f;
            SnapToCurrentMode();
        }
    }

    public void SetShowroom(bool enabled)
    {
        if (showroom == enabled)
            return;

        showroom = enabled;
        positionVelocity = Vector3.zero;
        if (target != null && enabled)
            orbitAngle = Mathf.Atan2(transform.position.x - target.position.x,
                                    transform.position.z - target.position.z) * Mathf.Rad2Deg;
    }

    public void SnapToCurrentMode()
    {
        if (target == null)
            return;

        Vector3 desiredPosition;
        Vector3 lookPoint;
        CalculatePose(out desiredPosition, out lookPoint, 0f);
        transform.position = desiredPosition;
        transform.rotation = Quaternion.LookRotation((lookPoint - desiredPosition).normalized, Vector3.up);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        float unscaledDelta = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        Vector3 desiredPosition;
        Vector3 lookPoint;
        CalculatePose(out desiredPosition, out lookPoint, unscaledDelta);

        float smoothTime = showroom ? 0.28f : positionSmoothTime;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            smoothTime,
            Mathf.Infinity,
            unscaledDelta);

        Vector3 lookDirection = lookPoint - transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            float blend = 1f - Mathf.Exp(-(showroom ? 5f : rotationSharpness) * unscaledDelta);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
        }

        float speedKph = GetSpeedKph();
        float normalizedSpeed = Mathf.InverseLerp(40f, 355f, speedKph);
        float targetFov = showroom ? 56f : Mathf.Lerp(62f, 76f, normalizedSpeed);
        cameraComponent.fieldOfView = Mathf.Lerp(
            cameraComponent.fieldOfView,
            targetFov,
            1f - Mathf.Exp(-4.5f * unscaledDelta));
    }

    private void CalculatePose(out Vector3 desiredPosition, out Vector3 lookPoint, float deltaTime)
    {
        if (showroom)
        {
            orbitAngle += showroomOrbitSpeed * deltaTime;
            float radians = orbitAngle * Mathf.Deg2Rad;
            Vector3 orbitOffset = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * showroomDistance;
            desiredPosition = target.position + orbitOffset + Vector3.up * showroomHeight;
            lookPoint = target.position + Vector3.up * 0.65f;
            return;
        }

        Vector3 targetUp = target.up;
        if (Vector3.Dot(targetUp, Vector3.up) < 0.25f)
            targetUp = Vector3.up;

        float speedFactor = Mathf.InverseLerp(25f, 340f, GetSpeedKph());
        float dynamicDistance = chaseDistance + speedFactor * 1.7f;
        float dynamicHeight = chaseHeight + speedFactor * 0.45f;

        desiredPosition = target.position - target.forward * dynamicDistance + targetUp * dynamicHeight;
        lookPoint = target.position + target.forward * (lookAhead + speedFactor * 3f) + targetUp * 0.75f;
    }

    private float GetSpeedKph()
    {
        if (carController != null)
            return carController.SpeedKph;

        if (targetBody != null)
            return targetBody.velocity.magnitude * 3.6f;

        return 0f;
    }
}
