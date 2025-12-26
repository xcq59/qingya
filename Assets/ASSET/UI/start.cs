using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class start : MonoBehaviour
{
    // 按钮点击时调用（在 Button 的 OnClick 中注册此方法）
    public void StartGame()
    {
        // 载入场景索引为 1 的场景（确保场景已在 Build Settings 中添加）
        SceneManager.LoadScene(1);
    }
}
