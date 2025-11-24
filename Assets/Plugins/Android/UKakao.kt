package com.company.UNITY_PROJECT_NAME

import android.util.Log
import com.kakao.sdk.auth.model.OAuthToken
import com.kakao.sdk.common.model.ClientError
import com.kakao.sdk.common.model.ClientErrorCause
import com.kakao.sdk.user.UserApiClient
import com.unity3d.player.UnityPlayer

class UKakao {

    private val context get() = UnityPlayer.currentActivity

    /** 정식: 콜백 받을 Unity GameObject 이름을 전달받아 사용 */
    fun KakaoLogin(gameObjectName: String) {
        val accountCallback: (OAuthToken?, Throwable?) -> Unit = { token, error ->
            if (error != null) {
                Log.e("UnityLog", "카카오계정으로 로그인 실패", error)
                unitySend(gameObjectName, "OnKakaoLoginFail", error.message ?: "")
            } else if (token != null) {
                Log.i("UnityLog", "카카오계정으로 로그인 성공 ${token.accessToken}")
                unitySend(gameObjectName, "OnKakaoLoginSuccess", token.accessToken)
            }
        }

        if (UserApiClient.instance.isKakaoTalkLoginAvailable(context)) {
            UserApiClient.instance.loginWithKakaoTalk(context) { token, error ->
                if (error != null) {
                    Log.e("UnityLog", "카카오톡으로 로그인 실패", error)
                    if (error is ClientError && error.reason == ClientErrorCause.Cancelled) {
                        unitySend(gameObjectName, "OnKakaoLoginCancel", "")
                        return@loginWithKakaoTalk
                    }
                    // 폴백: 카카오계정 로그인
                    UserApiClient.instance.loginWithKakaoAccount(context, callback = accountCallback)
                } else if (token != null) {
                    Log.i("UnityLog", "카카오톡으로 로그인 성공 ${token.accessToken}")
                    unitySend(gameObjectName, "OnKakaoLoginSuccess", token.accessToken)
                }
            }
        } else {
            UserApiClient.instance.loginWithKakaoAccount(context, callback = accountCallback)
        }
    }

    private fun unitySend(goName: String, method: String, payload: String) {
        UnityPlayer.UnitySendMessage(goName, method, payload)
    }

}
