using UnityEngine;

// Cycles this object's material through the list each time Space is pressed.
public class MaterialSwapper : MonoBehaviour
{
    public Material[] materials;
    MeshRenderer meshRenderer;
    int index;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) SwapToNextMaterial();
    }

    public void SwapToNextMaterial()
    {
        index = (index + 1) % materials.Length;
        meshRenderer.sharedMaterial = materials[index];
    }
}
