using System.Collections;
using UnityEngine;
using Unity.Cinemachine; 

public class PlayerRespawn : MonoBehaviour
{
    [Header("Respawn Settings")]
    [SerializeField] private Transform currentRespawnPoint; 
    [SerializeField] private string hazardTag = "VOID"; 
    
    [Space]
    [Header("Cinematic Transition")]
    [Tooltip("Drag your Cinemachine Camera here!")]
    [SerializeField] private CinemachineCamera virtualCam;
    [SerializeField] private float sinkDepth = 2f;      
    [SerializeField] private float sinkDuration = 1f;   
    [SerializeField] private float panDuration = 2f;    
    
    [Space]
    [Header("The Drop")]
    [SerializeField] private float dropHeight = 15f;    
    [SerializeField] private float fallDuration = 0.8f; 
    [SerializeField] private float cameraSettleDuration = 0.5f; // NEW: How long the camera takes to perfectly realign at the end

    private CharacterController controller;
    private Movement movementScript;
    private bool isRespawning; 

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        movementScript = GetComponent<Movement>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(hazardTag) && !isRespawning)
        {
            StartCoroutine(CinematicRespawnSequence());
        }
    }

    private IEnumerator CinematicRespawnSequence()
    {
        isRespawning = true;

        // 1. FREEZE THE PLAYER
        controller.enabled = false;
        if (movementScript != null) movementScript.enabled = false; 

        // 2. PREP THE CAMERA
        CinemachineCollider camCollider = virtualCam.GetComponent<CinemachineCollider>();
        if (camCollider != null) camCollider.enabled = false; 

        Transform originalTracking = virtualCam.Target.TrackingTarget;
        Transform originalLookAt = virtualCam.Target.LookAtTarget;

        // Create the Master Drone at the player's feet
        GameObject masterDrone = new GameObject("RespawnMasterDrone");
        masterDrone.transform.position = transform.position;
        masterDrone.transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);

        // Create Child Drone 1 (The Position Track)
        GameObject trackingDrone = new GameObject("TrackingDrone");
        trackingDrone.transform.position = originalTracking != null ? originalTracking.position : transform.position;
        trackingDrone.transform.rotation = originalTracking != null ? originalTracking.rotation : transform.rotation;
        trackingDrone.transform.SetParent(masterDrone.transform); // Attach to Master

        // Create Child Drone 2 (The Lens Target)
        GameObject lookAtDrone = new GameObject("LookAtDrone");
        lookAtDrone.transform.position = originalLookAt != null ? originalLookAt.position : transform.position;
        lookAtDrone.transform.rotation = originalLookAt != null ? originalLookAt.rotation : transform.rotation;
        lookAtDrone.transform.SetParent(masterDrone.transform); // Attach to Master
        
        // Hijack the camera with our perfectly separated child drones
        virtualCam.Target.TrackingTarget = trackingDrone.transform;
        if (virtualCam.Target.LookAtTarget != null) virtualCam.Target.LookAtTarget = lookAtDrone.transform;


        // --- PHASE 1: SINK ---
        Vector3 startPos = transform.position;
        Vector3 undergroundPos = startPos - new Vector3(0, sinkDepth, 0);
        float elapsed = 0f;

        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / sinkDuration); 
            transform.position = Vector3.Lerp(startPos, undergroundPos, t);
            yield return null; 
        }


        // --- PHASE 2: MASTER DRONE PAN ---
        Vector3 droneStartPos = masterDrone.transform.position;
        Vector3 droneEndPos = currentRespawnPoint.position;
        
        Quaternion droneStartRot = masterDrone.transform.rotation;
        Quaternion droneEndRot = Quaternion.Euler(0, currentRespawnPoint.eulerAngles.y, 0);

        elapsed = 0f;
        while (elapsed < panDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / panDuration);

            // By moving the Master Drone, the two child drones perfectly maintain their height and offset!
            masterDrone.transform.position = Vector3.Lerp(droneStartPos, droneEndPos, t);
            masterDrone.transform.rotation = Quaternion.Slerp(droneStartRot, droneEndRot, t);
            
            yield return null;
        }


        // --- PHASE 3: THE DROP IN ---
        Vector3 finalSpawnPos = currentRespawnPoint.position;
        Vector3 ceilingPos = finalSpawnPos + new Vector3(0, dropHeight, 0);
        
        transform.position = ceilingPos;
        transform.rotation = Quaternion.Euler(0, currentRespawnPoint.eulerAngles.y, 0);

        elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float tFall = Mathf.Pow(elapsed / fallDuration, 2f);
            
            transform.position = Vector3.Lerp(ceilingPos, finalSpawnPos, tFall);
            yield return null;
        }

        transform.position = finalSpawnPos;


        // --- PHASE 4: MICRO-SETTLE ---
        // Just in case idle breathing animations shifted the player's bones by a millimeter, 
        // we micro-glide the two child drones directly onto the final bone targets.
        if (originalTracking != null && originalLookAt != null)
        {
            elapsed = 0f;
            Vector3 trackStartPos = trackingDrone.transform.position;
            Quaternion trackStartRot = trackingDrone.transform.rotation;
            
            Vector3 lookStartPos = lookAtDrone.transform.position;
            Quaternion lookStartRot = lookAtDrone.transform.rotation;

            while (elapsed < cameraSettleDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / cameraSettleDuration);
                
                trackingDrone.transform.position = Vector3.Lerp(trackStartPos, originalTracking.position, t);
                trackingDrone.transform.rotation = Quaternion.Slerp(trackStartRot, originalTracking.rotation, t);
                
                lookAtDrone.transform.position = Vector3.Lerp(lookStartPos, originalLookAt.position, t);
                lookAtDrone.transform.rotation = Quaternion.Slerp(lookStartRot, originalLookAt.rotation, t);
                
                yield return null;
            }
        }

        // --- 5. CLEANUP ---
        Destroy(masterDrone); // Destroys the children too!
        if (camCollider != null) camCollider.enabled = true; 
        
        // Zero snap, as they are now perfectly occupying the exact same space
        virtualCam.Target.TrackingTarget = originalTracking;
        virtualCam.Target.LookAtTarget = originalLookAt;
        
        controller.enabled = true;
        if (movementScript != null) movementScript.enabled = true;
        
        isRespawning = false;
    }

    public void UpdateRespawnPoint(Transform newPoint)
    {
        currentRespawnPoint = newPoint;
    }
}