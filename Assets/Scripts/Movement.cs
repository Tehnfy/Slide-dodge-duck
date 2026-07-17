using System;
using System.Numerics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;
using Unity.Cinemachine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;


public class Movement : MonoBehaviour
{
    [Header("References")]
    private CharacterController controller;
    [SerializeField] private Transform followCam;
    [SerializeField] private Animator anim;
    [SerializeField] private CameraFraming cameraFraming;

    [Space]
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float turnSpeed = 5f;
    [SerializeField] private float gravity = 2.81f;
    [SerializeField] private float jumpHeight = 2.5f;
    [SerializeField] private float sprintSpeed = 10f;
    [SerializeField] private float sprintTransitSpeed = 3f;
    [Space]
    [Header ("Crouch Settings")]
    [SerializeField] private float crouchSpeed = 1f;
    [SerializeField] private float slideInitialSpeed = 6f;
    [SerializeField] private float slideMinSpeed = 7f;
    [SerializeField] private float slideDecayRate = 1f;
    [SerializeField] private float crouchHeight = 1f;

    // Standing capsule size/alignment is no longer a serialized number: the
    // CharacterController as authored in the scene (hand-aligned to the
    // model) is the source of truth. Captured once in Start.
    private float standingHeight;
    private Vector3 centerOffset;

    [Space]
    [Header ("Jump Tuck")]
    [SerializeField] private float airborneHeight = 1.2f;
    [SerializeField] private float airborneCenterY = 1.4f;
    [Space]
    [Header("Jump Forgiveness")]
    [SerializeField] private float jumpBufferTime = 0.2f;
    private float jumpBufferCounter;
    [SerializeField] private float coyoteTime = 0.3f;
    private float coyoteTimeCounter;

    [Space]
    [Header ("Turn Settings")]
    [SerializeField] private float turnSmoothTime = 0.1f;
    private float turnSmoothVelocity; 

    [Space]
    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckOffset = 0.1f;
    [SerializeField] private float hazardProbeDepth = 3f;
    [SerializeField] private float hazardProbeRadius = 0.15f;
    [SerializeField] private float animHazardProbeRadius = 0.4f;
    private int hazardMask;
    private bool overHazard;
    private bool animOverHazard;

    [Space]
    [Header("Collider Smoothing")]
    [SerializeField] private float colliderSmoothTime = 0.1f;
    private float heightVelocity;
    private float centerYVelocity;

    private float verticalVelocity;
    private float speed;

    [Space]
    [Header ("Input")]
    private float moveInput;
    private float turnInput;

    [Space]
    [Header("State Trackers")]
    private bool isCrouching;
    private bool isSliding;
    private bool Grounded;
    private bool isRunning;
    private bool isJumping;
    private bool wasGrounded;

    [Space]
    [Header("Slope Physics")]
    [SerializeField] private float slideAcceleration = 12f;
    [SerializeField] private float steepSlopeSlideSpeed = 5f;
    private Vector3 groundNormal;
    private float groundSlopeAngle;
    private bool isOnSteepSlope;
    private Vector3 slideVelocity;


    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Capture the scene-authored capsule before any runtime resizing.
        // centerOffset is its deviation from the geometric default (centre at
        // height/2, no lateral shift); ColliderManager re-applies it in every
        // state so the hand alignment to the model is never lost.
        standingHeight = controller.height;
        centerOffset = controller.center - new Vector3(0f, standingHeight * 0.5f, 0f);

        if (anim == null) anim = GetComponent<Animator>();
        if (cameraFraming == null) cameraFraming = GetComponent<CameraFraming>();

        // Position is fully script-driven through the CharacterController. The
        // jump/fall clips carry baked root-Y motion which, when applied, moves
        // the transform directly - bypassing collision - and can push the player
        // through the floor. Done in code so both scenes get it.
        if (anim != null) anim.applyRootMotion = false;

