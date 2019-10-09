using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterController : MonoBehaviour
{
    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    public void StartTalking()
    {
        if (Random.Range(0,2) == 0)
        {
            _animator.SetTrigger("startTalking_a");
        }
        else
        {
            _animator.SetTrigger("startTalking_b");
        }
    }

    public void StopTalking()
    {
        _animator.SetTrigger("stopTalking");
    }
}
