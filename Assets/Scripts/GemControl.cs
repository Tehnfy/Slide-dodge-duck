using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UIElements;

public class GemControl : MonoBehaviour
{

    [Header("Rotate")]
    [SerializeField] float rotateSpeedY = 0.3f;
    [Header("Bounce")]
    [SerializeField] float bounceSpeed = 0.5f;
    [SerializeField] float bounceFreq = 1f;
    private Vector3 startPos;
    [SerializeField] AudioSource gemCollect;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        transform.Rotate(0, rotateSpeedY * Time.deltaTime, 0, Space.World);
        
        float newY = startPos.y + Mathf.Sin(Time.time * bounceFreq) * bounceSpeed;
        transform.localPosition = new Vector3(startPos.x, newY, startPos.z);
    }

    void OggerEnter(Collider other)
    {
        gemCollect.Play();
        Destroy(gameObject);        
    }
}

