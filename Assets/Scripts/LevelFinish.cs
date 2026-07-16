using UnityEngine;

public class LevelFinish : MonoBehaviour
{

[SerializeField] GameObject playerControl;
[SerializeField] AudioSource levelEndSound;
[SerializeField] GameObject levelBGM;
[SerializeField] GameObject fadeOut;

    void Start()
    {
        fadeOut.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        levelBGM.SetActive(false);
        levelEndSound.Play();
        fadeOut.SetActive(true);

        StartCoroutine(StopPlayerSequence());
    }
    
    System.Collections.IEnumerator StopPlayerSequence()
    {
        yield return new WaitForSeconds(0.3f);
        Movement movement = playerControl.GetComponent<Movement>();
        movement.enabled = false;
        // Once Movement is off nothing updates the Animator params, so clear
        // the stale isMoving/isRunning or Idle instantly transitions back to
        // Run (the state the player entered the trigger in).
        movement.ForceAnimatorState(grounded: true, yVelocityValue: 0f);
        playerControl.GetComponent<Animator>().Play("Idle");
    }
}