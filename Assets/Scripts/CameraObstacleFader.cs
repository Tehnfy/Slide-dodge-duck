using System.Collections.Generic;
using UnityEngine;

public class CameraObstacleFader : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("Drag your CamFollowRigg (or Player chest) here!")]
    public Transform playerTarget;
    
    [Tooltip("The tag you created for x-ray objects")]
    public string obstacleTag = "CameraIgnore_Obstacle";

    [Space]
    [Header("Visuals")]
    [Tooltip("Create a transparent/hologram material and drag it here")]
    public Material xrayMaterial; 

    // A dictionary to remember the exact original materials of every object we touch
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    
    // A list of what we are currently looking through
    private List<Renderer> currentlyFadedRenderers = new List<Renderer>();

    private void Update()
    {
        if (playerTarget == null) return;

        // 1. Draw a laser from the Camera to the Player's chest
        Vector3 direction = playerTarget.position - transform.position;
        float distance = direction.magnitude;

        // Shoot a Raycast that hits EVERYTHING between the camera and player
        RaycastHit[] hits = Physics.RaycastAll(transform.position, direction.normalized, distance);

        List<Renderer> hitsThisFrame = new List<Renderer>();

        // 2. Check everything the laser touched
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag(obstacleTag))
            {
                Renderer rnd = hit.collider.GetComponent<Renderer>();
                if (rnd != null)
                {
                    hitsThisFrame.Add(rnd);
                    
                    // If we haven't already faded this object, fade it!
                    if (!currentlyFadedRenderers.Contains(rnd))
                    {
                        FadeOut(rnd);
                    }
                }
            }
        }

        // 3. Restore any objects we are no longer looking through
        for (int i = currentlyFadedRenderers.Count - 1; i >= 0; i--)
        {
            Renderer rnd = currentlyFadedRenderers[i];
            if (!hitsThisFrame.Contains(rnd))
            {
                FadeIn(rnd);
                currentlyFadedRenderers.RemoveAt(i);
            }
        }
    }

    private void FadeOut(Renderer rnd)
    {
        // Memorize the object's original materials before we change them
        if (!originalMaterials.ContainsKey(rnd))
        {
            originalMaterials[rnd] = rnd.materials; 
        }

        // Create a temporary array of our X-Ray material to replace all sub-meshes
        Material[] xrayMats = new Material[rnd.materials.Length];
        for (int i = 0; i < xrayMats.Length; i++)
        {
            xrayMats[i] = xrayMaterial; 
        }
        
        rnd.materials = xrayMats;
        currentlyFadedRenderers.Add(rnd);
    }

    private void FadeIn(Renderer rnd)
    {
        // Put the original materials back exactly as we found them
        if (originalMaterials.ContainsKey(rnd))
        {
            rnd.materials = originalMaterials[rnd];
            originalMaterials.Remove(rnd);
        }
    }
}