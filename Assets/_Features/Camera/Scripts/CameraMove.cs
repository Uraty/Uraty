using UnityEngine;

public class CameraMove : MonoBehaviour
{
    [Header("追従対象")]
    [SerializeField] private GameObject targetObj;

    [Header("位置調整")]
    [SerializeField] private Vector3 Offset = new Vector3(0.0f, 0.0f, 0.0f);


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    private void Update()
    {
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if(targetObj == null) return;

        transform.position = targetObj.transform.position + Offset;
        transform.LookAt(targetObj.transform);
    }
}
