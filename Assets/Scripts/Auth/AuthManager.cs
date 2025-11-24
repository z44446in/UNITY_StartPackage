using System;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Google;
using System.Threading;
using Firebase.Functions;
using Firebase.Firestore;
using UnityEngine.Networking;
using System.Text;
using System.Collections;
using System.Security.Cryptography;
/// <summary>
/// Firebase Auth 중심 인증 관리자
/// Google/Apple/Kakao 소셜 로그인을 Firebase Auth로 통합
/// </summary>
public class AuthManager : MonoBehaviour
{

    private static AuthManager instance;
    public static AuthManager Instance
    {
        get
        {
            if (instance == null)
            {
                var obj = new GameObject("AuthManager");
                instance = obj.AddComponent<AuthManager>();
                DontDestroyOnLoad(obj);
            }
            return instance;
        }
    }

    [Header("Social Login References")]
    [SerializeField] private googleSignin googleSignIn;
    [SerializeField] private appleSignin appleSignIn;
    [SerializeField] private kakaoSignin kakaoSignIn;
    [Header("Endpoints")]
    // 예) https://asia-northeast3-<project-id>.cloudfunctions.net/api/verifyToken
     [SerializeField] private string verifyUrl = "https://YOUR_FUNCTIONS_URL/api/verifyToken";



    private FirebaseAuth auth;
    private FirebaseFirestore db;
    private FirebaseFunctions functions;
    private FirebaseUser currentUser;
    private volatile bool isInitialized = false;
    private readonly SemaphoreSlim _authLock = new SemaphoreSlim(1, 1); // 동시 로그인 방지

    public FirebaseUser CurrentUser => auth?.CurrentUser;
    public bool IsLoggedIn => auth?.CurrentUser != null;
    public string GetCurrentUserId() => auth?.CurrentUser?.UserId;


