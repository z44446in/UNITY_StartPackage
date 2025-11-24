using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Components;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.Localization;

/// <summary>
/// 로그인 Panel
/// - 로그인하기: Firebase Auth 자동 로그인 확인 → MainHome / 신규 사용자 → 회원가입 안내
/// - 회원가입하기: SignUpPanel로 이동
/// ✅ 수정: 자동 로그인 로직 구현
/// </summary>
public class LoginPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button loginButton;
    [SerializeField] private Button signUpButton;

    [Header("Loading")]
    [SerializeField] private GameObject loadingIndicator;

    [Header("Localized Strings")]
    [SerializeField] private LocalizedString buttonLoginText;
    [SerializeField] private LocalizedString buttonSignUpText;

    private UIManager uiManager;
    private AuthManager authManager;
    private SceneTransitionManager sceneTransitionManager;

    private void Awake()
    {
        uiManager = FindAnyObjectByType<UIManager>();
        authManager = AuthManager.Instance;
        sceneTransitionManager = SceneTransitionManager.Instance;

        loginButton.onClick.AddListener(OnLoginButtonClicked);
        signUpButton.onClick.AddListener(OnSignUpButtonClicked);
        
        UpdateButtonTexts();
    }

    private void OnEnable()
    {
        UpdateButtonTexts();
    }

    private void OnDestroy()
    {
        loginButton.onClick.RemoveListener(OnLoginButtonClicked);
        signUpButton.onClick.RemoveListener(OnSignUpButtonClicked);
    }

    /// <summary>
    /// 버튼 텍스트 업데이트
    /// </summary>
    private void UpdateButtonTexts()
    {
        TextMeshProUGUI loginButtonText = loginButton.GetComponentInChildren<TextMeshProUGUI>();
        TextMeshProUGUI signUpButtonText = signUpButton.GetComponentInChildren<TextMeshProUGUI>();

        if (loginButtonText != null && buttonLoginText != null && !buttonLoginText.IsEmpty)
        {
            loginButtonText.text = buttonLoginText.GetLocalizedString();
        }

        if (signUpButtonText != null && buttonSignUpText != null && !buttonSignUpText.IsEmpty)
        {
            signUpButtonText.text = buttonSignUpText.GetLocalizedString();
        }
    }

    /// <summary>
    /// ✅ 수정: 로그인하기 버튼 클릭 - Firebase Auth 자동 로그인 확인
    /// </summary>
    private async void OnLoginButtonClicked()
    {
        SetLoadingState(true);

        try
        {
            // Firebase Auth 자동 로그인 확인
            var currentUser = authManager.CurrentUser;

            if (currentUser != null)
            {
                // 자동 로그인 성공 - Firestore에 사용자 데이터 있는지 확인
                Debug.Log($"[LoginPanel] 자동 로그인 감지: {currentUser.UserId}");

                bool isNewUser = await authManager.IsNewUser(currentUser);

                if (isNewUser)
                {
                    // Firestore에 데이터 없음 - 회원가입 필요
                    Debug.Log("[LoginPanel] Firestore에 사용자 데이터 없음 - 회원가입 필요");
                    ShowSignUpRequiredPopup();
                }
                else
                {
                    // 기존 사용자 - MainHome으로 이동
                    Debug.Log("[LoginPanel] 기존 사용자 확인 - MainHome으로 이동");
                    sceneTransitionManager.LoadScene("MainHome");
                }
            }
            else
            {
                // 로그인 정보 없음 - 회원가입 필요 안내
                Debug.Log("[LoginPanel] 로그인 정보 없음 - 회원가입 필요");
                ShowSignUpRequiredPopup();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LoginPanel] 로그인 확인 실패: {e.Message}");
            
            if (e.Message.Contains("network") || e.Message.Contains("Network"))
            {
                NetworkErrorPopup.Instance?.Show();
            }
            else
            {
                ShowSignUpRequiredPopup();
            }
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    /// <summary>
    /// 회원가입하기 버튼 클릭
    /// </summary>
    private void OnSignUpButtonClicked()
    {
        uiManager.ShowSignUpPanel();
    }

    /// <summary>
    /// "회원가입이 필요합니다" 팝업 표시
    /// </summary>
    private void ShowSignUpRequiredPopup()
    {
        // TODO: 전용 팝업 UI 구현
        // 임시로 SignUpPanel로 이동
        Debug.Log("[LoginPanel] 회원가입 필요 - SignUpPanel로 이동");
        uiManager.ShowSignUpPanel();
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

        loginButton.interactable = !isLoading;
        signUpButton.interactable = !isLoading;
    }
}