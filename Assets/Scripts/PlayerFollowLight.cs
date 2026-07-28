using UnityEngine;

// Parks a light at a fixed offset from the player, expressed in the player's
// own facing space - so a light placed "ahead" keeps leading wherever the
// player turns.
//
// Dormant until BeginFollowing() is called (PortalTeleport does this the
// moment the player arrives), which lets the light sit in the scene as static
// set-dressing beforehand - e.g. lighting the landing platform so it reads as
// a beacon from across the level.
//
// Rotation is deliberately never touched: these lights point straight down, and
// spinning a vertical cone about its own axis would only shimmer the shadows.
public class PlayerFollowLight : MonoBehaviour
{
    [Header("Offset from player")]
    [Tooltip("Derive the offsets below from where this light sits in the scene relative to the player on the first follow frame. Keeps hand-placed lighting authoritative.")]
    [SerializeField] private bool deriveOffsetOnArrival = true;
    [Tooltip("Metres in front of the player, along their facing.")]
    [SerializeField] private float distanceAhead = 5f;
    [Tooltip("Metres above the player's feet.")]
    [SerializeField] private float height = 5f;

    [Space]
    [Header("Motion")]
    [Tooltip("0 = rigidly locked to the player. Small values let the pool of light trail slightly, which reads as weight.")]
    [SerializeField] private float smoothTime = 0.08f;

    private Transform player;
    private bool following;
    private bool offsetDerived;
    private Vector3 followVelocity;

    public bool IsFollowing => following;

    // Called by whatever hands the player over (PortalTeleport on arrival).
    public void BeginFollowing(Transform playerTransform)
    {
        if (playerTransform == null) return;

        player = playerTransform;

        // Once only: by a second arrival this light has already been trailing
        // the player around, so re-measuring would bake that stale position in
        // as the offset instead of the placement authored in the scene.
        if (deriveOffsetOnArrival && !offsetDerived)
        {
            offsetDerived = true;

            // Measured as a distance rather than a direction: the player's yaw
            // on arrival is arbitrary (the portal doesn't reorient them), so a
            // stored world direction would leave an "ahead" light pointing
            // behind them.
            Vector3 offset = transform.position - player.position;
            height = offset.y;
            distanceAhead = new Vector3(offset.x, 0f, offset.z).magnitude;
        }

        followVelocity = Vector3.zero;
        following = true;
    }

    public void StopFollowing()
    {
        following = false;
    }

    // LateUpdate so the player has already been moved by Movement this frame,
    // matching how HeadCollider rides the controller.
    private void LateUpdate()
    {
        if (!following) return;

        if (player == null)
        {
            following = false;
            return;
        }

        Vector3 flatForward = player.forward;
        flatForward.y = 0f;
        flatForward = flatForward.sqrMagnitude > 0.0001f ? flatForward.normalized : Vector3.forward;

        Vector3 target = player.position + Vector3.up * height + flatForward * distanceAhead;

        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, target, ref followVelocity, smoothTime)
            : target;
    }
}
