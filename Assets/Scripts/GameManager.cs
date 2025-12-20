using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        Normal, // Can click player to enter Movement mode
        Movement, // Player is highlighted, can click to move
        Painting // Painting mode active
    }

    public GameState CurrentState { get; private set; } = GameState.Normal;

    public event Action<GameState> OnStateChanged;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SetState(GameState newState)
    {
        if (CurrentState == newState) return;
        
        CurrentState = newState;
        OnStateChanged?.Invoke(CurrentState);
        Debug.Log($"Game State Changed to: {CurrentState}");
    }

    public void TogglePaintingMode()
    {
        if (CurrentState == GameState.Painting)
        {
            SetState(GameState.Normal);
        }
        else
        {
            // Exit movement mode if active
            if (CurrentState == GameState.Movement)
            {
                // Logic to deselect player handled by listeners
            }
            SetState(GameState.Painting);
        }
    }
}
