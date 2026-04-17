//using System.Collections.Generic;

using UnityEngine;

public class Generator : MonoBehaviour
{
    [Header("射出対象")]
    [SerializeField] private GameObject gummyObj;

    [Header("射出設定")]
    [SerializeField] private float shotPower = 10.0f;
    [SerializeField] private float upwardPower = 2.0f;
    [SerializeField] private float offsetAngle = 20.0f;
    [SerializeField] private int shotEqualDivision = 4;
    //[SerializeField] private float StartAngle = 20.0f;
    //[SerializeField] private float StartTime = 10.0f;

    [Header("生成の最大数")]
    [SerializeField] private int gummyMax = 10;

    [Header("インターバル")]
    [SerializeField] private float IntervalTime = 10.0f;

    //private Vector3[] Directions ={
    //    new Vector3( 2, 0,  1).normalized,
    //    new Vector3( 1, 0,  2).normalized,
    //    new Vector3( -1, 0,  2).normalized,
    //    new Vector3(-2, 0,  1).normalized
    //};

    private Vector3[] Directions;

    private int currentIndex = 0;
    private float timer = 0.0f;
    private float shotDis;
    //private List<GameObject> activegummys = new List<GameObject>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentIndex = 0;
        timer = 0.0f;
        shotDis = (180.0f - (offsetAngle * 2)) / (shotEqualDivision - 1);

        if (gummyMax < 0){
            gummyMax = 0;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (gummyObj == null) return;
        if (IntervalTime <= 0.0f) return;

        timer += Time.deltaTime;

        if (timer >= IntervalTime){
            timer = 0.0f;
            SpawnGummy3();

            currentIndex++;

            if (currentIndex >= shotEqualDivision * 2){
                currentIndex = 0;
            }
        }
    }

    private void SpawnGummy()
    {
        Vector3 dir = Directions[currentIndex / 2];
        if (currentIndex % 2 == 1){
            dir = -dir;
        }

        Vector3 spawnPos = transform.position + Vector3.up;

        GameObject gummy = Instantiate(gummyObj, spawnPos, Quaternion.identity);
        Rigidbody rb = gummy.GetComponent<Rigidbody>();
        if (rb != null){
            Vector3 finalDir = (dir + Vector3.up * upwardPower).normalized;
            rb.AddForce(finalDir * shotPower, ForceMode.Impulse);
        }
    }

    private void SpawnGummy2()
    {
        float baseAngle = currentIndex / 2 * offsetAngle;

        if (currentIndex % 2 == 1){
            baseAngle += 180.0f;
        }

        Vector3 dir = AngleToDir(baseAngle);

        Vector3 spawnPos = transform.position + Vector3.up;
        GameObject gummy = Instantiate(gummyObj, spawnPos, Quaternion.identity);
        Rigidbody rb = gummy.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 finalDir = (dir + Vector3.up * upwardPower).normalized;
            rb.AddForce(finalDir * shotPower, ForceMode.Impulse);
        }
    }

    private void SpawnGummy3()
    {
        float shotAngle = shotDis * (currentIndex / 2) + offsetAngle;

        if (currentIndex % 2 == 1){
            shotAngle += 180.0f;
        }

        Vector3 dir = AngleToDir(shotAngle);

        Vector3 spawnPos = transform.position + Vector3.up;
        GameObject gummy = Instantiate(gummyObj, spawnPos, Quaternion.identity);
        Rigidbody rb = gummy.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 finalDir = (dir + Vector3.up * upwardPower).normalized;
            rb.AddForce(finalDir * shotPower, ForceMode.Impulse);
        }
    }

    private Vector3 AngleToDir(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad), 0.0f, Mathf.Sin(rad));
    }
}
