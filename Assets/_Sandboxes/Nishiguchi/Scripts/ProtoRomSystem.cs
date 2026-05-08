using UnityEngine;

using Uraty.Systems.Input;

public class ProtoRomSystem : MonoBehaviour
{
    [SerializeField] private GameInput _gameInput;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        _gameInput.EnableUIInput();
    }
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
