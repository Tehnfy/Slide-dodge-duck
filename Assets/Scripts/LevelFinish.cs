using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelFinish : MonoBehaviour
{

[SerializeField] GameObject playerControl;
[SerializeField] AudioSource levelEndSound;
[SerializeField] GameObject levelBGM;
[SerializeField] GameObject fadeOut;

[Space]
[Header("Next Level")]
#if UNITY_EDITOR
[Tooltip("Scene to load when this trigger fires. It must ALSO be listed in the build's scene list (File > Build Profiles > Scene List) or it cannot be loaded at runtime - a warning is logged on Start if it isn't.")]
[SerializeField] UnityEditor.SceneAsset nextLevel;
#endif
// SceneAsset lives in UnityEditor, so it is gone in a build. The name is cached
// into this hidden field by OnValidate and that is what actually gets loaded.
[SerializeField, HideInInspector] string nextLevelName;

[Tooltip("Seconds between the trigger firing and the next scene loading. The end sound and the fade play out during this window, so give it at least as long as the fade takes. 0 loads immediately.")]
[SerializeField] float loadDelay = 2f;

    // The player carries several colliders (CharacterController plus the head
    // sphere), so the trigger fires more than once - without this the sequence
    // and the scene load would both run twice.
    private bool finished;

    void Start()
    {
        fadeOut.SetActive(false);

        // Said now rather than on the way out, so a missing entry surfaces when
        // the level is first played instead of after reaching the end of it.
        if (!string.IsNullOrEmpty(nextLevelName) && !IsInBuildSettings(nextLevelName))
        {
            Debug.LogWarning($"{name}: '{nextLevelName}' is not in the build's scene list, so LevelFinish will not be able to load it. Add it via File > Build Profiles > Scene List.", this);
        }
    }

#if UNITY_EDITOR
    // Keeps the runtime name in step with whatever is dragged into the field.
    private void OnValidate()
    {
        nextLevelName = nextLevel != null ? nextLevel.name : string.Empty;
    }
#endif

    void OnTriggerEnter(Collider other)
    {
        if (finished) return;
        finished = true;

        levelBGM.SetActive(false);
        levelEndSound.Play();
        fadeOut.SetActive(true);

        StartCoroutine(StopPlayerSequence());
        StartCoroutine(LoadNextLevelSequence());
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

    // Timed from the trigger itself rather than from the end of the sequence
    // above, so loadDelay means what it says no matter how that sequence changes.
    System.Collections.IEnumerator LoadNextLevelSequence()
    {
        if (loadDelay > 0f) yield return new WaitForSeconds(loadDelay);

        if (string.IsNullOrEmpty(nextLevelName))
        {
            Debug.LogWarning($"{name}: no next level assigned on LevelFinish, so the player is left standing at the end of this one.", this);
            yield break;
        }

        SceneManager.LoadScene(nextLevelName);
    }

    private static bool IsInBuildSettings(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName) return true;
        }

        return false;
    }
}
