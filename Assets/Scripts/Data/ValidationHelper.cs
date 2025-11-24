using System;
using System.Text.RegularExpressions;

/// <summary>
/// 입력 유효성 검증 헬퍼
/// 모든 입력값 검증 로직을 여기서 처리
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    /// 추천인 코드 형식 검증 (6자리 숫자)
    /// </summary>
    public static bool IsValidReferralCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return false;
        return Regex.IsMatch(code, @"^\d{6}$");
    }

    /// <summary>
    /// 이름 유효성 검증 (1~20자, 빈 문자열 불가)
    /// </summary>
    public static bool IsValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return name.Length >= 1 && name.Length <= 20;
    }

    /// <summary>
    /// 생년월일 유효성 검증
    /// </summary>
    public static bool IsValidBirthDate(int year, int month, int day)
    {
        // 연도: 1900 ~ 현재년도
        if (year < 1900 || year > DateTime.Now.Year) return false;

        // 월: 1~12
        if (month < 1 || month > 12) return false;

        // 일: 1~31 (월별 일수는 DateTime으로 검증)
        try
        {
            var date = new DateTime(year, month, day);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 시간 유효성 검증
    /// </summary>
    public static bool IsValidTime(int hour, int minute)
    {
        // 시간 모름의 경우 (-1, -1)은 유효함
        if (hour == -1 && minute == -1) return true;

        // 정상 시간: 0~23시, 0~59분
        return hour >= 0 && hour <= 23 && minute >= 0 && minute <= 59;
    }

    /// <summary>
    /// 성별 유효성 검증
    /// </summary>
    public static bool IsValidGender(string gender)
    {
        return gender == "male" || gender == "female";
    }

    /// <summary>
    /// 이메일 형식 검증 (간단한 패턴)
    /// </summary>
    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    }

    /// <summary>
    /// 생년월일시 입력 완료 여부 확인
    /// </summary>
    /// <param name="year">년</param>
    /// <param name="month">월</param>
    /// <param name="day">일</param>
    /// <param name="hour">시</param>
    /// <param name="minute">분</param>
    /// <param name="timeUnknown">시간 모름 체크 여부</param>
    /// <param name="isLunarSelected">음력 선택 여부</param>
    /// <param name="isSolarSelected">양력 선택 여부</param>
    /// <param name="gender">성별</param>
    /// <param name="name">이름</param>
    /// <returns>모든 필수 입력이 완료되었는지 여부</returns>
    public static bool IsBirthInfoComplete(
        int year, int month, int day,
        int hour, int minute,
        bool timeUnknown,
        bool isLunarSelected, bool isSolarSelected,
        string gender, string name)
    {
        // 생년월일 검증
        if (!IsValidBirthDate(year, month, day)) return false;

        // 시간 검증 (시간 모름이면 hour=-1, minute=-1이어야 함)
        if (timeUnknown)
        {
            if (hour != -1 || minute != -1) return false;
        }
        else
        {
            if (!IsValidTime(hour, minute)) return false;
        }

        // 음력/양력 중 하나는 반드시 선택
        if (!isLunarSelected && !isSolarSelected) return false;

        // 둘 다 선택은 불가
        if (isLunarSelected && isSolarSelected) return false;

        // 성별 검증
        if (!IsValidGender(gender)) return false;

        // 이름 검증
        if (!IsValidName(name)) return false;

        return true;
    }
}