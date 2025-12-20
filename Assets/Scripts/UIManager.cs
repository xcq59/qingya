using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public Button inkButton;
    public PaintingSystem paintingSystem;

    void Start()
    {
        if (inkButton != null && paintingSystem != null)
        {
            inkButton.onClick.AddListener(paintingSystem.OnInkButtonClicked);
            
            // Update button visual state
            GameManager.Instance.OnStateChanged += OnGameStateChanged;
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= OnGameStateChanged;
    }

    void OnGameStateChanged(GameManager.GameState newState)
    {
        if (inkButton == null) return;

        ColorBlock colors = inkButton.colors;
        if (newState == GameManager.GameState.Painting)
        {
            colors.normalColor = Color.green; // Active color
            colors.selectedColor = Color.green;
        }
        else
        {
            colors.normalColor = Color.white; // Inactive color
            colors.selectedColor = Color.white;
        }
        inkButton.colors = colors;
    }
}
