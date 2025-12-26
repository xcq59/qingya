using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class kaichang : MonoBehaviour
{
    [Tooltip("要在开始时显示并在首次点击后隐藏的 Panel；若不设置，则使用当前 GameObject。")]
    public GameObject panel;

    bool hidden = false;

    void Start()
    {
        if (panel == null) panel = this.gameObject;
        // 确保开始时显示
        panel.SetActive(true);
    }

    void Update()
    {
        if (hidden) return;

        // 鼠标或触摸开始
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            panel.SetActive(false);
            hidden = true;
            // 禁用脚本以节省开销
            enabled = false;
        }
    }
}
