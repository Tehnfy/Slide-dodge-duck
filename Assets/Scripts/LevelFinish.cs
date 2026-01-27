using UnityEngine;

public class LevelFinish : MonoBehaviour
{

[SerializeField] GameObject playerControl;
[SerializeField] AudioSource levelEndSound;
[SerializeField] GameObject levelBGM;
[SerializeField] GameObject fadeOut;

    void OnTriggerEnter(Collider other)
    {
        playerControl.GetComponent<Movement>().enabled = false;
        levelBGM.SetActive(false);
        levelEndSound.Play();
        fadeOut.SetActive(true);
    }
}
