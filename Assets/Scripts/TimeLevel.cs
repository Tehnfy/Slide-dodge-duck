using System.Collections;
using TMPro.EditorUtilities;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TimeLevel : MonoBehaviour
{
    [Header("Logic")]
    [SerializeField] GameObject timeBox;
    [SerializeField] int timeLeft = 60;
    [SerializeField] bool takingSecond = false;
    [SerializeField] GameObject playerControl;
    
    [Space]
    [Header("Sounds")]
    [SerializeField] AudioSource timeUpSound;
    [SerializeField] GameObject levelBGM;
    [SerializeField] GameObject fadeOut;
    [SerializeField] GameObject timeUpText;

    void Update()
    {
        timeBox.GetComponent<TMPro.TMP_Text>().text = "TIME LEFT : " + timeLeft;
        if (takingSecond == false)
        {
            StartCoroutine(RemoveTime());
        }
        if (timeLeft == 0)
        {
            levelBGM.SetActive(false);
            timeUpSound.Play();
            fadeOut.SetActive(true);
            timeUpText.SetActive(true);
            StartCoroutine(TimeRunOut());
        }
    }

    IEnumerator RemoveTime()
    {
        takingSecond = true;
        yield return new WaitForSeconds(1);
        timeLeft -= 1;
        takingSecond = false;
    }

    IEnumerator DeathLoad()
    {
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene(3);
    }

    IEnumerator TimeRunOut()
    {
        yield return new WaitForSeconds(0.3f);
        playerControl.GetComponent<Movement>().enabled = false;
        playerControl.GetComponent<Animator>().Play("Idle");
    }
}
