using UnityEngine; 
using UnityEditor;

[ExecuteInEditMode]
public class SetMaterialProperties : MonoBehaviour
{
    public Texture detailTexture;
    public float detailStep;
    public Vector2 detailScale;

    // Start is called before the first frame update
    void Start() 
    {
        SetProperties();
    }

    void Update()
    {
        SetProperties(); // this is dumb, but the script keeps turning off
    }

    void Awake()
    {
        SetProperties();
    }

    void OnValidate()
    {
        SetProperties();
    }

    void SetProperties()
    {
        var rndr = GetComponent<Renderer>();

        var propertyBlock = new MaterialPropertyBlock();
        rndr.GetPropertyBlock(propertyBlock);

        if (detailTexture != null)
        {
            propertyBlock.SetTexture("_DetailTexture", detailTexture);
            propertyBlock.SetFloat("_DetailStep", detailStep);
            propertyBlock.SetVector("_DetailScale", detailScale);
        }

        rndr.SetPropertyBlock(propertyBlock);
    }
}
