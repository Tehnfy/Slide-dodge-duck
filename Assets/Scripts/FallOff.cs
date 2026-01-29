using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
public class FallOff : MonoBehaviour
{
[SerializeField] AudioSource levelEndSound;
[SerializeField] GameObject levelBGM;
[SerializeField] GameObject fadeOut;

    void OnTriggerEnter(Collider other)
    {
        levelBGM.SetActive(false);
        levelEndSound.Play();
        fadeOut.SetActive(true);
        StartCoroutine(DeathLoad());
    }

    IEnumerator DeathLoad()
    {
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene(3);
    }

}