package com.company.IwantToKnowMyFuture
import android.app.Application
import com.kakao.sdk.common.KakaoSdk

class GlobalApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        KakaoSdk.init(this, "KAKAO_NATIVE_KEY")
    }
}