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

    [Space]
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float turnSpeed = 5f;
    [SerializeField] private float gravity = 2.81f;
    [SerializeField] private float jumpHeight = 2.5f;
    [SerializeField] private float sprintSpeed = 10f;
    [SerializeField] private float sprintTransitSpeed = 5f;
    [Space]
    [Header ("Crouch Settings")]
    [SerializeField] private float crouchSpeed = 2f;
    [SerializeField] private float slideInitialSpeed = 12f;
    [SerializeField] private float slideDecayRate = 10f; 
    [SerializeField] private float normalHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;

    [Space]
    [Header ("Jump Tuck")]
    [SerializeField] private float airborneHeight = 1.2f;
    [SerializeField] private float airborneCenterY = 1.4f;
    [Space]
    [Header("Jump Forgiveness")]
    [SerializeField] private float jumpBufferTime = 0.2f;
    private float jumpBufferCounter;

    [Space]
    [Header ("Turn Settings")]
    [SerializeField] private float turnSmoothTime = 0.1f;
    private float turnSmoothVelocity; 

    [Space]
    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckOffset = 0.1f;

    [Space]
    [Header("Dynamic Camera Zoom")]
    [SerializeField] private CinemachineCamera virtualCam; 
    [SerializeField] private float normalCamRadius = 3f;   
    [SerializeField] private float pullBackCamRadius = 6f; 
    [SerializeField] private float cameraZoomSpeed = 2f;  
    
    private CinemachineOrbitalFollow orbitalFollow;
    private float verticalVelocity;
    private float speed;

    [Space]
    [Header ("Input")]
    private float moveInput;
    private float turnInput;

    [Space]
    [Header("State Tarckers")]
    private bool isCrouching;
    private bool isSliding;
    private bool Grounded;
    private bool isRunning;
    private bool isJumping;
    private bool wasGrounded;    
    private float currentSlideSpeed;
    private Vector3 slideDirection;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (anim == null) anim = GetComponent<Animator>();

        if (orbitalFollow == null)
        {
            orbitalFollow = virtualCam.GetComponent<CinemachineOrbitalFollow>();
        }
    }

    private void Update()
    {
        wasGrounded = Grounded; 

        float radius = controller.radius * 0.9f;
        Vector3 origin = transform.position + controller.center;

        float maxDistance = controller.center.y - radius + groundCheckOffset;

        Grounded = Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, maxDistance, groundMask);

        if (verticalVelocity > 0f)
        {
            Grounded = false;
        }
        
        if(Grounded && !wasGrounded && verticalVelocity <= 0)
        {
            float floorY = origin.y - hit.distance - radius;

            controller.enabled = false;
            transform.position = new Vector3(transform.position.x, floorY, transform.position.z);
            controller.enabled = true;
        }

        InputManagement();
        TheMovement();
        DynamicCameraZoom();

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

        if (Input.GetButtonDown("Jump") && Grounded && !isJumping)
        {
            PerformJump();
        }

        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }
        if (jumpBufferCounter > 0f && !isJumping)
        {
            PerformJump();
            jumpBufferCounter = 0f;
        }
    }
        private void PerformJump()
    {
        isJumping = true; 

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
            Debug.Log("WE ARE RUNNING!");
        }

        if (Grounded)
        {
            Debug.Log("On The Ground, Boss");
        }

        if (isCrouching)
        {
            Debug.Log("SITTED");
        }
        anim.SetBool("isMoving", isMoving);
        anim.SetBool("isCrouching",isCrouching);
        anim.SetBool("isSliding", isSliding);
        anim.SetBool("Grounded", Grounded);
        anim.SetBool("isRunning", isRunning);

        float speedAnimator = Grounded ? 0f : verticalVelocity;
        anim.SetFloat("yVelocity", speedAnimator);
    }

    private void TheMovement()
    {
        Vector3 finalMove = Vector3.zero;
        if (isSliding)
        {
            finalMove = HandleSlide();
        }
        else
        {
            finalMove = GroundMovement();
        }

        finalMove.y = VerticalForceCalc();
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
        Vector3 slideMove = slideDirection * currentSlideSpeed;
        currentSlideSpeed -= slideDecayRate * Time.deltaTime;

        if (currentSlideSpeed <= crouchSpeed)
        {
            isSliding = false;
        }

        return slideMove;
    }

    private void StartCrouchOrSlide()
    {
        isCrouching = true;

        bool isMoving = Mathf.Abs(moveInput) > 0.1f || Mathf.Abs(turnInput) > 0.1f;
        
        if(isMoving && Input.GetKey(KeyCode.LeftShift) && Grounded)
        {
            isSliding = true;
            currentSlideSpeed = slideInitialSpeed;
            slideDirection = transform.forward;
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
            verticalVelocity = -10f;
            isJumping = false;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;

            if (verticalVelocity < -5f)
            {
                verticalVelocity = -3f;
            }
        }

        return verticalVelocity;
    }

    private void ColliderManager()
    {
        if (!Grounded)
        {
            controller.height = airborneHeight;
            controller.center = new Vector3(0, airborneCenterY, 0);
        }
        else if (isCrouching || isSliding)
        {
            controller.height = crouchHeight;
            controller.center = new Vector3(0, crouchHeight / 2f, 0);
        }
        else
        {
            controller.height = normalHeight;
            controller.center = new Vector3(0, normalHeight / 2f, 0);
        }

    }

    private void DynamicCameraZoom()
    {
        if (orbitalFollow == null)
        {
            Debug.LogWarning("Camera Zoom: Orbital Follow component is missing or not assigned!");
            return;
        }

        bool isMoving = Mathf.Abs(moveInput) > 0.1f || Mathf.Abs(moveInput) > 0.1f;

        float viewAngle = Vector3.Dot(transform.forward, followCam.forward);

        if(isMoving && viewAngle < -0.2f)
        {
            Debug.Log("Camera Zoom: Player is running AT the camera! Expanding radius...");
            orbitalFollow.Radius = Mathf.Lerp(orbitalFollow.Radius, pullBackCamRadius, Time.deltaTime * cameraZoomSpeed);
        }
        else
        {
            orbitalFollow.Radius = Mathf.Lerp(orbitalFollow.Radius, normalCamRadius, Time.deltaTime*cameraZoomSpeed);
        }
    }
}


