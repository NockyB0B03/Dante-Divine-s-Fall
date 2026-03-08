using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ComboManager : MonoBehaviour
{
    [Header("Combo Settings")]
    public float comboWindowDuration = 0.5f;   // window after anim ends
    public string[] punchTriggers = {           // Animator trigger names
        "Punch1","Punch2","Punch3","Punch4","Punch5"
    };

    private Animator _anim;
    private PlayerInputActions _input;
    private Queue<int> _comboQueue = new Queue<int>();
    private int _currentIndex = -1;
    private bool _isAttacking;
    private Coroutine _windowRoutine;

    void Awake()
    {
        _anim = GetComponentInChildren<Animator>();
        _input = new PlayerInputActions();
    }

    void OnEnable() { _input.Enable(); _input.Player.Punch.performed += OnPunch; }
    void OnDisable() { _input.Disable(); _input.Player.Punch.performed -= OnPunch; }

    void OnPunch(InputAction.CallbackContext ctx)
    {
        int next = (_currentIndex + 1) % punchTriggers.Length;

        if (!_isAttacking)
        {
            ExecuteAttack(next);
        }
        else if (_comboQueue.Count < 1)   // buffer max 1 ahead
        {
            _comboQueue.Enqueue(next);
        }
    }

    void ExecuteAttack(int index)
    {
        _currentIndex = index;
        _isAttacking = true;
        _anim.SetTrigger(punchTriggers[index]);
    }

    // Called by Animation Event at the END of each punch clip
    public void OnAttackAnimationEnd()
    {
        if (_comboQueue.Count > 0)
        {
            ExecuteAttack(_comboQueue.Dequeue());
        }
        else
        {
            // Open combo window — if no input, reset
            if (_windowRoutine != null) StopCoroutine(_windowRoutine);
            _windowRoutine = StartCoroutine(ComboWindowRoutine());
        }
    }

    IEnumerator ComboWindowRoutine()
    {
        yield return new WaitForSeconds(comboWindowDuration);
        ResetCombo();
    }

    void ResetCombo()
    {
        _isAttacking = false;
        _currentIndex = -1;
        _comboQueue.Clear();
    }
}