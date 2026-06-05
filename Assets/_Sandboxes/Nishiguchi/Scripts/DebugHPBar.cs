using UnityEngine;
using UnityEngine.InputSystem;
using Uraty.Features.Character;

public class DebugHPBar : MonoBehaviour
{
    [SerializeField] private HPSystem _hpSystem;

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            _hpSystem.DamageHP(100.0f);
        }
        if(Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            _hpSystem.HealHP(100.0f);
        }
    }
}
