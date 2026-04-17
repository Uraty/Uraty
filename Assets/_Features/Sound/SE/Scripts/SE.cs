using UnityEngine;

public class SE : MonoBehaviour
{
    [SerializeField] private AudioClip seGet;
    [SerializeField] private float seVolume = 1.0f;
    [SerializeField] private GameObject playerObj;

    private bool isTaken = false;

    private void OnTriggerEnter(Collider other)
    {
        //if (isTaken) return;
        Debug.Log("当たり");
        if (other.gameObject == playerObj){
            Debug.Log("当たり");
            isTaken = true;

            if (SoundManager.Instance != null){
                SoundManager.Instance.PlaySE(seGet, seVolume);
            }

            //Destroy(gameObject);
        }
    }
}
