using UnityEngine;
using Unity.Cinemachine;
using Vector3 = UnityEngine.Vector3;

public class CameraFraming : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform followCam;

    [Space]
    [Header("Dynamic Camera Zoom")]
    [SerializeField] private CinemachineCamera virtualCam;
    [SerializeField] private float normalCamRadius = 3f;
    [SerializeField] private float pullBackCamRadius = 6f;
    [SerializeField] private float zoomSmoothTime = 0.5f;
    private float zoomVelocity;

    [Space]
    [Header("Wall Collision Crane")]
    [SerializeField] private Transform cameraFollowTarget;
    [SerializeField] private float maxCraneHeight = 2.5f;
    [SerializeField] private float wallCheckDistance = 3f;
    [SerializeField] private LayerMask cameraObstacleMask;

    private float defaultTargetY;
    private float craneVelocity;
    private float craneTargetY;

    private CinemachineOrbitalFollow orbitalFollow;

    void Start()
    {
        if (orbitalFollow == null)
        {
            orbitalFollow = virtualCam.GetComponent<CinemachineOrbitalFollow>();
        }

        if (cameraFollowTarget != null)
        {
            defaultTargetY = cameraFollowTarget.localPosition.y;
            craneTargetY = defaultTargetY;
        }
    }

    public void UpdateZoom(bool isMoving)
    {
        if (orbitalFollow == null)
        {
            Debug.LogWarning("Camera Zoom: Orbital Follow component is missing or not assigned!");
            return;
        }

        float viewAngle = Vector3.Dot(transform.forward, followCam.forward);

        float targetRadius;
        if (isMoving && viewAngle < -0.2f)
        {
            targetRadius = pullBackCamRadius;
        }
        else
        {
            targetRadius = normalCamRadius;
        }

        orbitalFollow.Radius = Mathf.SmoothDamp(orbitalFollow.Radius, targetRadius, ref zoomVelocity, zoomSmoothTime);
    }

    // grounded is passed in by Movement, the same way UpdateZoom is handed isMoving.
    public void UpdateCrane(bool grounded)
    {
        if (cameraFollowTarget == null) return;

        // Only re-aimed from the ground. The wall ray starts at the player's own
        // height, so while airborne it used to ride up with the jump and could
        // clear the top of a wall or a dresser mid-flight - flipping the hit off
        // and swinging this transform, which is the camera's tracking target, by up
        // to maxCraneHeight. Jumping is not a reason to re-frame the shot, so the
        // last grounded target is held until they land. The smoothing below still
        // finishes any transition that was already under way at take-off.
        if (grounded)
        {
            craneTargetY = ResolveCraneTarget();
        }

        float smoothedY = Mathf.SmoothDamp(cameraFollowTarget.localPosition.y, craneTargetY, ref craneVelocity, 0.2f);

        cameraFollowTarget.localPosition = new Vector3(0, smoothedY, 0);
    }

    // Lifts the follow target as the player backs up against a wall, so the shot
    // isn't left pressed into the geometry behind them.
    private float ResolveCraneTarget()
    {
        Vector3 flatCameraPos = new Vector3(followCam.position.x, transform.position.y, followCam.position.z);
        Vector3 flatDirectionToCamera = (flatCameraPos - transform.position).normalized;

        Vector3 rayOrigin = transform.position + new Vector3(0, defaultTargetY, 0);

        if (Physics.Raycast(rayOrigin, flatDirectionToCamera, out RaycastHit hit, wallCheckDistance, cameraObstacleMask))
        {
            float squishPercent = 1f - (hit.distance / wallCheckDistance);
            return defaultTargetY + (maxCraneHeight * squishPercent);
        }

        return defaultTargetY;
    }
}
