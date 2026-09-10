using UnityEngine;

public class Spin : MonoBehaviour
{
    void Update()
    {
        transform.Rotate(0, 50f * Time.deltaTime, 0);
    }
}
