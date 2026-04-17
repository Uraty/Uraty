using UnityEngine;

public class SE : MonoBehaviour
{
    [SerializeField] private AudioClip seGet;
    [SerializeField] private float seVolume = 1.0f;
    [SerializeField] private GameObject playerObj;

    private bool isTaken = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == playerObj){
            isTaken = true;

            if (SoundManager.Instance != null){
                SoundManager.Instance.PlaySE(seGet, seVolume);
            }
        }
    }
}
