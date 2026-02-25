using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.Rendering;

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
    [SerializeField] private float gravity = 7.81f;
    [SerializeField] private float jumpHeight = 0.4f;
    [SerializeField] private float sprintSpeed = 10f;
    [SerializeField] private float sprintTransitSpeed = 5f;

    [Header ("Crouch Settings")]
    [SerializeField] private float crouchSpeed = 2f;
    [SerializeField] private float slideInitialSpeed = 12f;
    [SerializeField] private float slideDecayRate = 10f; 
    [SerializeField] private float normalHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;

    private float verticalVelocity;
    private float speed;

    [Space]
    [Header ("Input")]
    private float moveInput;
    private float turnInput;
    [Header("State Tarckers")]
    private bool isCrouching;
    private bool isSliding;
    private bool Grounded;
    private float currentSlideSpeed;
    private Vector3 slideDirection;
    

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if(anim == null) anim = GetComponent<Animator>();
    }

    private void Update()
    {
        InputManagement();
        TheMovement();
        Turn();
        VerticalForceCalc();

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
    }
    
    private void AnimationManagement()
    {
        if (anim == null) return;

        bool isMoving = Mathf.Abs(moveInput) > 0.1f || Mathf.Abs(turnInput) > 0.1f;

        anim.SetBool("isMoving",isMoving);
        anim.SetBool("isCrouching",isCrouching);
        anim.SetBool("isSliding", isSliding);
        anim.SetBool("Grounded", Grounded);
    }

    private void TheMovement()
    {
        if (isSliding)
        {
            HandleSlide();
        }
        else
        {
        GroundMovement();
        }
    }
    private void GroundMovement()
    {
        Vector3 move = new Vector3(turnInput, 0, moveInput);
        move = followCam.transform.TransformDirection(move);

        move.y = VerticalForceCalc();

        if (Input.GetKey(KeyCode.LeftShift))
        {
            speed = Mathf.Lerp(speed, sprintSpeed, sprintTransitSpeed * Time.deltaTime);
        }
        else
        {
            speed = Mathf.Lerp(speed, walkSpeed, sprintTransitSpeed * Time.deltaTime);
        }

        float currentSpeed = isCrouching ? crouchSpeed : walkSpeed;

        move *= speed;
        controller.Move(move * Time.deltaTime);
    }

    private void HandleSlide()
    {
        controller.Move(slideDirection * currentSlideSpeed * Time.deltaTime);
        currentSlideSpeed -= slideDecayRate * Time.deltaTime;

        if (currentSlideSpeed <= crouchSpeed)
        {
            isSliding = false;
        }
    }

    private void StartCrouchOrSlide()
    {
        isCrouching = true;

        controller.height = crouchHeight;
        controller.center = new Vector3(0, crouchHeight / 2f, 0);

        if (Mathf.Abs(moveInput) > 0.1f || MathF.Abs(turnInput) > 0.1f)
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

        controller.height = normalHeight;
        controller.center = new Vector3(0, normalHeight / 2f, 0);
    }
    private void Turn()
    {
        if (Mathf.Abs(turnInput) > 0.1f || Mathf.Abs(moveInput) > 0.1f)
        {
            Vector3 currentLookDirection = controller.velocity.normalized;
            currentLookDirection.y = 0;

            currentLookDirection.Normalize();

            Quaternion targetRotation = Quaternion.LookRotation(currentLookDirection);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
            Time.deltaTime * turnSpeed);
        }
    }

    private float VerticalForceCalc()
    {
        if (controller.isGrounded)
        {
            verticalVelocity = -1;
            if (Input.GetButtonDown("Jump"))
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * gravity);
            }
            if(Input.GetButtonDown("Jump") && controller.isGrounded)
            {
            if (anim != null) anim.SetTrigger("Jump");
        
            }
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }
        return verticalVelocity;


    }
    

}


