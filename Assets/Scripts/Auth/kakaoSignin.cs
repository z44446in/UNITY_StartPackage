using UnityEngine;
using System;

public class kakaoSignin : MonoBehaviour
{
    // 외부(AuthManager)에서 구독할 이벤트들
    public event Action<string> OnKakaoSucceeded;  // 카카오 accessToken
    public event Action<string> OnKakaoFailed;     // 에러 메시지
    public event Action OnKakaoCanceled;

    private AndroidJavaObject _androidJavaObject;

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _androidJavaObject = new AndroidJavaObject("com.company.PROJECT_NAME.UKakao");
#endif
    }

    /// 호출 시작점 (버튼/코드에서 호출)
    public void BeginKakaoLogin()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // 방법 B: 이 컴포넌트가 달린 GameObject 이름으로 콜백을 받음
        _androidJavaObject?.Call("KakaoLogin", gameObject.name);
#else
        Debug.LogWarning("Kakao login runs on Android device.");
#endif
    }

    // ===== Kotlin → UnitySendMessage 콜백 수신 메서드 =====
    public void OnKakaoLoginSuccess(string accessToken)
    {
        Debug.Log("[KAKAO] success: " + accessToken);
        OnKakaoSucceeded?.Invoke(accessToken);
    }

    public void OnKakaoLoginFail(string message)
    {
        Debug.LogError("[KAKAO] fail: " + message);
        OnKakaoFailed?.Invoke(message);
    }

    public void OnKakaoLoginCancel(string _)
    {
        Debug.Log("[KAKAO] cancel");
        OnKakaoCanceled?.Invoke();
    }
}
