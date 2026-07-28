using UnityEngine;
using UnityEngine.UI;

// Eases a freshly loaded scene in from black.
//
// The scenes already carry a UI/FadeIn object - a full-screen black graphic with
// the FadeIn clip on it - but nothing ever switched it on at load, so a level cut
// in at full brightness however gently the previous one faded out. This plays it
// on Start, and switches it off again once it has run so a full-screen graphic
// isn't left sitting over the level.
public class SceneFadeIn : MonoBehaviour
{
    [Tooltip("The scene's FadeIn object: a full-screen black graphic with the FadeIn animation on it. A direct child named 'FadeIn' is used if this is left empty.")]
    [SerializeField] private GameObject fadeIn;
    [Tooltip("How long the fade from black takes. The shared clip is retimed to fit, so anything else using it keeps its own pacing.")]
    [SerializeField] private float fadeInDuration = 1.5f;

    private void Start()
    {
        if (fadeIn == null)
        {
            // Find rather than GetComponentInChildren: the object is inactive
            // until we play it, and Find is the lookup that still sees it.
            Transform found = transform.Find("FadeIn");
            if (found != null) fadeIn = found.gameObject;
        }

        if (fadeIn == null)
        {
            Debug.LogWarning($"{name}: SceneFadeIn has no FadeIn object assigned and no child named 'FadeIn', so this scene will cut in rather than fade.", this);
            return;
        }

        StartCoroutine(FadeFromBlack());
    }

    private System.Collections.IEnumerator FadeFromBlack()
    {
        fadeIn.SetActive(false);

        // Painted black before the object goes live. The Animator does not restore
        // colours on disable, so the graphic is still holding whatever alpha the
        // previous fade left on it - and on the very first frame of a scene the
        // Animator has not evaluated yet, so without this the level flashes
        // through before the fade takes hold.
        Graphic graphic = fadeIn.GetComponentInChildren<Graphic>(true);
        if (graphic != null)
        {
            Color primed = graphic.color;
            primed.a = 1f;
            graphic.color = primed;
        }

        // Speed set while still inactive so the first evaluated frame is already
        // at the right pace.
        SetFadeSpeed(ResolveSpeed());
        fadeIn.SetActive(true);

        yield return new WaitForSeconds(fadeInDuration);

        // Off once it has played: a full-screen graphic left active would swallow
        // UI clicks, and the clip is handed back at its authored speed for
        // whatever else uses it (FallOff replays this same object on a return).
        fadeIn.SetActive(false);
        SetFadeSpeed(1f);
    }

    // Retimed through the Animator rather than by editing the clip, because the
    // fade objects are shared with the level-end and fall-return sequences.
    private float ResolveSpeed()
    {
        if (fadeInDuration <= 0f) return 1f;

        Animator animator = fadeIn.GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) return 1f;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0 || clips[0].length <= 0f) return 1f;

        return clips[0].length / fadeInDuration;
    }

    private void SetFadeSpeed(float speed)
    {
        Animator animator = fadeIn.GetComponent<Animator>();
        if (animator != null) animator.speed = speed;
    }
}
