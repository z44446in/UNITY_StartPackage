


    using System;
    using System.Text;
    using AppleAuth.Enums;
    using UnityEngine;

#if UNITY_IOS 
    using AppleAuth;
    using AppleAuth.Interfaces;
    using AppleAuth.Native;
    using AppleAuth.Extensions;
#endif

    public class appleSignin : MonoBehaviour
    {
        // 저장 키 (유저 ID는 꼭 보관하세요. 이메일/이름은 '최초 1회'만 내려옵니다)
        public const string AppleUserIdKey = "APPLE_USER_ID";

        // 싱글톤 (원하면 제거하고 직접 참조해도 됩니다)
        public static appleSignin Instance { get; private set; }

#if UNITY_IOS
        private IAppleAuthManager _authManager;
#endif

        /// <summary>현재 플랫폼이 네이티브 Sign in with Apple을 지원하는지</summary>
        public bool IsSupported
        {
            get
            {
#if UNITY_IOS
                return AppleAuthManager.IsCurrentPlatformSupported;
#else
            return false;
#endif
            }
        }

        /// <summary>PlayerPrefs에 저장된 Apple User ID (없으면 빈 문자열)</summary>
        public string SavedUserId => PlayerPrefs.GetString(AppleUserIdKey, string.Empty);

        #region Unity lifecycle
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_IOS || UNITY_TVOS || UNITY_STANDALONE_OSX || UNITY_VISIONOS
            if (IsSupported)
            {
                var deserializer = new PayloadDeserializer();
                _authManager = new AppleAuthManager(deserializer);
            }
#endif
        }

        private void Update()
        {
#if UNITY_IOS
            _authManager?.Update();
#endif
        }
        #endregion

        #region Public API
        /// <summary>
        /// 표준 로그인 플로우. 이메일/이름은 최초 1회만 제공됨.
        /// </summary>
        /// <param name="requestEmail">이메일 요청</param>
        /// <param name="requestFullName">이름 요청</param>
        /// <param name="nonce">옵션 Nonce (매 요청마다 새 랜덤 권장)</param>
        /// <param name="state">옵션 State</param>
        /// <param name="onSuccess">성공 콜백</param>
        /// <param name="onError">에러 콜백(문자열 메시지)</param>
        public void SignIn(
            bool requestEmail,
            bool requestFullName,
            string nonce = null,
            string state = null,
            Action<SignInResult> onSuccess = null,
            Action<string> onError = null)
        {
            if (!CheckReady(onError)) return;

#if UNITY_IOS || UNITY_TVOS || UNITY_STANDALONE_OSX || UNITY_VISIONOS
            var options = LoginOptions.None;
            if (requestEmail) options |= LoginOptions.IncludeEmail;
            if (requestFullName) options |= LoginOptions.IncludeFullName;

            var args = new AppleAuthLoginArgs(options, nonce, state);

            _authManager.LoginWithAppleId(
                args,
                credential =>
                {
                    var result = CreateResultFromCredential(credential);
                    if (!string.IsNullOrEmpty(result.UserId))
                    {
                        PlayerPrefs.SetString(AppleUserIdKey, result.UserId);
                        PlayerPrefs.Save();
                    }
                    onSuccess?.Invoke(result);
                },
                error =>
                {
                    onError?.Invoke(BuildErrorMessage(error));
                }
            );
#endif
        }

        // ← 이 메서드를 한 줄로 호출할 거예요.
        public void OnSignInApple()
        {
            appleSignin.Instance.SignIn(
                requestEmail: true,
                requestFullName: true,
                onSuccess: r =>
                {
                    Debug.Log($"[Apple] OK userId={r.UserId}, email={r.Email}, name={r.FullName}");
                    // TODO: 백엔드 검증/회원 처리
                },
                onError: err =>
                {
                    Debug.LogError($"[Apple] SignIn Error: {err}");
                }
            );
        }


        /// <summary>
        /// 빠른 로그인(Quick Login). 이전에 권한이 있으면 확인 다이얼로그만으로 UserId를 다시 얻습니다.
        /// (이메일/이름은 내려오지 않습니다)
        /// </summary>
        public void QuickLogin(
            string nonce = null,
            string state = null,
            Action<SignInResult> onSuccess = null,
            Action<string> onError = null)
        {
            if (!CheckReady(onError)) return;

#if UNITY_IOS || UNITY_TVOS || UNITY_STANDALONE_OSX || UNITY_VISIONOS
            var args = new AppleAuthQuickLoginArgs(nonce, state);

            _authManager.QuickLogin(
                args,
                credential =>
                {
                    var result = CreateResultFromCredential(credential);
                    if (!string.IsNullOrEmpty(result.UserId))
                    {
                        PlayerPrefs.SetString(AppleUserIdKey, result.UserId);
                        PlayerPrefs.Save();
                    }
                    onSuccess?.Invoke(result);
                },
                error =>
                {
                    onError?.Invoke(BuildErrorMessage(error));
                }
            );
#endif
        }

        /// <summary>
        /// 저장된 사용자 ID(또는 전달받은 ID)의 자격 상태 조회
        /// </summary>
        public void CheckCredentialState(
            string userId,
            Action<CredentialState> onComplete,
            Action<string> onError = null)
        {
            if (!CheckReady(onError)) return;

            if (string.IsNullOrEmpty(userId))
            {
                onError?.Invoke("Missing userId. Save it from the first successful login.");
                return;
            }

#if UNITY_IOS
            _authManager.GetCredentialState(
                userId,
                state => onComplete?.Invoke(state),
                error => onError?.Invoke(BuildErrorMessage(error))
            );
#endif
        }

        /// <summary>
        /// 자격 취소(Revoked) 알림 콜백 등록/해제 (null을 주면 해제)
        /// </summary>
        public void SetCredentialsRevokedCallback(Action onRevoked)
        {
#if UNITY_IOS
            if (!IsSupported || _authManager == null)
            {
                // 미지원 플랫폼에서는 아무 것도 하지 않음
                return;
            }

            if (onRevoked == null)
            {
                _authManager.SetCredentialsRevokedCallback(null);
                return;
            }

            _authManager.SetCredentialsRevokedCallback(_ =>
            {
                // 앱 정책에 맞게 저장된 자격 정리
                PlayerPrefs.DeleteKey(AppleUserIdKey);
                PlayerPrefs.Save();
                onRevoked?.Invoke();
            });
#endif
        }
        #endregion

        #region Helpers
        private bool CheckReady(Action<string> onError)
        {
            if (!IsSupported)
            {
                onError?.Invoke("Sign in with Apple is not supported on this platform.");
                return false;
            }
#if UNITY_IOS || UNITY_TVOS || UNITY_STANDALONE_OSX || UNITY_VISIONOS
            if (_authManager == null)
            {
                onError?.Invoke("AppleAuthManager not initialized.");
                return false;
            }
#endif
            return true;
        }

