using UnityEngine;
using UnityEngine.UI;

namespace Settings
{
    public class CreditController : MonoBehaviour
    {
        public GameObject panelRoot;
        public Button closeButton;

        void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void Toggle()
        {
            if (panelRoot == null) return;
            if (panelRoot.activeSelf) Close();
            else Open();
        }

        public void Open()
        {
            if (panelRoot != null) panelRoot.SetActive(true);
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }
    }
}