        // Resolved by name so no per-scene mask wiring is needed.
        hazardMask = LayerMask.GetMask("VOID");
    }

    private void Update()
    {
        wasGrounded = Grounded; 

        float radius = controller.radius * 0.9f;
        Vector3 origin = transform.position + controller.center;

        float maxDistance = controller.center.y - radius + groundCheckOffset;

        // Ignore triggers: hazard volumes (e.g. VOID fall zones) are trigger
        // colliders and must never count as jumpable ground.
        Grounded = Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, maxDistance, groundMask, QueryTriggerInteraction.Ignore);

        // During the first frames of a jump's ascent the character hasn't risen
        // past the sphere-cast offset yet (at high framerates: ~5cm/frame vs 10cm
        // offset), so the cast still reports the floor. Treat ascent as airborne,
        // otherwise the stale 'grounded' redirects the Animator's jump transition
        // back to Idle and refreshes coyote time mid-jump.
        if (isJumping && verticalVelocity > 0f) Grounded = false;

        if (Grounded)
        {
            groundNormal = hit.normal;
            groundSlopeAngle = Vector3.Angle(Vector3.up, groundNormal);
            isOnSteepSlope = groundSlopeAngle > controller.slopeLimit; 
        }
        else
        {
            groundNormal = Vector3.up;
            groundSlopeAngle = 0f;
            isOnSteepSlope = false;
        }

        if (Grounded)
        {
            coyoteTimeCounter = coyoteTime;

            // Reset before InputManagement() so a buffered jump can fire on the
            // landing frame itself instead of one frame late (VerticalForceCalc's
            // reset runs after input). vVel guard: ascent frames can still graze
            // the ground with the sphere cast.
            if (verticalVelocity <= 0f) isJumping = false;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        // Fall zones must always suck the player in: sense whether the nearest
        // thing below is a hazard trigger rather than ground, and if so block
        // every jump path (grounded press, coyote, buffered) via PerformJump.
        // Thin probe radius keeps jumps fair right at a zone's edge; distant
        // catch-all void volumes are beyond the probe depth, so ledge coyote
        // jumps still work.
        overHazard = Physics.SphereCast(origin, hazardProbeRadius, Vector3.down, out RaycastHit hazardHit, hazardProbeDepth, groundMask | hazardMask, QueryTriggerInteraction.Collide)
                     && ((1 << hazardHit.collider.gameObject.layer) & hazardMask) != 0;
        if (overHazard) coyoteTimeCounter = 0f;

        // Wider probe used ONLY for animation: when a fall zone is under or
        // right beside the feet (e.g. grazing a hole's rim in the frames
        // before the death trigger fires), keep the Animator airborne so
        // 'falling' doesn't flicker to Idle/Run for a split second. Gameplay
        // (jump blocking above) keeps the thin probe so edge jumps stay fair.
        animOverHazard = Physics.SphereCast(origin, animHazardProbeRadius, Vector3.down, out RaycastHit animHazardHit, hazardProbeDepth, groundMask | hazardMask, QueryTriggerInteraction.Collide)
                         && ((1 << animHazardHit.collider.gameObject.layer) & hazardMask) != 0;

        InputManagement();
        TheMovement();

        bool isMoving = Mathf.Abs(moveInput) > 0.1f || Mathf.Abs(turnInput) > 0.1f;
        cameraFraming.UpdateZoom(isMoving);
        cameraFraming.UpdateCrane();

        ColliderManager();
        AnimationManagement();
        }
    private void InputManagement()
    {
        moveInput = Input.GetAxisRaw("Vertical");
        turnInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl))
        {
            StartCrouchOrSlide();
        }

        if (Input.GetKeyUp(KeyCode.C) || Input.GetKeyUp(KeyCode.LeftControl))
        {
            StopCrouch();
        }

        // Crouch buffering: the press itself is ignored while airborne (see the
        // Grounded guard in StartCrouchOrSlide), so if the key is still held on
        // the landing frame, enter crouch/slide now - lets players squeeze
        // through low gaps right out of a landing.
        if (Grounded && !wasGrounded && !isCrouching && (Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl)))
        {
            StartCrouchOrSlide();
        }

        if (Input.GetButtonDown("Jump") && coyoteTimeCounter > 0f && !isJumping)
        {
            PerformJump();
            coyoteTimeCounter = 0f;
        }

        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }
        if (jumpBufferCounter > 0f && Grounded && !isJumping)
        {
            PerformJump();
            jumpBufferCounter = 0f;
        }
    }
        private void PerformJump()
    {
        // Standing on / falling into a fall zone: no jump, the reset takes over.
        if (overHazard) return;

        isJumping = true;

        // Grounded was sampled at the top of this Update() call, before the jump
        // was decided, so it's still stale-true here. Correct it immediately so
        // AnimationManagement() (later this same frame) reports airborne instead
        // of momentarily replaying a grounded state alongside the Jump trigger.
        Grounded = false;

        verticalVelocity = Mathf.Sqrt(jumpHeight * 2f * gravity);
        if (anim != null) anim.SetTrigger("Jump");
    }
    
    private void AnimationManagement()
    {
        if (anim == null) return;

        bool isMoving = Mathf.Abs(moveInput) > 0.1f || Mathf.Abs(turnInput) > 0.1f;

        isRunning = isMoving && Input.GetKey(KeyCode.LeftShift) && !isCrouching && Grounded;

        if (isRunning)
        {
           // Debug.Log("WE ARE RUNNING!");
        }

        if (Grounded)
        {
            // Debug.Log("On The Ground, Boss");
        }

        if (isCrouching)
        {
            Debug.Log("SITTED");
        }
        // Over a fall zone the Animator is told 'airborne' even while the
        // capsule still technically touches the rim - see animOverHazard.
        bool animGrounded = Grounded && !animOverHazard;

        anim.SetBool("isMoving", isMoving);
        anim.SetBool("isCrouching",isCrouching);
        anim.SetBool("isSliding", isSliding);
        anim.SetBool("Grounded", animGrounded);
        anim.SetBool("isRunning", isRunning);

        float speedAnimator = animGrounded ? 0f : verticalVelocity;
        anim.SetFloat("yVelocity", speedAnimator);
    }

