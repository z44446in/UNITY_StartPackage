using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Components;
using System.Threading.Tasks;
using UnityEngine.Localization;

/// <summary>
/// 추천인 코드 입력 Panel
/// 추천인 코드 입력 및 검증 처리
/// ✅ 수정: 본인 코드 입력 방지, 네트워크 오류 처리
/// </summary>
public class ReferralCodePanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField referralCodeInputField;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private Button backButton;

    [Header("Error Message")]
    [SerializeField] private GameObject errorMessageObject;
    [SerializeField] private TextMeshProUGUI errorMessageText;

    [Header("Loading")]
    [SerializeField] private GameObject loadingIndicator;

    [Header("Error Messages - Localized")]
    [SerializeField] private LocalizedString errorEmptyCode;
    [SerializeField] private LocalizedString errorInvalidFormat;
    [SerializeField] private LocalizedString errorOwnCode;
    [SerializeField] private LocalizedString errorNotFound;
    [SerializeField] private LocalizedString errorNetwork;

    private UIManager uiManager;
    private DataManager dataManager;
    private AuthManager authManager;

    private void Awake()
    {
        uiManager = FindAnyObjectByType<UIManager>();
        dataManager = DataManager.Instance;
        authManager = AuthManager.Instance;

        confirmButton.onClick.AddListener(OnConfirmButtonClicked);
        skipButton.onClick.AddListener(OnSkipButtonClicked);

        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackButtonClicked);
        }

        referralCodeInputField.onValueChanged.AddListener(OnInputFieldChanged);

        if (errorMessageObject != null)
        {
            errorMessageObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        confirmButton.onClick.RemoveListener(OnConfirmButtonClicked);
        skipButton.onClick.RemoveListener(OnSkipButtonClicked);

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(OnBackButtonClicked);
        }

        referralCodeInputField.onValueChanged.RemoveListener(OnInputFieldChanged);
    }

    /// <summary>
    /// ✅ 수정: 확인 버튼 클릭 - 본인 코드 입력 방지
    /// </summary>
    private async void OnConfirmButtonClicked()
    {
        // 1. 입력값 검증
        string inputCode = referralCodeInputField.text.Trim();

        if (string.IsNullOrEmpty(inputCode))
        {
            ShowErrorMessage(errorEmptyCode);
            return;
        }

        if (!ValidationHelper.IsValidReferralCode(inputCode))
        {
            ShowErrorMessage(errorInvalidFormat);
            return;
        }

        SetLoadingState(true);
        HideErrorMessage();

        try
        {
            string currentUserId = authManager.GetCurrentUserId();

            // 2. 본인 추천인 코드 확인
            var currentUserData = await dataManager.GetUserData(currentUserId);
            if (currentUserData.referralCode == inputCode)
            {
                ShowErrorMessage(errorOwnCode);
                return;
            }

            // 3. 추천인 코드 존재 여부 확인 (탈퇴 회원 코드는 자동 제외됨)
            bool exists = await dataManager.IsReferralCodeExists(inputCode, currentUserId);

            if (!exists)
            {
                ShowErrorMessage(errorNotFound);
                return;
            }

            // 4. ✅ 수정: 보상 지급 제거, referredBy만 저장
            await dataManager.SaveReferredBy(currentUserId, inputCode);

            Debug.Log($"추천인 코드 저장 성공: {inputCode}");

            // 5. 생년월일시 입력 Panel로 이동
            uiManager.ShowBirthInfoPanel();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"추천인 코드 처리 실패: {e.Message}");

            if (e.Message.Contains("network") || e.Message.Contains("Network"))
            {
                NetworkErrorPopup.Instance?.Show();
            }
            else
            {
                ShowErrorMessage(errorNetwork);
            }
        }
        finally
        {
            SetLoadingState(false);
        }
    }


    /// <summary>
    /// 추천인 없음 버튼 클릭
    /// </summary>
    private void OnSkipButtonClicked()
    {
        Debug.Log("추천인 없음 선택");
        uiManager.ShowBirthInfoPanel();
    }

    /// <summary>
    /// 뒤로가기 버튼 클릭
    /// </summary>
    private void OnBackButtonClicked()
    {
        uiManager.GoBack();
    }

    /// <summary>
    /// InputField 값 변경 시 (숫자만 입력, 6자리 제한)
    /// </summary>
    private void OnInputFieldChanged(string value)
    {
        HideErrorMessage();

        // 숫자만 입력 가능하도록 필터링
        string filtered = System.Text.RegularExpressions.Regex.Replace(value, @"[^0-9]", "");

        // 6자리 제한
        if (filtered.Length > 6)
        {
            filtered = filtered.Substring(0, 6);
        }

        if (filtered != value)
        {
            referralCodeInputField.text = filtered;
        }
    }

    #region UI Helpers

    /// <summary>
    /// 에러 메시지 표시
    /// </summary>
    private void ShowErrorMessage(LocalizedString localizedMessage)
    {
        if (errorMessageObject != null)
        {
            errorMessageObject.SetActive(true);
            errorMessageText.text = localizedMessage.GetLocalizedString();
        }
    }

    /// <summary>
    /// 에러 메시지 숨김
    /// </summary>
    private void HideErrorMessage()
    {
        if (errorMessageObject != null)
        {
            errorMessageObject.SetActive(false);
        }
    }

    /// <summary>
    /// 로딩 상태 설정
    /// </summary>
    private void SetLoadingState(bool isLoading)
    {
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(isLoading);
        }

        referralCodeInputField.interactable = !isLoading;
        confirmButton.interactable = !isLoading;
        skipButton.interactable = !isLoading;

        if (backButton != null)
        {
            backButton.interactable = !isLoading;
        }
    }

    #endregion
}