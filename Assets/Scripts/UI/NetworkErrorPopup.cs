using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;



/// <summary>
/// 네트워크 오류 팝업
/// "네트워크가 불안정합니다. 네트워크를 확인 후 다시 접속해주세요."
/// </summary>
public class NetworkErrorPopup : MonoBehaviour
{
   [Header("UI References")]
[SerializeField] private GameObject popupPanel;
[SerializeField] private TMP_Text messageText;
[SerializeField] private Button confirmButton;

[Header("Localized Message")]
[SerializeField] private LocalizedString networkUnstableMessage;


    private static NetworkErrorPopup instance;
    public static NetworkErrorPopup Instance
    {
        get
        {
            if (instance == null)
            {
                // Resources 폴더에서 프리팹 로드 또는 동적 생성
                var prefab = Resources.Load<NetworkErrorPopup>("Popups/NetworkErrorPopup");
                if (prefab != null)
                {
                    instance = Instantiate(prefab);
                    DontDestroyOnLoad(instance.gameObject);
                }
                else
                {
                    Debug.LogError("[NetworkErrorPopup] 프리팹을 찾을 수 없습니다.");
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // 초기 상태: 숨김
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }

        // 확인 버튼 이벤트
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(Hide);
        }
    }

    /// <summary>
    /// 네트워크 오류 팝업 표시
    /// </summary>
    public void Show()
{
    if (popupPanel != null)
    {
        popupPanel.SetActive(true);
        messageText.text = networkUnstableMessage.GetLocalizedString();
    }
}

    /// <summary>
    /// 팝업 숨김
    /// </summary>
    public void Hide()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(Hide);
        }
    }
}