private void TheMovement()
    {
        Vector3 finalMove = Vector3.zero;
        float gravityY = VerticalForceCalc(); 

        if (isOnSteepSlope)
        {
            Vector3 steepDown = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
            finalMove = steepDown * steepSlopeSlideSpeed;
            isSliding = false;

            finalMove.y -= 2f;
        }

        else if (isSliding)
        {
            finalMove = HandleSlide();
            if (Grounded) finalMove.y += gravityY;
        }

        else
        {
            finalMove = GroundMovement();

            if (Grounded)
            {
                finalMove = Vector3.ProjectOnPlane(finalMove, groundNormal);
                finalMove.y += gravityY;
            }
        }

        if (!Grounded)
        {
            finalMove.y = gravityY; 
        }

        controller.Move(finalMove * Time.deltaTime);
    }
    

    private Vector3 GroundMovement()
    {
        float targetSpeed;
        if (isCrouching)
        {
            targetSpeed = crouchSpeed;
        }
        else if(isRunning)
        {
            targetSpeed = sprintSpeed;           
        }
        else
        {
            targetSpeed = walkSpeed;
        }

        speed = Mathf.Lerp(speed, targetSpeed, sprintTransitSpeed * Time.deltaTime);

        Vector3 inputDirection = new Vector3(turnInput, 0f, moveInput).normalized;
        Vector3 moveDir = Vector3.zero;

        if (inputDirection.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + followCam.eulerAngles.y;

            float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

            moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        }

        return moveDir * speed;
    }

