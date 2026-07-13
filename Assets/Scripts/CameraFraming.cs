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

    public void UpdateCrane()
    {
        if (cameraFollowTarget == null) return;

        Vector3 flatCameraPos = new Vector3(followCam.position.x, transform.position.y, followCam.position.z);
        Vector3 flatDirectionToCamera = (flatCameraPos - transform.position).normalized;

        Vector3 rayOrigin = transform.position + new Vector3(0, defaultTargetY, 0);
        float targetY = defaultTargetY;

        if (Physics.Raycast(rayOrigin, flatDirectionToCamera, out RaycastHit hit, wallCheckDistance, cameraObstacleMask))
        {
            float squishPercent = 1f - (hit.distance / wallCheckDistance);
            targetY = defaultTargetY + (maxCraneHeight * squishPercent);
        }

        float smoothedY = Mathf.SmoothDamp(cameraFollowTarget.localPosition.y, targetY, ref craneVelocity, 0.2f);

        cameraFollowTarget.localPosition = new Vector3(0, smoothedY, 0);
    }
}
