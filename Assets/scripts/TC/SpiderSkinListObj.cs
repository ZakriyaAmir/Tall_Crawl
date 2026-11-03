using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Component on the "spiderSkinListObj" prefab used to represent a single skin list item.
public class SpiderSkinListObj : MonoBehaviour
{
    public Button button;
    public Image cardImage;
    public Image accentImage;
    public TextMeshProUGUI priceLabel;
    public Image lockIcon;
    public Image checkIcon;
    public int skinIndex;

    void Awake()
    {
        BindComponents();
    }

    public void BindComponents()
    {
        if (!button) button = GetComponent<Button>();
        if (!cardImage) cardImage = GetComponent<Image>();
        if (!accentImage)
        {
            var tr = transform.Find("Accent");
            if (tr) accentImage = tr.GetComponent<Image>();
        }
        if (!priceLabel)
        {
            var tr = transform.Find("Price");
            if (tr) priceLabel = tr.GetComponent<TextMeshProUGUI>();
        }
        if (!lockIcon)
        {
            var tr = transform.Find("Lock");
            if (tr) lockIcon = tr.GetComponent<Image>();
        }
        if (!checkIcon)
        {
            var tr = transform.Find("Check");
            if (tr) checkIcon = tr.GetComponent<Image>();
        }
    }

    public void Setup(int index, SkinDef def, Action onClick)
    {
        BindComponents();
        skinIndex = index;
        if (accentImage && def != null) accentImage.color = def.accent;
        if (button)
        {
            button.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                button.onClick.AddListener(() =>
                {
                    TCAudio.Click();
                    onClick();
                });
            }
        }
    }
}

