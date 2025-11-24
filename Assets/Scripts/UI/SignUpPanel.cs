using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Components;
using System.Threading.Tasks;
using UnityEngine.Localization;
using TMPro;

/// <summary>
/// 회원가입 Panel
/// Google / Apple / KakaoTalk 소셜 로그인 버튼
/// ✅ 수정: 신규/기존 사용자 분기 처리
/// </summary>
public class SignUpPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button googleSignInButton;
    [SerializeField] private Button appleSignInButton;
    [SerializeField] private Button kakaoSignInButton;
    [SerializeField] private Button backButton;

    [Header("Loading")]
    [SerializeField] private GameObject loadingIndicator;

    [Header("Localized Strings")]
    [SerializeField] private LocalizedString buttonGoogleSignIn;
    [SerializeField] private LocalizedString buttonAppleSignIn;
    [SerializeField] private LocalizedString buttonKakaoSignIn;

    private UIManager uiManager;
    private AuthManager authManager;
    private DataManager dataManager;
    private SceneTransitionManager sceneTransitionManager;

    private void Awake()
    {
        uiManager = FindAnyObjectByType<UIManager>();
        authManager = AuthManager.Instance;
        dataManager = DataManager.Instance;
        sceneTransitionManager = SceneTransitionManager.Instance;

        // 버튼 이벤트 등록
        googleSignInButton.onClick.AddListener(() => OnSocialSignInClicked("google"));
        appleSignInButton.onClick.AddListener(() => OnSocialSignInClicked("apple"));
        kakaoSignInButton.onClick.AddListener(() => OnSocialSignInClicked("kakao"));

        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackButtonClicked);
        }

        // 플랫폼별 버튼 활성화/비활성화
        SetupPlatformButtons();

        // 버튼 텍스트 초기화
        UpdateButtonTexts();
    }

    private void OnEnable()
    {
        // Panel 활성화될 때마다 텍스트 업데이트
        UpdateButtonTexts();
    }

    private void OnDestroy()
    {
        googleSignInButton.onClick.RemoveAllListeners();
        appleSignInButton.onClick.RemoveAllListeners();
        kakaoSignInButton.onClick.RemoveAllListeners();

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
        }
    }

    /// <summary>
    /// 버튼 텍스트 업데이트
    /// </summary>
    private void UpdateButtonTexts()
    {
        TextMeshProUGUI googleButtonText = googleSignInButton.GetComponentInChildren<TextMeshProUGUI>();
        TextMeshProUGUI appleButtonText = appleSignInButton.GetComponentInChildren<TextMeshProUGUI>();
        TextMeshProUGUI kakaoButtonText = kakaoSignInButton.GetComponentInChildren<TextMeshProUGUI>();

        if (googleButtonText != null && buttonGoogleSignIn != null && !buttonGoogleSignIn.IsEmpty)
        {
            googleButtonText.text = buttonGoogleSignIn.GetLocalizedString();
        }

        if (appleButtonText != null && buttonAppleSignIn != null && !buttonAppleSignIn.IsEmpty)
        {
            appleButtonText.text = buttonAppleSignIn.GetLocalizedString();
        }

        if (kakaoButtonText != null && buttonKakaoSignIn != null && !buttonKakaoSignIn.IsEmpty)
        {
            kakaoButtonText.text = buttonKakaoSignIn.GetLocalizedString();
        }
    }

    /// <summary>
    /// 플랫폼별 로그인 버튼 활성화/비활성화
    /// </summary>
    private void SetupPlatformButtons()
    {
#if UNITY_IOS
        googleSignInButton.gameObject.SetActive(true);
        appleSignInButton.gameObject.SetActive(true);
        kakaoSignInButton.gameObject.SetActive(false);
#elif UNITY_ANDROID
        googleSignInButton.gameObject.SetActive(true);
        appleSignInButton.gameObject.SetActive(false);
        kakaoSignInButton.gameObject.SetActive(true);
#else
        googleSignInButton.gameObject.SetActive(false);
        appleSignInButton.gameObject.SetActive(false);
        kakaoSignInButton.gameObject.SetActive(false);
        Debug.LogWarning("소셜 로그인은 모바일 플랫폼에서만 작동합니다.");
#endif
    }

    /// <summary>
    /// ✅ 수정: 소셜 로그인 버튼 클릭 - 신규/기존 사용자 분기 처리
    /// </summary>
    private async void OnSocialSignInClicked(string provider)
    {
        Debug.Log($"{provider} 로그인 시도");

        SetLoadingState(true);

        try
        {
            Firebase.Auth.FirebaseUser user = null;

            // 1. 각 플랫폼별 로그인 처리
            switch (provider)
            {
                case "google":
                    user = await authManager.SignInWithGoogle();
                    break;

                case "apple":
                    user = await authManager.SignInWithApple();
                    break;

                case "kakao":
                    user = await authManager.SignInWithKakao();
                    break;

                default:
                    Debug.LogError($"지원하지 않는 로그인 방식: {provider}");
                    return;
            }

            if (user == null)
            {
                throw new System.Exception("로그인 실패: 사용자 정보 없음");
            }

            Debug.Log($"{provider} 로그인 성공: {user.UserId}");

// ✅ 수정: 신규/기존 사용자 확인
bool isNewUser = await authManager.IsNewUser(user);

            if (isNewUser)
            {
                // ✅ 추가: hasData=false인 쓰레기 데이터 삭제
                var existingData = await dataManager.GetUserData(user.UserId);
                if (existingData != null && !existingData.hasData)
                {
                    Debug.Log("[SignUpPanel] hasData=false인 기존 문서 삭제 중...");
                    await dataManager.WithdrawUser(user.UserId);
                }

                // ✅ 추가: 탈퇴 회원 재가입 체크
                bool isWithdrawn = await authManager.IsWithdrawnUser(user);

                // 신규 사용자: Firestore에 사용자 생성
                Debug.Log("[SignUpPanel] 신규 사용자 - Firestore에 데이터 생성 중...");

                string email = user.Email ?? "";

                // ✅ 추가: 탈퇴 회원이면 기존 referralCode 재사용
                if (isWithdrawn)
                {
                    var withdrawnData = await dataManager.GetUserData(user.UserId);

                    // 기존 데이터 재활성화 (isWithdrawn=false, hasData=false로 초기화)
                    await dataManager.UpdateUserData(new UserData(
                        user.UserId,
                        email,
                        provider,
                        withdrawnData.referralCode // 기존 코드 재사용
                    ));

                    // ✅ hasReceivedReferralReward=true면 ReferralCodePanel 건너뛰기
                    if (withdrawnData.hasReceivedReferralReward)
                    {
                        Debug.Log("[SignUpPanel] 탈퇴 회원 재가입 - BirthInfoPanel로 이동");
                        uiManager.ShowBirthInfoPanel();
                    }
                    else
                    {
                        Debug.Log("[SignUpPanel] 탈퇴 회원 재가입 - ReferralCodePanel로 이동");
                        uiManager.ShowReferralCodePanel();
                    }
                }
                else
                {
                    // 완전 신규 회원
                    await dataManager.CreateNewUser(user.UserId, email, provider);
                    Debug.Log("[SignUpPanel] 추천인 코드 입력 화면으로 이동");
                    uiManager.ShowReferralCodePanel();
                }
            }
            else
            {
                // 기존 사용자: MainHome Scene으로 이동
                Debug.Log("[SignUpPanel] 기존 사용자 - MainHome으로 이동");
                sceneTransitionManager.LoadScene("MainHome");
            }

            // 2. ✅ 신규/기존 사용자 확인

        }
        catch (System.Exception e)
        {
            Debug.LogError($"{provider} 로그인 실패: {e.Message}");

            // 네트워크 오류 팝업 표시
            if (e.Message.Contains("network") || e.Message.Contains("Network"))
            {
                NetworkErrorPopup.Instance?.Show();
            }
            else
            {
                ShowErrorMessage($"로그인에 실패했습니다: {e.Message}");
            }
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    /// <summary>
    /// 뒤로가기 버튼 클릭
    /// </summary>
    private void OnBackButtonClicked()
    {
        uiManager.GoBack();
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

        googleSignInButton.interactable = !isLoading;
        appleSignInButton.interactable = !isLoading;
        kakaoSignInButton.interactable = !isLoading;

        if (backButton != null)
        {
            backButton.interactable = !isLoading;
        }
    }

    /// <summary>
    /// 에러 메시지 표시
    /// </summary>
    private void ShowErrorMessage(string message)
    {
        // TODO: 에러 메시지 UI 구현
        Debug.LogWarning($"에러 메시지: {message}");
    }
}