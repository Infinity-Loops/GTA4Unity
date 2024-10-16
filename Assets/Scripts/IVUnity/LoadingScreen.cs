using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
    public Slider progressBar;
    public TMP_Text loadingText;

    public void SetupLoadingTarget(int max)
    {
        progressBar.maxValue = max;
    }

    public void ResetProgress()
    {
        progressBar.value = 0;
    }

    public void AdvanceProgress(string text)
    {
        loadingText.text = text;
        progressBar.value++;
    }
}
