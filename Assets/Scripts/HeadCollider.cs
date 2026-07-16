using UnityEngine;

// Physical "head" volume for the player. The CharacterController only
// collides through its own capsule, so an extra SphereCollider on the same
// object is ignored by controller.Move(). This component keeps the sphere
// centred on the capsule's top point (following the crouch / airborne-tuck
// resizing done in Movement.ColliderManager) and manually pushes the player
// out of any geometry the sphere overlaps.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(SphereCollider))]
public class HeadCollider : MonoBehaviour
{
    [Tooltip("Geometry the head is not allowed to clip into.")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [Tooltip("Depenetration passes per frame; more passes resolve corners where two walls meet.")]
    [SerializeField] private int maxResolveIterations = 3;

    private CharacterController controller;
    private SphereCollider head;
    private Vector3 headOffset;
    private readonly Collider[] overlaps = new Collider[16];

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        head = GetComponent<SphereCollider>();

        // Scene-authored offset of the sphere from the capsule's top point
        // (hand-aligned to the model, e.g. sunk slightly below the top).
        // Captured before any runtime resizing and re-applied every frame, so
        // the sphere rides the capsule top without losing that alignment.
        headOffset = head.center - (controller.center + Vector3.up * (controller.height * 0.5f));
    }

    // LateUpdate: Movement has already run controller.Move() and resized the
    // capsule in its Update(), so the sphere follows the final pose.
    private void LateUpdate()
    {
        // A disabled controller means a scripted sequence owns the player
        // (e.g. PlayerRespawn's sink-into-the-pit cinematic drives the raw
        // transform). Stand down: Move() would both log errors and fight the
        // sequence by pushing the head back out of the ground.
        if (!controller.enabled) return;

        head.center = controller.center + Vector3.up * (controller.height * 0.5f) + headOffset;

        for (int i = 0; i < maxResolveIterations; i++)
        {
            if (!ResolveOverlapsOnce()) break;
        }
    }

    private bool ResolveOverlapsOnce()
    {
        Vector3 worldCenter = transform.TransformPoint(head.center);
        float worldRadius = head.radius * MaxAbsComponent(transform.lossyScale);

        int count = Physics.OverlapSphereNonAlloc(worldCenter, worldRadius, overlaps, obstacleMask, QueryTriggerInteraction.Ignore);
        bool pushed = false;

        for (int i = 0; i < count; i++)
        {
            Collider other = overlaps[i];
            if (other == null || other.transform.IsChildOf(transform)) continue;

            if (Physics.ComputePenetration(
                    head, transform.position, transform.rotation,
                    other, other.transform.position, other.transform.rotation,
                    out Vector3 direction, out float distance))
            {
                // Push through Move() so the capsule keeps colliding while
                // being shoved out (e.g. doesn't get pushed into the floor).
                controller.Move(direction * distance);
                pushed = true;
            }
        }

        return pushed;
    }

    private static float MaxAbsComponent(Vector3 v)
    {
        return Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
    }
}
