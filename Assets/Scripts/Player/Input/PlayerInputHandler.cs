using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace Hanzo.Player.Input
{
public class PlayerInputHandler : MonoBehaviour
{
[Header("Input Settings")]
[SerializeField] private float inputDeadzone = 0.1f;

    public PlayerInputActions inputActions;
    
    // Events for input
    public event Action<Vector2> OnMoveInput;
    public event Action OnDashInput;
    
    public Vector2 MoveInput { get; private set; }
    
    private void Awake()
    {
        inputActions = new PlayerInputActions();
        
        // Bind input events
        inputActions.Player.Move.performed += OnMovePerformed;
        inputActions.Player.Move.canceled += OnMoveCanceled;
        
        // Bind dash input (you'll need to add this action in your Input Actions asset)
        // For now, using Space key as default
        inputActions.Player.Dash.performed += OnDashPerformed;
    }
    
    private void OnEnable()
    {
        inputActions?.Enable();
    }
    
    private void OnDisable()
    {
        inputActions?.Disable();
    }
    
    private void OnDestroy()
    {
        inputActions?.Dispose();
    }
    
    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();
        MoveInput = input.magnitude > inputDeadzone ? input : Vector2.zero;
        OnMoveInput?.Invoke(MoveInput);
    }
    
    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        MoveInput = Vector2.zero;
        OnMoveInput?.Invoke(MoveInput);
    }
    
    private void OnDashPerformed(InputAction.CallbackContext context)
    {
        OnDashInput?.Invoke();
        
    }
}


}