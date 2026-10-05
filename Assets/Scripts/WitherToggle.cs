using UnityEngine;

// Space fades the global _Wither value between bloom (0) and withered (1).
// The blossom, outline and paper shaders all read it.
public class WitherToggle : MonoBehaviour
{
    public float duration = 1.5f;
    static readonly int WitherId = Shader.PropertyToID("_Wither");
    float target;
    float current;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) Toggle();
        current = Mathf.MoveTowards(current, target, Time.deltaTime / duration);
        Shader.SetGlobalFloat(WitherId, current);
    }

    public void Toggle()
    {
        target = 1 - target;
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat(WitherId, 0);
    }
}
