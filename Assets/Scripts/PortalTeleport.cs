using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

// Sits on a trigger volume (e.g. Window_portal) and warps the player to a
// destination transform behind a white flash. The flash hides the cut, so the
// teleport itself happens at peak whiteout - not on the trigger frame.
public class PortalTeleport : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("Where the player lands. If this object has a Collider, the player is placed on its top face.")]
    [SerializeField] private Transform destination;
    [SerializeField] private float verticalOffset = 0.05f;
    [Tooltip("Turn the player to face the same way as the destination on arrival.")]
    [SerializeField] private bool matchDestinationYaw;

    [Space]
    [Header("White Flash")]
    [Tooltip("Optional. Leave empty and a full-screen overlay is built at runtime - no scene wiring needed.")]
    [SerializeField] private Graphic flashOverlay;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float fadeInDuration = 0.35f;
    [SerializeField] private float holdDuration = 0.1f;
    [SerializeField] private float fadeOutDuration = 0.55f;

    [Space]
    [Header("Camera")]
    [Tooltip("Warped on teleport so the camera cuts across instead of damping its way over. Auto-found if empty.")]
    [SerializeField] private CinemachineCamera virtualCam;

    // Only set when we built the overlay ourselves - a hand-wired one belongs to
    // the scene and must not be toggled or destroyed by us.
    private GameObject ownedOverlay;
    private bool isTeleporting;

    private void Start()
    {
        // The collider is the whole point of this component: a solid one would
        // just block the player instead of firing OnTriggerEnter.
        Collider portalCollider = GetComponent<Collider>();
        if (portalCollider != null && !portalCollider.isTrigger)
        {
            portalCollider.isTrigger = true;
            Debug.LogWarning($"{name}: PortalTeleport needs a trigger collider - forcing isTrigger on.", this);
        }

        if (virtualCam == null) virtualCam = FindFirstObjectByType<CinemachineCamera>();
        if (flashOverlay == null) flashOverlay = BuildOverlay();

        SetFlashAlpha(0f);
    }

    private void OnDestroy()
    {
        if (ownedOverlay != null) Destroy(ownedOverlay);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isTeleporting) return;

        // The player carries several colliders (CharacterController plus the
        // head sphere), so match on the component and let the isTeleporting
        // guard swallow the duplicate enter events.
        Movement movement = other.GetComponentInParent<Movement>();
        if (movement == null) return;

        if (destination == null)
        {
            Debug.LogWarning($"{name}: PortalTeleport has no destination assigned.", this);
            return;
        }

        StartCoroutine(TeleportSequence(movement));
    }

    private IEnumerator TeleportSequence(Movement movement)
    {
        isTeleporting = true;

        Transform player = movement.transform;
        CharacterController controller = movement.GetComponent<CharacterController>();

        // Freeze first: the fade-in takes a moment and an un-frozen player would
        // keep running (possibly straight off the ledge) behind a white screen.
        if (controller != null) controller.enabled = false;
        movement.enabled = false;
        // Movement.Update() no longer feeds the Animator, so park it in a
        // grounded/idle pose rather than leaving the run cycle looping.
        movement.ForceAnimatorState(grounded: true, yVelocityValue: 0f);

        yield return Fade(0f, 1f, fadeInDuration);

        if (holdDuration > 0f) yield return new WaitForSeconds(holdDuration);

        Vector3 from = player.position;
        Vector3 to = ResolveDestination();

        player.position = to;
        if (matchDestinationYaw) player.rotation = Quaternion.Euler(0f, destination.eulerAngles.y, 0f);

        // Without this the orbital follow damps its way across the entire level
        // and the player watches it arrive as the flash clears.
        if (virtualCam != null)
        {
            Transform warpTarget = virtualCam.Target.TrackingTarget != null ? virtualCam.Target.TrackingTarget : player;
            virtualCam.OnTargetObjectWarped(warpTarget, to - from);
        }

        // Control comes back at the teleport, not at the end of the fade - the
        // remaining half second then reads as recovery instead of a lockout.
        if (controller != null) controller.enabled = true;
        movement.enabled = true;

        yield return Fade(1f, 0f, fadeOutDuration);

        isTeleporting = false;
    }

    private Vector3 ResolveDestination()
    {
        Vector3 landing = destination.position;

        // Platform meshes are pivoted at their centre, so the raw position would
        // spawn the player half-sunk into the surface - aim at the top face.
        if (destination.TryGetComponent(out Collider destinationCollider))
        {
            landing.y = destinationCollider.bounds.max.y;
        }

        return landing + Vector3.up * verticalOffset;
    }

    private IEnumerator Fade(float fromAlpha, float toAlpha, float duration)
    {
        if (duration <= 0f)
        {
            SetFlashAlpha(toAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            SetFlashAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }

        SetFlashAlpha(toAlpha);
    }

    private void SetFlashAlpha(float alpha)
    {
        if (flashOverlay == null) return;

        Color colour = flashColor;
        colour.a = alpha;
        flashOverlay.color = colour;

        // Keep our own canvas out of the render loop while idle.
        if (ownedOverlay != null) ownedOverlay.SetActive(alpha > 0f);
    }

    private Graphic BuildOverlay()
    {
        ownedOverlay = new GameObject("PortalFlashCanvas");

        Canvas canvas = ownedOverlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // above the level's own fade UI

        GameObject flashObject = new GameObject("Flash");
        flashObject.transform.SetParent(ownedOverlay.transform, false);

        Image flash = flashObject.AddComponent<Image>();
        flash.raycastTarget = false;

        RectTransform rect = flash.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return flash;
    }
}
