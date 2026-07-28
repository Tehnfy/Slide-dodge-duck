using UnityEngine;

// Drifts an object in a continuous loop on any combination of axes, and - when
// the player stands on it - sags under their weight and springs back like a
// branch.
//
// The drift and the sag are kept separate: the loop is a pure function of time
// from the object's starting point, and the branch response is a downward offset
// laid on top. Nothing accumulates, so the object can never wander away from
// where it was placed.
[RequireComponent(typeof(Collider))]
public class Platform_floating : MonoBehaviour
{
    [Header("Looping Drift")]
    [Tooltip("How far the object travels either side of its start position on each axis. Leave an axis at 0 to hold it still.")]
    [SerializeField] private Vector3 amplitude = new Vector3(0f, 0.4f, 0f);
    [Tooltip("Loops per second on each axis.")]
    [SerializeField] private Vector3 frequency = new Vector3(0.25f, 0.25f, 0.25f);
    [Tooltip("Degrees of head start per axis. Offsetting two axes by 90 turns a straight drift into a circle or a figure of eight.")]
    [SerializeField] private Vector3 phaseOffset = Vector3.zero;

    [Space]
    [Header("Branch Response")]
    [SerializeField] private bool reactToPlayer = true;
    [Tooltip("How far it dips at the moment of landing.")]
    [SerializeField] private float impactDepth = 0.35f;
    [Tooltip("How much of that dip it climbs back while the player stands on it. 0.82 recovers 82% and rests 18% down.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float loadedRecovery = 0.82f;
    [Tooltip("Extra kick down when the player jumps off, as a fraction of the impact dip.")]
    [Range(0f, 1.5f)]
    [SerializeField] private float jumpOffKick = 0.7f;
    [Tooltip("The deepest it may sit while the player simply walks off, as a fraction of the impact dip.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float walkOffDip = 0.1f;

    [Space]
    [Header("Response Timing")]
    [SerializeField] private float impactSmoothTime = 0.05f;
    [Tooltip("How long it holds at the bottom of the landing dip before recuperating.")]
    [SerializeField] private float impactHold = 0.09f;
    [Tooltip("The slow climb back up while loaded - this is the part that reads as a branch taking the weight.")]
    [SerializeField] private float settleSmoothTime = 0.3f;
    [SerializeField] private float releaseSmoothTime = 0.07f;
    [Tooltip("How long the release kick lasts before it returns to rest.")]
    [SerializeField] private float releaseHold = 0.11f;
    [SerializeField] private float returnSmoothTime = 0.25f;

    [Space]
    [Header("Stand Detection")]
    [Tooltip("Height of the probe sitting on the top face that spots a rider.")]
    [SerializeField] private float standProbeHeight = 0.3f;
    [Tooltip("Widens the probe past the edges, so a player half over the lip still counts.")]
    [SerializeField] private float standProbePadding = 0.05f;
    [Tooltip("Upward speed that counts as the player having jumped rather than walked off.")]
    [SerializeField] private float jumpDetectVelocity = 0.5f;
    [Tooltip("How long the rider must stay undetected before it counts as them leaving. Covers the branch sagging out from under them, which would otherwise read as landing twice.")]
    [SerializeField] private float releaseGrace = 0.14f;

    private enum Response { Resting, Impact, Loaded, Releasing }

    private Vector3 startLocalPosition;
    private Collider platformCollider;
    private int riderMask;
    private readonly Collider[] riderHits = new Collider[8];

    private Response phase = Response.Resting;
    private float phaseTimer;
    private float sag;              // current offset below rest, positive = down
    private float sagVelocity;
    private float targetSag;
    private float sagSmoothTime;

    private bool loaded;
    private float riderLostTime;
    private Movement rider;

    private void Start()
    {
        startLocalPosition = transform.localPosition;
        platformCollider = GetComponent<Collider>();

        // Resolved by name so no per-scene mask wiring is needed, same as
        // Movement does for its hazard layer.
        riderMask = LayerMask.GetMask("Player");

        sagSmoothTime = returnSmoothTime;
    }

    private void Update()
    {
        if (reactToPlayer) UpdateRider();
        UpdateSag();

        // Rebuilt from the start point every frame rather than nudged, so drift
        // and sag cannot accumulate into a slow crawl.
        transform.localPosition = startLocalPosition + LoopOffset() + Vector3.down * sag;
    }

    private Vector3 LoopOffset()
    {
        float t = Time.time;
        return new Vector3(
            AxisOffset(amplitude.x, frequency.x, phaseOffset.x, t),
            AxisOffset(amplitude.y, frequency.y, phaseOffset.y, t),
            AxisOffset(amplitude.z, frequency.z, phaseOffset.z, t));
    }

    // Sine so the object passes back through its authored position every cycle
    // instead of hanging off to one side of it.
    private static float AxisOffset(float amp, float freq, float phaseDegrees, float t)
    {
        if (amp == 0f) return 0f;
        return Mathf.Sin((t * freq * 360f + phaseDegrees) * Mathf.Deg2Rad) * amp;
    }

    private void UpdateRider()
    {
        Movement standing = FindRider();
        if (standing != null) rider = standing;

        // Rising means they have pushed off, so they stop counting as a load the
        // moment they jump - which is what turns the release into a kick.
        bool rising = rider != null && rider.GetVerticalVelocity() > jumpDetectVelocity;

        if (standing != null && !rising)
        {
            riderLostTime = 0f;

            if (!loaded)
            {
                loaded = true;
                BeginImpact();
            }
            return;
        }

        if (!loaded) return;

        // A jump is unambiguous, so release at once and let the kick land with
        // the push-off.
        if (rising)
        {
            riderLostTime = 0f;
            loaded = false;
            BeginRelease();
            return;
        }

        // Otherwise hold off: as the branch bows it drops away from the player
        // for a frame or two, and treating that as them leaving would fire a
        // second landing the instant they catch up again.
        riderLostTime += Time.deltaTime;
        if (riderLostTime < releaseGrace) return;

        riderLostTime = 0f;
        loaded = false;
        BeginRelease();
    }

    private Movement FindRider()
    {
        Bounds bounds = platformCollider.bounds;
        Vector3 centre = new Vector3(bounds.center.x, bounds.max.y + standProbeHeight * 0.5f, bounds.center.z);
        Vector3 halfExtents = new Vector3(
            bounds.extents.x + standProbePadding,
            standProbeHeight * 0.5f,
            bounds.extents.z + standProbePadding);

        int count = Physics.OverlapBoxNonAlloc(centre, halfExtents, riderHits, Quaternion.identity, riderMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (riderHits[i] == null) continue;

            Movement movement = riderHits[i].GetComponentInParent<Movement>();
            if (movement != null) return movement;
        }

        return null;
    }

    private void BeginImpact()
    {
        phase = Response.Impact;
        targetSag = impactDepth;
        sagSmoothTime = impactSmoothTime;
        phaseTimer = impactHold;
    }

    private void BeginRelease()
    {
        bool jumped = rider != null && rider.GetVerticalVelocity() > jumpDetectVelocity;

        phase = Response.Releasing;
        // Jumping shoves the branch down as they push off; simply stepping away
        // just lets it up, so its dip is capped low.
        targetSag = impactDepth * (jumped ? jumpOffKick : walkOffDip);
        sagSmoothTime = releaseSmoothTime;
        phaseTimer = releaseHold;
    }

    private void UpdateSag()
    {
        if (phaseTimer > 0f)
        {
            phaseTimer -= Time.deltaTime;
            if (phaseTimer <= 0f) AdvancePhase();
        }

        sag = Mathf.SmoothDamp(sag, targetSag, ref sagVelocity, sagSmoothTime);
    }

    private void AdvancePhase()
    {
        switch (phase)
        {
            case Response.Impact:
                // Recuperate most of the way, but stay bowed while it carries them.
                phase = Response.Loaded;
                targetSag = impactDepth * (1f - loadedRecovery);
                sagSmoothTime = settleSmoothTime;
                break;

            case Response.Releasing:
                phase = Response.Resting;
                targetSag = 0f;
                sagSmoothTime = returnSmoothTime;
                break;
        }
    }
}
