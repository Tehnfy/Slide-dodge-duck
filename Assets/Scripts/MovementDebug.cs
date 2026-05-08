using UnityEngine;

public class MovementDebugger : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float maxMeterSpeed = 20f;
    
    private CharacterController controller;
    private Movement moveScript;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        moveScript = GetComponent<Movement>();

        if (moveScript == null)
        {
            Debug.LogError("MovementDebugger needs to be on the same object as the Movement script!");
        }
    }

    private void OnGUI()
    {
        if (controller == null || moveScript == null) return;

        Vector3 horizontalVelocity = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
        float realSpeed = horizontalVelocity.magnitude;

        Rect backgroundRect = new Rect(20, 20, 200, 20);
        float fillAmount = Mathf.Clamp01(realSpeed / maxMeterSpeed);
        Rect fillRect = new Rect(20, 20, 200 * fillAmount, 20);

        GUI.color = new Color(0, 0, 0, 0.5f);
        GUI.DrawTexture(backgroundRect, Texture2D.whiteTexture);

        if (moveScript.GetIsSliding()) GUI.color = Color.cyan;
        else if (moveScript.GetIsRunning()) GUI.color = Color.green;
        else if (moveScript.GetIsCrouching()) GUI.color = Color.yellow;
        else GUI.color = Color.white;
        
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
        GUI.color = Color.white; 

        GUIStyle textStyle = new GUIStyle();
        textStyle.normal.textColor = Color.white;
        textStyle.fontSize = 24;
        textStyle.fontStyle = FontStyle.Bold;

        GUI.Label(new Rect(20, 45, 250, 20), "Current Speed: " + realSpeed.ToString("F2"), textStyle);
        GUI.Label(new Rect(20, 65, 250, 20), "Slide Velocity: " + moveScript.GetSlideVelocity().magnitude.ToString("F2"), textStyle);
        GUI.Label(new Rect(20, 85, 250, 20), "Ground Angle: " + moveScript.GetGroundAngle().ToString("F1") + "°", textStyle);
    }
}