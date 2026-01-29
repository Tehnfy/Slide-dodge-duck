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
        playerControl.GetComponent<Movement>().enabled = false;
        playerControl.GetComponent<Animator>().Play("Idle");
    }
}