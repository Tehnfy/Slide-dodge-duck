using UnityEngine;
using UnityEngine.SceneManagement;

public class RespawnLevel : MonoBehaviour
{
    void Start()
    {
        SceneManager.LoadScene(4);
    }

}
