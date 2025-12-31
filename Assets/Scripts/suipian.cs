using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class suipian : MonoBehaviour
{
    [Header("UI")]
    public Image uiImage; // Assign the Image (inside a Canvas) to show when clicked
    public Sprite spriteToShow; // Optional: set a sprite to display on the image (will override current sprite if set)
    public bool setSprite = true;
    public bool closeOnImageClick = true; // If true, clicking the image will hide it
    public float autoHideAfter = 0f; // Seconds to auto-hide; 0 = never auto-hide

    Camera mainCam;
    Coroutine hideCoroutine;

    void Start()
    {
        mainCam = Camera.main;
        if (uiImage != null) uiImage.gameObject.SetActive(false);
    }

    void Update()
    {
        // Mouse click (Editor / Standalone)
        if (Input.GetMouseButtonDown(0))
        {
            TryHandlePointer(Input.mousePosition);
        }

        // Touch (mobile)
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                TryHandlePointer(t.position);
            }
        }
    }

    void TryHandlePointer(Vector2 screenPos)
    {
        if (mainCam == null) mainCam = Camera.main;
        Ray ray = mainCam.ScreenPointToRay(screenPos);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f))
        {
            if (hit.collider != null && hit.collider.gameObject == this.gameObject)
            {
                ShowImage();
            }
        }
    }

    public void ShowImage()
    {
        if (uiImage == null) return;

        uiImage.gameObject.SetActive(true);
        if (setSprite && spriteToShow != null)
        {
            uiImage.sprite = spriteToShow;
            uiImage.SetNativeSize();
        }

        // Add click handler to the image if close on click is desired
        if (closeOnImageClick)
        {
            Button btn = uiImage.GetComponent<Button>();
            if (btn == null)
            {
                // Add a transparent button to capture clicks
                btn = uiImage.gameObject.AddComponent<Button>();
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(HideImage);
        }

        // Auto-hide if requested
        if (autoHideAfter > 0f)
        {
            if (hideCoroutine != null) StopCoroutine(hideCoroutine);
            hideCoroutine = StartCoroutine(AutoHideCoroutine(autoHideAfter));
        }
    }

    public void HideImage()
    {
        if (uiImage == null) return;
        uiImage.gameObject.SetActive(false);
        // Clean up button listener if we added one
        Button btn = uiImage.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            // Optionally remove the Button component if you want (not removing to avoid losing other attached handlers)
        }

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }

    IEnumerator AutoHideCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideImage();
    }
}
