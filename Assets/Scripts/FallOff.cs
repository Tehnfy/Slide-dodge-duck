using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

// Sits on a trigger volume strung underneath a jumping section, gating it: catch
// the player on a missed jump and set them back down on the last platform they
// were genuinely standing on, behind the level's fade.
//
// The fade hides the cut, so the move happens at the end of the fade rather than
// on the trigger frame.
public class FallOff : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] AudioSource levelEndSound;
    [Tooltip("Silenced for the fall and switched back on once the player is returned.")]
    [SerializeField] GameObject levelBGM;

    [Space]
    [Header("Fade")]
    [SerializeField] GameObject fadeOut;
    [Tooltip("Optional. Played once the player is back so the screen clears again - the counterpart to fadeOut.")]
    [SerializeField] GameObject fadeIn;
    [Tooltip("How long the drop to black takes. The shared fade clip is retimed to fit, so other users of it keep their own pacing.")]
    [SerializeField] float fadeOutDuration = 0.6f;
    [Tooltip("How long the return from black takes once the player is back on their feet.")]
    [SerializeField] float fadeInDuration = 1.5f;

    // Where a caught player is put back.
    private enum ReturnTarget
    {
        RecordedPlatform, // the last spot they were genuinely standing on
        FallbackSpawn     // always the fixed transform below
    }

    [Space]
    [Header("Return")]
    [Tooltip("RecordedPlatform sets them down where they last stood. FallbackSpawn always uses the transform below - the right choice when this volume is something a player can walk into over and over, such as a wall, where returning them to where they stood just puts them straight back into it.")]
    [SerializeField] ReturnTarget returnTarget = ReturnTarget.RecordedPlatform;
    [SerializeField] float verticalOffset = 0.05f;
    [Tooltip("Used by FallbackSpawn mode, and as a safety net when nothing has been recorded yet. If it has a Collider the player is placed on its top face.")]
    [SerializeField] Transform fallbackSpawn;

    [Space]
    [Header("Camera")]
    [Tooltip("Warped on return so the camera cuts across instead of damping its way back. Auto-found if empty.")]
    [SerializeField] CinemachineCamera virtualCam;

    private bool isReturning;

    // Shared across every gate. The player keeps falling during the fade now, so
    // they can drop through a second volume mid-sequence; without this the two
    // sequences race and yank the player twice.
    private static FallOff activeReturn;

    private void Start()
    {
        // The collider is the whole point of this component: a solid one would
        // just be a floor the player lands on, and OnTriggerEnter would never
        // fire.
        Collider gate = GetComponent<Collider>();
        if (gate != null && !gate.isTrigger)
        {
            gate.isTrigger = true;
            Debug.LogWarning($"{name}: FallOff needs a trigger collider - forcing isTrigger on.", this);
        }

        if (virtualCam == null) virtualCam = FindFirstObjectByType<CinemachineCamera>();
    }

    // Never leave the shared guard latched if this gate is disabled or its scene
    // unloads part-way through a return.
    private void OnDisable()
    {
        if (activeReturn == this) activeReturn = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isReturning || activeReturn != null) return;

        // The player carries several colliders (CharacterController plus the
        // head sphere), so match on the component and let the isReturning guard
        // swallow the duplicate enter events.
        Movement movement = other.GetComponentInParent<Movement>();
        if (movement == null) return;

        StartCoroutine(ReturnSequence(movement));
    }

    private IEnumerator ReturnSequence(Movement movement)
    {
        isReturning = true;
        activeReturn = this;

        Transform player = movement.transform;
        CharacterController controller = movement.GetComponent<CharacterController>();

        // Snapshot the destination now, on the way in. The player keeps falling
        // through the fade below, and if they touch down on the way they would
        // otherwise overwrite their own safe ground and get returned to wherever
        // they landed instead of the platform they slipped off.
        bool hasReturnPoint = TryResolveReturnPoint(movement, out Vector3 to, out float returnYaw);

        if (levelBGM != null) levelBGM.SetActive(false);
        if (levelEndSound != null) levelEndSound.Play();
        Replay(fadeOut, fadeOutDuration, 0f);

        // Deliberately NOT frozen yet: halting the player mid-air while the
        // screen is still clear reads as a hitch. They carry on falling until
        // the fade has gone fully black, and only then are they picked up.
        // The clip is retimed to fadeOutDuration above, so this lands on its
        // last frame - full black - every time.
        yield return new WaitForSeconds(fadeOutDuration);

        if (hasReturnPoint)
        {
            Vector3 from = player.position;

            // Off before repositioning, so the move is not fought by collision
            // resolution on the way out.
            if (controller != null) controller.enabled = false;
            movement.enabled = false;

            player.position = to;
            player.rotation = Quaternion.Euler(0f, returnYaw, 0f);

            // Without this the follow camera damps its way back across the
            // level and the player watches it arrive as the fade clears.
            if (virtualCam != null)
            {
                Transform warpTarget = virtualCam.Target.TrackingTarget != null ? virtualCam.Target.TrackingTarget : player;
                virtualCam.OnTargetObjectWarped(warpTarget, to - from);
            }
        }

        // Back on their feet: grounded pose before Movement takes over again.
        movement.ForceAnimatorState(grounded: true, yVelocityValue: 0f);
        if (controller != null) controller.enabled = true;
        movement.enabled = true;

        if (levelBGM != null) levelBGM.SetActive(true);

        if (fadeOut != null)
        {
            fadeOut.SetActive(false);
            // Hand the shared object back at normal speed, so the level-end fade
            // that also uses it is not left running at our pace.
            SetFadeSpeed(fadeOut, 1f);
        }

        Replay(fadeIn, fadeInDuration, 1f);

        isReturning = false;
        if (activeReturn == this) activeReturn = null;
    }

    private bool TryResolveReturnPoint(Movement movement, out Vector3 point, out float yaw)
    {
        bool preferFallback = returnTarget == ReturnTarget.FallbackSpawn;

        if (!preferFallback && movement.HasLastSafeGround())
        {
            Vector3 recorded = movement.GetLastSafeGround() + Vector3.up * verticalOffset;

            // Putting them back inside this very volume would trip it again the
            // instant control returns - the respawn loop this toggle exists to
            // avoid. Worth catching even in RecordedPlatform mode, because a
            // walk-into volume can be entered from a spot that qualified.
            if (!IsInsideThisVolume(recorded))
            {
                point = recorded;
                yaw = movement.GetLastSafeGroundYaw();
                return true;
            }

            Debug.LogWarning($"{name}: the recorded platform lies inside this volume, so the fallback is used instead to avoid a respawn loop.", this);
        }

        if (fallbackSpawn != null)
        {
            point = ResolveFallbackPoint();
            yaw = fallbackSpawn.eulerAngles.y;
            return true;
        }

        if (movement.HasLastSafeGround())
        {
            if (preferFallback)
            {
                Debug.LogWarning($"{name}: set to FallbackSpawn but none is assigned, so the recorded platform is used.", this);
            }

            point = movement.GetLastSafeGround() + Vector3.up * verticalOffset;
            yaw = movement.GetLastSafeGroundYaw();
            return true;
        }

        // Nothing to go back to - leave them be rather than flinging them to the
        // origin, and say so.
        Debug.LogWarning($"{name}: no recorded platform and no fallbackSpawn, so the player was left where they fell.", this);
        point = movement.transform.position;
        yaw = movement.transform.eulerAngles.y;
        return false;
    }

    private Vector3 ResolveFallbackPoint()
    {
        Vector3 landing = fallbackSpawn.position;

        // Same trick PortalTeleport uses: aim at the top face, so a platform
        // pivoted at its centre does not spawn the player half sunk into it.
        if (fallbackSpawn.TryGetComponent(out Collider spawnCollider))
        {
            landing.y = spawnCollider.bounds.max.y;
        }

        return landing + Vector3.up * verticalOffset;
    }

    private bool IsInsideThisVolume(Vector3 worldPoint)
    {
        Collider gate = GetComponent<Collider>();
        if (gate == null) return false;

        // ClosestPoint hands back the point unchanged when it is already inside.
        return (gate.ClosestPoint(worldPoint) - worldPoint).sqrMagnitude < 0.0001f;
    }

    // Toggling off first rewinds the clip, so a second fall fades from the top
    // instead of resuming on the last frame of the previous one.
    private static void Replay(GameObject fadeObject, float targetDuration, float startAlpha)
    {
        if (fadeObject == null) return;

        fadeObject.SetActive(false);

        // The Animator does not restore colours on disable, so the graphic is
        // still holding the alpha the last fade ended on. Prime it before the
        // object goes live, or the first frame shows the tail of the previous
        // fade until the Animator gets around to rewinding it.
        Graphic graphic = fadeObject.GetComponentInChildren<Graphic>(true);
        if (graphic != null)
        {
            Color primed = graphic.color;
            primed.a = startAlpha;
            graphic.color = primed;
        }

        // Speed set while still inactive so the very first evaluated frame is
        // already at the right pace.
        SetFadeSpeed(fadeObject, ResolveSpeedFor(fadeObject, targetDuration));
        fadeObject.SetActive(true);
    }

    // Retimed through the Animator rather than by editing the clip, because
    // these fade objects are shared with the level-end sequence.
    private static float ResolveSpeedFor(GameObject fadeObject, float targetDuration)
    {
        if (targetDuration <= 0f) return 1f;

        Animator animator = fadeObject.GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) return 1f;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0 || clips[0].length <= 0f) return 1f;

        return clips[0].length / targetDuration;
    }

    private static void SetFadeSpeed(GameObject fadeObject, float speed)
    {
        Animator animator = fadeObject != null ? fadeObject.GetComponent<Animator>() : null;
        if (animator != null) animator.speed = speed;
    }
}
