using UnityEngine;

public class Generator : MonoBehaviour
{
    [Header("射出対象")]
    [SerializeField] private GameObject emeraldObj;

    [Header("射出設定")]
    [SerializeField] private float shotPower = 10.0f;
    [SerializeField] private float upwardPower = 2.0f;
    //[SerializeField] private float StartAngle = 20.0f;
    //[SerializeField] private float StartTime = 10.0f;

    [Header("インターバル")]
    [SerializeField] private float IntervalTime = 10.0f;

    private Vector3[] Directions ={
        new Vector3( 2, 0,  1).normalized,
        new Vector3( 1, 0,  2).normalized,
        new Vector3( -1, 0,  2).normalized,
        new Vector3(-2, 0,  1).normalized
    };

    private int currentIndex = 0;
    private float timer = 0.0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentIndex = 0;
        timer = 0.0f;
    }

    // Update is called once per frame
    void Update()
    {
        if (emeraldObj == null) return;

        timer += Time.deltaTime;

        if (timer >= IntervalTime){
            timer = 0.0f;
            SpawnEmerald();

            currentIndex++;

            if (currentIndex >= Directions.Length * 2){
                currentIndex = 0;
            }
        }
    }

    private void SpawnEmerald()
    {
        Vector3 dir = Directions[currentIndex / 2];
        if (currentIndex % 2 == 1){
            dir = -dir;
        }

        Vector3 spawnPos = transform.position + Vector3.up;

        GameObject emerald = Instantiate(emeraldObj, spawnPos, Quaternion.identity);
        Rigidbody rb = emerald.GetComponent<Rigidbody>();
        if (rb != null){
            Vector3 finalDir = (dir + Vector3.up * upwardPower).normalized;
            rb.AddForce(finalDir * shotPower, ForceMode.Impulse);
        }
    }
}
