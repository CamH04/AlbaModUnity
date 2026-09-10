using UnityEngine;
using UnityEngine.SceneManagement;

public class BaseButton : MonoBehaviour {
    public void GoToHomeBase() {
        SceneManager.LoadScene("HomeBase");
    }
}