private Vector3 HandleSlide()
    {
        Vector3 slopeDown = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;

        if (groundSlopeAngle > 2f)
        {
            float slopeIntensity = groundSlopeAngle / controller.slopeLimit; 
            slideVelocity += slopeDown * (slideAcceleration * slopeIntensity) * Time.deltaTime;
        }
        else
        {
            Vector3 friction = -slideVelocity.normalized * slideDecayRate * Time.deltaTime;
            
            if (friction.magnitude >= slideVelocity.magnitude) {
                slideVelocity = Vector3.zero;
            } else {
                slideVelocity += friction;
            }
        }

        if (slideVelocity.magnitude <= crouchSpeed && groundSlopeAngle <= 2f)
        {
            isSliding = false;
        }

        return slideVelocity;
    }

    private void StartCrouchOrSlide()
    {
        if (!Grounded) return;

        isCrouching = true;
        bool isMoving = Mathf.Abs(moveInput) > 0.1f || Mathf.Abs(turnInput) > 0.1f;

        // Speed gate: sliding converts current speed into slideInitialSpeed, so
        // entering it below that (e.g. landing from a walk-jump with Run+crouch
        // held) would be a free speed boost. Only actual sprinting pace slides.
        if (isMoving && Input.GetKey(KeyCode.LeftShift) && speed >= slideMinSpeed && Grounded && !isOnSteepSlope)
        {
            isSliding = true;
            slideVelocity = transform.forward * slideInitialSpeed;
        }

        else if (Grounded && groundSlopeAngle > 5f && !isOnSteepSlope)
        {
            isSliding = true;
            Vector3 slopeDown = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
            slideVelocity = slopeDown * 2f;
        }
    }
    
    private void StopCrouch()
    {
        isCrouching = false;
        isSliding = false;
    }
private float VerticalForceCalc()
    {
        if (Grounded && verticalVelocity <= 0f)
        {
            verticalVelocity = -2f; 
            isJumping = false;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;

            if (verticalVelocity < -15f)
            {
                verticalVelocity = -15f;
            }
        }

        return verticalVelocity;
    }

    private void ColliderManager()
    {
        float targetHeight;
        float targetCenterY; 

        if (!Grounded)
        {
            targetHeight = airborneHeight;
            targetCenterY = airborneCenterY + centerOffset.y;
        }
        else if (isCrouching || isSliding)
        {
            targetHeight = crouchHeight;
            targetCenterY = crouchHeight / 2f + centerOffset.y;
        }
        else
        {
            targetHeight = standingHeight;
            targetCenterY = standingHeight / 2f + centerOffset.y;
        }

        if (Grounded && !wasGrounded)
        {
            controller.height = airborneHeight;
            controller.center = new Vector3(centerOffset.x, airborneHeight / 2f + centerOffset.y, centerOffset.z);

            heightVelocity = 0f;
            centerYVelocity = 0f;
            return;
        }

        controller.height = Mathf.SmoothDamp(controller.height, targetHeight, ref heightVelocity, colliderSmoothTime);
        float smoothedCenterY = Mathf.SmoothDamp(controller.center.y, targetCenterY, ref centerYVelocity, colliderSmoothTime);
        controller.center = new Vector3(centerOffset.x, smoothedCenterY, centerOffset.z);
    }

    public Vector3 GetSlideVelocity() { return slideVelocity; }
    public float GetGroundAngle() { return groundSlopeAngle; }
    public bool GetIsSliding() { return isSliding; }
    public bool GetIsRunning() { return isRunning; }
    public bool GetIsCrouching() { return isCrouching; }

    // Lets external sequences (e.g. PlayerRespawn) drive the Animator while
    // this component is disabled and AnimationManagement() isn't running.
    public void ForceAnimatorState(bool grounded, float yVelocityValue, bool moving = false, bool running = false, bool crouching = false, bool sliding = false)
    {
        if (anim == null) return;

        anim.SetBool("Grounded", grounded);
        anim.SetFloat("yVelocity", yVelocityValue);
        anim.SetBool("isMoving", moving);
        anim.SetBool("isRunning", running);
        anim.SetBool("isCrouching", crouching);
        anim.SetBool("isSliding", sliding);
    }
}