#if UNITY_IOS
        private static SignInResult CreateResultFromCredential(ICredential credential)
        {
            // IAppleIDCredential 또는 IPasswordCredential (Keychain) 두 경우 처리
            var appleId = credential as IAppleIDCredential;
            if (appleId != null)
            {
                return new SignInResult
                {
                    Type = SignInResult.CredentialType.AppleID,
                    UserId = appleId.User,
                    Email = appleId.Email, // 최초 1회만 제공
                    FullName = appleId.FullName != null ? appleId.FullName.Nickname ?? appleId.FullName.GivenName + " " + appleId.FullName.FamilyName : null,
                    IdentityToken = SafeDecode(appleId.IdentityToken),
                    AuthorizationCode = SafeDecode(appleId.AuthorizationCode),
                    State = appleId.State
                };
            }

            var pw = credential as IPasswordCredential;
            if (pw != null)
            {
                return new SignInResult
                {
                    Type = SignInResult.CredentialType.Password,
                    UserId = pw.User,
                    Password = pw.Password
                };
            }

            // 알 수 없는 타입
            return new SignInResult { Type = SignInResult.CredentialType.Unknown };
        }

        private static string SafeDecode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }

        private static string BuildErrorMessage(IAppleError error)
        {
            var code = (error != null) ? error.GetAuthorizationErrorCode().ToString() : "Unknown";
            var localized = (error != null) ? error.LocalizedDescription : "No description";
            return $"Apple SignIn Error [{code}]: {localized}";
        }
#endif
        #endregion

        #region Data
        [Serializable]
        public struct SignInResult
        {
            public enum CredentialType { Unknown, AppleID, Password }

            public CredentialType Type;

            // 공통/주요 필드
            public string UserId;          // Apple User ID (가장 중요! 보관 필수)
            public string Email;           // 최초 1회만 제공
            public string FullName;        // 최초 1회만 제공

            // AppleIDCredential 전용
            public string IdentityToken;   // JWT (백엔드 검증 등에 사용)
            public string AuthorizationCode; // 서버에서 refresh token 교환용 (최초 로그인 시)
            public string State;           // 요청 시 제공했던 state

            // PasswordCredential 전용 (Keychain)
            public string Password;
        }
        #endregion
    }
