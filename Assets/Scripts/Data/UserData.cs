using System;
using System.Collections.Generic;

/// <summary>
/// 사용자 데이터 구조
/// Firebase Firestore와 1:1 매핑되는 클래스
/// ✅ 수정:
/// - hasData 기본값 false로 변경 (BirthInfo 입력 후 true)
/// - hasReceivedReferralReward 필드 추가 (탈퇴 후 재가입 시 보상 중복 방지)
/// - isWithdrawn 필드 추가 (탈퇴 여부)
/// - withdrawnAt 필드 추가 (탈퇴 일시)
/// </summary>
[Serializable]
public class UserData
{
    public string userId;
    public string email;

    // ✅ 수정: BirthInfo 입력 완료 여부 (기본값 false)
    public bool hasData;
    
    // ✅ 추가: 추천인 보상 수령 여부 (탈퇴해도 유지)
    public bool hasReceivedReferralReward;
    
    // ✅ 추가: 탈퇴 여부
    public bool isWithdrawn;
    
    // ✅ 추가: 탈퇴 일시 (null = 탈퇴하지 않음)
    public DateTime? withdrawnAt;
    
    public string loginProvider; // "google", "apple", "kakao"
    public string referralCode; // 6자리 숫자
    public string referredBy; // 추천인 코드 (null 가능)
    public int questionTokens; // 질문권 개수
    public bool hasAdRemoval; // 광고제거 구매 여부
    public DateTime? adRemovalPurchasedAt; // 광고제거 구매 시각
    public DateTime createdAt;
    public BirthInfo birthInfo;
    public SajuData sajuData;

    /// <summary>
    /// 새 사용자 데이터 생성 (기본값 설정)
    /// ✅ 수정: hasData = false (생년월일 입력 전)
    /// ✅ 수정: hasReceivedReferralReward = false
    /// ✅ 수정: isWithdrawn = false
    /// </summary>
    public UserData(string userId, string email, string provider, string referralCode)
    {
        this.userId = userId;
        this.email = email;
        this.hasData = false; // ✅ 수정: 기본값 false
        this.hasReceivedReferralReward = false; // ✅ 추가
        this.isWithdrawn = false; // ✅ 추가
        this.withdrawnAt = null; // ✅ 추가
        this.loginProvider = provider;
        this.referralCode = referralCode;
        this.referredBy = null;
        this.questionTokens = 3; // 기본 질문권 3개
        this.hasAdRemoval = false;
        this.adRemovalPurchasedAt = null;
        this.createdAt = DateTime.UtcNow;
        this.birthInfo = null;
        this.sajuData = null;
    }

    /// <summary>
    /// 질문권 추가
    /// </summary>
    public void AddQuestionTokens(int amount)
    {
        questionTokens += amount;
        if (questionTokens < 0) questionTokens = 0;
    }

    /// <summary>
    /// 질문권 사용
    /// </summary>
    /// <returns>사용 성공 여부</returns>
    public bool UseQuestionToken()
    {
        if (questionTokens <= 0) return false;
        questionTokens--;
        return true;
    }

    /// <summary>
    /// 광고제거 구매 처리
    /// </summary>
    public void PurchaseAdRemoval()
    {
        hasAdRemoval = true;
        adRemovalPurchasedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// 사용자 생년월일시 정보
/// </summary>
[Serializable]
public class BirthInfo
{
    public int year;
    public int month;
    public int day;
    public int hour; // 시간 모름 시 -1
    public int minute; // 시간 모름 시 -1
    public bool isLunar; // true: 음력, false: 양력
    public string gender; // "male" or "female"
    public string name;

    public BirthInfo(int year, int month, int day, int hour, int minute, bool isLunar, string gender, string name)
    {
        this.year = year;
        this.month = month;
        this.day = day;
        this.hour = hour;
        this.minute = minute;
        this.isLunar = isLunar;
        this.gender = gender;
        this.name = name;
    }

    /// <summary>
    /// 시간 정보가 있는지 확인
    /// </summary>
    public bool HasTimeInfo()
    {
        return hour >= 0 && minute >= 0;
    }
}

/// <summary>
/// 사주팔자 데이터
/// Firebase Functions에서 계산되어 저장됨
/// </summary>
[Serializable]
public class SajuData
{
    public Pillar yearPillar; // 연주
    public Pillar monthPillar; // 월주
    public Pillar dayPillar; // 일주
    public Pillar hourPillar; // 시주
    public List<DaeunData> daeun; // 대운
    public DateTime calculatedAt;

    public SajuData()
    {
        daeun = new List<DaeunData>();
        calculatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// 사주 기둥 (천간 + 지지)
/// </summary>
[Serializable]
public class Pillar
{
    public string heavenly; // 천간 (甲, 乙, 丙, ...)
    public string earthly; // 지지 (子, 丑, 寅, ...)

    public Pillar(string heavenly, string earthly)
    {
        this.heavenly = heavenly;
        this.earthly = earthly;
    }

    public override string ToString()
    {
        return $"{heavenly}{earthly}";
    }
}

/// <summary>
/// 대운 데이터
/// </summary>
[Serializable]
public class DaeunData
{
    public Pillar pillar;
    public int startAge; // 시작 나이
    public int endAge; // 종료 나이

    public DaeunData(Pillar pillar, int startAge, int endAge)
    {
        this.pillar = pillar;
        this.startAge = startAge;
        this.endAge = endAge;
    }
}