    private async void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        await InitializeFirebaseAsync();
    }


    #region Firebase Initialization

    /// <summary>
    /// Firebase 초기화 및 자동 로그인 체크
    /// </summary>
    private async Task InitializeFirebaseAsync()
    {
        try
        {
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available)
            {
                Debug.LogError($"[AuthManager] Firebase 초기화 실패: {status}");
                isInitialized = false;
                return;
            }

            db = FirebaseFirestore.DefaultInstance;
            functions = FirebaseFunctions.DefaultInstance;



            auth = FirebaseAuth.DefaultInstance;
            isInitialized = true;

            // 자동 로그인 상태 로그
            if (auth.CurrentUser != null)
            {
                currentUser = auth.CurrentUser;
                Debug.Log($"[AuthManager] 자동 로그인 성공: {currentUser.UserId}");
            }
            else
            {
                Debug.Log("[AuthManager] Firebase 초기화 완료 - 로그인 필요");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AuthManager] Firebase 초기화 중 예외: {ex}");
            isInitialized = false;
        }
    }

    private async Task EnsureInitializedAsync()
    {
        // 이미 초기화 완료면 바로 리턴
        if (isInitialized && auth != null) return;

        // 초기화 진행 중이거나 아직 안 됐을 수 있으니 재호출
        await InitializeFirebaseAsync();
        if (!isInitialized || auth == null)
        {
            throw new InvalidOperationException("Firebase not initialized");
        }
    }
    private void OnEnable()
    {
        if (auth != null)
        {
            auth.StateChanged += OnFirebaseStateChanged;
            auth.IdTokenChanged += OnFirebaseIdTokenChanged;
        }
    }

    private void OnDisable()
    {
        if (auth != null)
        {
            auth.StateChanged -= OnFirebaseStateChanged;
            auth.IdTokenChanged -= OnFirebaseIdTokenChanged;
        }
    }

    private void OnFirebaseStateChanged(object sender, System.EventArgs e)
    {
        var user = auth.CurrentUser;
        if (user == null)
        {
            Debug.Log("[AuthManager] Firebase signed out (no current user).");
        }
        else
        {
            Debug.Log($"[AuthManager] Firebase signed in. uid={user.UserId}, email={user.Email}, displayName={user.DisplayName}");
            // TODO: 여기서 UI 갱신, 다음 씬 전환, 사용자 데이터 로드 등을 수행하세요.
        }
    }

    private void OnFirebaseIdTokenChanged(object sender, System.EventArgs e)
    {
        var user = auth.CurrentUser;
        if (user != null)
        {
            // 토큰 갱신 시점에 필요한 작업(예: 서버와 세션 동기화)이 있으면 추가
            Debug.Log("[AuthManager] Firebase ID token changed.");
        }
    }


    #endregion

    #region Google Sign In

    /// <summary>
    /// Google 로그인 (Firebase Google Provider 사용)
    /// </summary>
    public async Task<FirebaseUser> SignInWithGoogle()
    {
        await EnsureInitializedAsync();

        if (googleSignIn == null)
        {
            Debug.LogError("[AuthManager] googleSignIn 참조가 없습니다 (Inspector 할당 필요).");
            return null;
        }

        await _authLock.WaitAsync();
        try
        {
            // ✅ 수정: googleSignin의 SignInAsync 사용
            var googleUser = await googleSignIn.SignInAsync();

            if (googleUser == null)
            {
                Debug.LogError("[AuthManager] GoogleSignIn이 null을 반환.");
                return null;
            }

            string idToken = googleUser.IdToken;
            if (string.IsNullOrEmpty(idToken))
            {
                Debug.LogError("[AuthManager] Google ID Token이 비어있음 (SHA-1/IdToken 요청 세팅 확인).");
                return null;
            }

            // Firebase Credential 생성
            var credential = GoogleAuthProvider.GetCredential(idToken, null);
            var user = await auth.SignInWithCredentialAsync(credential);
            if (user == null)
            {
                Debug.LogError("[AuthManager] SignInWithCredentialAsync가 null 반환.");
                return null;
            }

            currentUser = user;

            Debug.Log($"[AuthManager] Google 로그인 성공: {currentUser.UserId}");
            return currentUser;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AuthManager] Google 로그인 실패: {e.Message}");
            throw;
        }
        finally
        {
            _authLock.Release();
        }
    }




    #endregion

    #region Apple Sign In

    /// <summary>
    /// Apple 로그인 (Firebase Apple Provider 사용)
    /// </summary>
    public async Task<FirebaseUser> SignInWithApple()
{
    await EnsureInitializedAsync();

    await _authLock.WaitAsync();
    try
    {
        // 1️⃣ raw nonce 생성 (Firebase용)
        string rawNonce = GenerateRandomNonce();

        // 2️⃣ 애플에 보낼 nonce는 SHA256 해시 값
        string hashedNonce = GenerateSHA256NonceFromRawNonce(rawNonce);

        // 3️⃣ 애플 SDK 호출 시에는 "해시된 nonce"를 넘김
        var appleResult = await SignInWithAppleSDK(hashedNonce);

        string idToken = appleResult.IdentityToken;
        string authorizationCode = appleResult.AuthorizationCode;

        if (string.IsNullOrEmpty(idToken))
        {
            Debug.LogError("[AuthManager] Apple Identity Token null/empty");
            return null;
        }

        // 4️⃣ Firebase Credential 생성 시에는 rawNonce 전달
        Credential credential = OAuthProvider.GetCredential(
            "apple.com",
            idToken,
            rawNonce,          // ✅ raw nonce
            authorizationCode  // (옵션, 그대로 두어도 됨)
        );

        var user = await auth.SignInWithCredentialAsync(credential);
        if (user == null)
        {
            Debug.LogError("[AuthManager] Apple 로그인 결과 user null");
            return null;
        }

        currentUser = user;
        Debug.Log($"[AuthManager] Apple 로그인 성공: {currentUser.UserId}");
        return currentUser;
    }
    catch (Exception e)
    {
        Debug.LogError($"[AuthManager] Apple 로그인 실패: {e.Message}");
        throw;
    }
    finally
    {
        _authLock.Release();
    }
}


private static string GenerateRandomNonce(int length = 32)
{
    const string charset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    var result = new char[length];
    var random = new System.Security.Cryptography.RNGCryptoServiceProvider();
    var buffer = new byte[sizeof(uint)];

    for (int i = 0; i < length; i++)
    {
        random.GetBytes(buffer);
        uint num = BitConverter.ToUInt32(buffer, 0);
        result[i] = charset[(int)(num % (uint)charset.Length)];
    }

    return new string(result);
}

