using System;
using System.Collections;
using JetBrains.Annotations;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;

public class Movement : MonoBehaviour
{

    [SerializeField] CharacterController controller;
    [SerializeField] GameObject ActiveChar;
    [SerializeField] Vector3 playerVelocity;
    [SerializeField] bool groundedPlayer;
    [SerializeField] bool isJumping; 
    [SerializeField] float gravityValue;
    [SerializeField] float moveX;
    [SerializeField] float moveY;
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float turnSpeed = 4f;
    [SerializeField] float jumpHeight = 1.5f;


    void Start()
    {
        moveSpeed = 4f;
        gravityValue = -10f;
    }

    void Update()
    {
        groundedPlayer = controller.isGrounded;
        if (groundedPlayer && playerVelocity.y < 0)
        {
            moveSpeed = 4f;
            playerVelocity.y = 0f;
        }

        transform.Rotate(0, Input.GetAxis("Horizontal") * turnSpeed, 0);
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        float curSpeed = moveSpeed * Input.GetAxis("Vertical");
        controller.SimpleMove(forward * curSpeed);
        groundedPlayer = true;

        if (Input.GetKey(KeyCode.Space) && groundedPlayer)
        {
            isJumping = true;
            groundedPlayer = false;
            ActiveChar.GetComponent<Animator>().Play("Jump");
            playerVelocity.y += 10f;
            StartCoroutine(ResetJump());
        }

        playerVelocity.y = gravityValue * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime);

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.A))
        {
            this.gameObject.GetComponent<CharacterController>().minMoveDistance = 0.001f;
            if (isJumping == false)
            {
                ActiveChar.GetComponent<Animator>().Play("Standard Run");
            }
        }
        else
        {
            this.gameObject.GetComponent<CharacterController>().minMoveDistance = 0.901f;
            if (isJumping == false)
            {
                ActiveChar.GetComponent<Animator>().Play("Idle");
            }
        }
    }

    IEnumerator ResetJump()
    {
        yield return new WaitForSeconds(0.8f);
        isJumping = false;
    }
}