private static string GenerateSHA256NonceFromRawNonce(string rawNonce)
{
    using (var sha = SHA256.Create())
    {
        var bytes = Encoding.UTF8.GetBytes(rawNonce);
        var hash = sha.ComputeHash(bytes);

        var sb = new StringBuilder(hash.Length * 2);
        for (int i = 0; i < hash.Length; i++)
        {
            sb.Append(hash[i].ToString("x2"));
        }
        return sb.ToString();
    }
}

    /// <summary>
    /// Apple SignIn SDK 호출 (비동기 대기)
    /// </summary>
    private Task<appleSignin.SignInResult> SignInWithAppleSDK(string nonce = null)
{
    var tcs = new TaskCompletionSource<appleSignin.SignInResult>();

    appleSignin.Instance.SignIn(
        requestEmail: false,
        requestFullName: false,
        nonce: nonce,               // ✅ nonce 전달
        onSuccess: result => tcs.SetResult(result),
        onError: error => tcs.SetException(new Exception(error))
    );

    return tcs.Task;
}



    #endregion

    #region Kakao Sign In

    /// <summary>
    /// Kakao 로그인 (Custom Token 방식)
    /// </summary>
    public async Task<FirebaseUser> SignInWithKakao()
    {
        await EnsureInitializedAsync();

        await _authLock.WaitAsync();
        try
        {
            // 1) kakaoSignin을 호출해서 "카카오 액세스 토큰"을 비동기로 받는다.
            string kakaoAccessToken = await GetKakaoAccessTokenAsync();
            if (string.IsNullOrEmpty(kakaoAccessToken))
            {
                Debug.LogError("[AuthManager] Kakao access token is null/empty");
                return null;
            }

            // 2) 서버로 교환 → Firebase Custom Token 수령
            string firebaseCustomToken = await ExchangeKakaoForFirebaseTokenAsync(kakaoAccessToken);
            if (string.IsNullOrEmpty(firebaseCustomToken))
            {
                Debug.LogError("[AuthManager] Firebase custom token is null/empty");
                return null;
            }

            // 3) Firebase에 커스텀 토큰으로 로그인
            var user = await auth.SignInWithCustomTokenAsync(firebaseCustomToken);
            if (user == null)
            {
                Debug.LogError("[AuthManager] Firebase user is null after custom sign-in");
                return null;
            }

            currentUser = user.User;
            Debug.Log($"[AuthManager] Kakao→Firebase 로그인 성공: uid={currentUser.UserId}");
            return currentUser;

        }
        catch (Exception e)
        {
            Debug.LogError($"[AuthManager] Kakao 로그인 실패: {e.Message}");
            throw;
        }
        finally
        {
            _authLock.Release();
        }

    }


    // ====================== 내부 헬퍼 ======================

    /// <summary>
    /// kakaoSignin의 콜백을 Task로 감싸 카카오 액세스 토큰을 비동기 획득
    /// </summary>
    private Task<string> GetKakaoAccessTokenAsync()
    {
        var tcs = new TaskCompletionSource<string>();

        // 임시 핸들러 등록
        void OnSuccess(string token)
        {
            Cleanup();
            tcs.TrySetResult(token);
        }
        void OnFail(string msg)
        {
            Cleanup();
            tcs.TrySetException(new Exception($"Kakao login failed: {msg}"));
        }
        void OnCancel()
        {
            Cleanup();
            tcs.TrySetException(new OperationCanceledException("Kakao login canceled by user"));
        }

        void Cleanup()
        {
       
            kakaoSignIn.OnKakaoSucceeded -= OnSuccess;
            kakaoSignIn.OnKakaoFailed -= OnFail;
            kakaoSignIn.OnKakaoCanceled -= OnCancel;
        }


        kakaoSignIn.OnKakaoSucceeded += OnSuccess;
        kakaoSignIn.OnKakaoFailed += OnFail;
        kakaoSignIn.OnKakaoCanceled += OnCancel;

        // 로그인 시작 (버튼 클릭 없이 코드에서 바로 시작)
        kakaoSignIn.BeginKakaoLogin();

        return tcs.Task;
    }

    /// <summary>
    /// 서버 /verifyToken 호출 → { firebase_token } 응답 파싱
    /// </summary>
    private async Task<string> ExchangeKakaoForFirebaseTokenAsync(string kakaoAccessToken)
    {
        var json = "{\"token\":\"" + kakaoAccessToken + "\"}";
        using (var req = new UnityWebRequest(verifyUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            // UnityWebRequest를 await 형태로 사용하기 위한 래퍼
            await SendWebRequestAsync(req);

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception("Verify request failed: " + req.error);

            var text = req.downloadHandler.text;
            var resp = JsonUtility.FromJson<TokenResp>(text);
            if (resp == null || string.IsNullOrEmpty(resp.firebase_token))
                throw new Exception("No firebase_token in response: " + text);

            return resp.firebase_token;
        }
    }

    // UnityWebRequest를 await로 쓰기 위한 간단 래퍼
    private static Task SendWebRequestAsync(UnityWebRequest req)
    {
        var tcs = new TaskCompletionSource<bool>();
#if UNITY_2020_2_OR_NEWER
        var op = req.SendWebRequest();
#else
        var op = req.SendWebRequest(); // Unity 2019 이상 동일 호출
#endif
        op.completed += _ => tcs.TrySetResult(true);
        return tcs.Task;
    }

    [Serializable]
    private class TokenResp { public string firebase_token; }


    

    #endregion

    #region User Management

    /// <summary>
    /// 신규 사용자인지 확인 (Firestore 체크)
    /// </summary>
    /// <summary>
    /// ✅ 수정: hasData 기반으로 신규 사용자 판단
    /// - Firestore에 문서 없음 → 신규
    /// - 문서 있음 + hasData=false → 신규 (쓰레기 데이터)
    /// - 문서 있음 + hasData=true → 기존
    /// </summary>
    public async Task<bool> IsNewUser(FirebaseUser user)
    {
        if (user == null) return false;

        try
        {
            if (string.IsNullOrEmpty(user.UserId))
            {
                Debug.LogError("[AuthManager] IsNewUser: user.UserId null/empty");
                return false;
            }

            if (DataManager.Instance == null)
            {
                Debug.LogError("[AuthManager] IsNewUser: DataManager.Instance is null");
                return false;
            }

            var userData = await DataManager.Instance.GetUserData(user.UserId);

            // 문서 없음 → 신규
            if (userData == null)
                return true;

            // 문서 있음 + hasData=false → 신규 (쓰레기 데이터 삭제 후 재생성 필요)
            if (!userData.hasData)
                return true;

            // 문서 있음 + hasData=true → 기존
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AuthManager] 신규 사용자 확인 실패: {e.Message}");
            return false;
        }
    }
    /// <summary>
    /// ✅ 신규: 탈퇴 회원 여부 확인
    /// </summary>
    public async Task<bool> IsWithdrawnUser(FirebaseUser user)
    {
        if (user == null) return false;

        try
        {
            var userData = await DataManager.Instance.GetUserData(user.UserId);
            return userData != null && userData.isWithdrawn;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AuthManager] 탈퇴 회원 확인 실패: {e.Message}");
            return false;
        }
    }


    /// <summary>
    /// 로그아웃
    /// </summary>
    public void SignOut()
    {
        if (auth != null)
        {
            auth.SignOut();
            currentUser = null;

            // 각 SDK도 로그아웃
            GoogleSignIn.DefaultInstance?.SignOut();
            // Apple/Kakao는 별도 로그아웃 불필요 (Firebase만 로그아웃하면 됨)

            Debug.Log("[AuthManager] 로그아웃 완료");
        }
    }

    /// <summary>
    /// 계정 삭제
    /// </summary>
    /// <summary>
    /// ✅ 수정: WithdrawUser 호출로 변경
    /// </summary>
    public async Task DeleteAccount()
    {
        if (currentUser == null)
        {
            throw new Exception("No user to delete");
        }

        try
        {
            // ✅ 수정: DeleteUserData → WithdrawUser
            await DataManager.Instance.WithdrawUser(currentUser.UserId);

            // Firebase Auth 계정 삭제
            await currentUser.DeleteAsync();

            currentUser = null;
            Debug.Log("[AuthManager] 계정 삭제 완료");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AuthManager] 계정 삭제 실패: {e.Message}");
            throw;
        }
    }


    #endregion

    #region Helper Methods

    /// <summary>
    /// 현재 로그인 제공자 가져오기
    /// </summary>
    public string GetCurrentProvider()
    {
        if (currentUser == null) return null;

        foreach (var providerData in currentUser.ProviderData)
        {
            if (providerData.ProviderId == "google.com") return "google";
            if (providerData.ProviderId == "apple.com") return "apple";
            if (providerData.ProviderId == "firebase") return "kakao"; // Custom Token은 firebase로 표시됨
        }

        return "unknown";
    }

    #endregion
}