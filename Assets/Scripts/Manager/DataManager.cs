using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Firestore;
using Firebase.Functions;
using UnityEngine;

/// <summary>
/// Firebase Firestore 데이터 관리자
/// 사용자 데이터 CRUD 및 추천인 코드 검증 처리
/// </summary>
public class DataManager : MonoBehaviour
{
    private static DataManager instance;
    public static DataManager Instance
    {
        get
        {
            if (instance == null)
            {
                var obj = new GameObject("DataManager");
                instance = obj.AddComponent<DataManager>();
                DontDestroyOnLoad(obj);
            }
            return instance;
        }
    }

    private FirebaseFirestore db;
    private FirebaseFunctions functions;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

    }

// ✅ 추가: 사용 전 초기화 확인
private async Task EnsureFirebaseInitialized()
{
    if (db != null) return;

    // Firebase 초기화 대기
    var status = await Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
    if (status != Firebase.DependencyStatus.Available)
    {
        throw new System.Exception($"Firebase 초기화 실패: {status}");
    }

    db = FirebaseFirestore.DefaultInstance;
    functions = FirebaseFunctions.DefaultInstance;

    Debug.Log("[DataManager] Firebase 초기화 완료");
}


    #region User Data CRUD

    /// <summary>
    /// 새 사용자 생성 및 저장
    /// </summary>
    public async Task<UserData> CreateNewUser(string userId, string email, string loginProvider)
    {
        await EnsureFirebaseInitialized(); // ✅ 추가
    
        try
        {
            // 1. 중복되지 않는 추천인 코드 생성
            string referralCode = await GenerateUniqueReferralCode();

            // 2. UserData 생성
            UserData newUser = new UserData(userId, email, loginProvider, referralCode);

            // 3. Firestore에 저장
            var userDoc = db.Collection("users").Document(userId);
            await userDoc.SetAsync(ConvertToDictionary(newUser));

            Debug.Log($"새 사용자 생성 완료: {userId}, 추천인 코드: {referralCode}");
            return newUser;
        }
        catch (Exception e)
        {
            Debug.LogError($"사용자 생성 실패: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// 사용자 데이터 불러오기
    /// </summary>
    public async Task<UserData> GetUserData(string userId)
    {
        await EnsureFirebaseInitialized(); // ✅ 추가
    
        try
        {
            var userDoc = await db.Collection("users").Document(userId).GetSnapshotAsync();

            if (!userDoc.Exists)
            {
                Debug.LogWarning($"사용자 데이터 없음: {userId}");
                return null;
            }

            return ConvertToUserData(userDoc);
        }
        catch (Exception e)
        {
            Debug.LogError($"사용자 데이터 불러오기 실패: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// 사용자 데이터 업데이트
    /// </summary>
    public async Task UpdateUserData(UserData userData)
    {
        await EnsureFirebaseInitialized(); // ✅ 추가
    
        try
        {
            var userDoc = db.Collection("users").Document(userData.userId);
            await userDoc.SetAsync(ConvertToDictionary(userData), SetOptions.MergeAll);

            Debug.Log($"사용자 데이터 업데이트 완료: {userData.userId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"사용자 데이터 업데이트 실패: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// 생년월일시 정보 저장
    /// </summary>
    public async Task SaveBirthInfo(string userId, BirthInfo birthInfo)
    {
        await EnsureFirebaseInitialized(); // ✅ 추가
    
        try
        {
            var userDoc = db.Collection("users").Document(userId);
            await userDoc.UpdateAsync("birthInfo", ConvertBirthInfoToDictionary(birthInfo));

            Debug.Log($"생년월일시 정보 저장 완료: {userId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"생년월일시 정보 저장 실패: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// ✅ 신규: 사용자 데이터 삭제 (회원 탈퇴용)
    /// </summary>
    /// <summary>
    /// ✅ 수정: 회원 탈퇴 처리
    /// 문서 삭제 대신 개인정보만 삭제하고 최소 데이터 유지
    /// </summary>
    public async Task WithdrawUser(string userId)
    {
        await EnsureFirebaseInitialized(); // ✅ 추가
    
        try
        {
            var userDoc = db.Collection("users").Document(userId);

            // 개인정보 삭제 & 탈퇴 플래그 설정
            await userDoc.UpdateAsync(new Dictionary<string, object>
        {
            { "isWithdrawn", true },
            { "withdrawnAt", Timestamp.FromDateTime(DateTime.UtcNow) },
            { "hasData", false }, // 재가입 시 BirthInfo 다시 입력
            { "email", FieldValue.Delete },
            { "birthInfo", FieldValue.Delete },
            { "sajuData", FieldValue.Delete },
            { "questionTokens", 0 }, // 질문권 초기화
            { "hasAdRemoval", false },
            { "adRemovalPurchasedAt", FieldValue.Delete },
            { "referredBy", FieldValue.Delete }
            // hasReceivedReferralReward, referralCode는 유지
        });

            Debug.Log($"회원 탈퇴 처리 완료: {userId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"회원 탈퇴 처리 실패: {e.Message}");
            throw;
        }
    }


    #endregion

    #region Referral Code

    /// <summary>
    /// ✅ 수정: 중복되지 않는 추천인 코드 생성 (Firestore에서 생성)
    /// </summary>
    private async Task<string> GenerateUniqueReferralCode()
    {
        await EnsureFirebaseInitialized(); // ✅ 추가
    
        int maxAttempts = 10; // 최대 시도 횟수
        int attempts = 0;

        while (attempts < maxAttempts)
        {
            // 6자리 랜덤 숫자 생성 (100000 ~ 999999)
            string code = UnityEngine.Random.Range(100000, 1000000).ToString();

            // Firestore에서 중복 확인
            var query = await db.Collection("users")
                .WhereEqualTo("referralCode", code)
                .GetSnapshotAsync();

            if (query.Count == 0)
            {
                // 중복 없음 - 사용 가능
                return code;
            }

            attempts++;
        }

        throw new Exception("추천인 코드 생성 실패: 최대 시도 횟수 초과");
    }

    /// <summary>
    /// 추천인 코드 존재 여부 확인 (본인 코드 제외)
    /// </summary>
    /// <param name="referralCode">확인할 추천인 코드</param>
    /// <param name="currentUserId">현재 사용자 ID (본인 코드 제외용)</param>
    /// <returns>존재하면 true, 없으면 false</returns>
    /// <summary>
    /// 추천인 코드 존재 여부 확인 (본인 코드 제외 & 탈퇴 회원 제외)
    /// ✅ 수정: isWithdrawn=true인 경우 존재하지 않는 것으로 처리
    /// </summary>
    public async Task<bool> IsReferralCodeExists(string referralCode, string currentUserId)
    {
        await EnsureFirebaseInitialized(); // ✅ 추가
    
        try
        {
            var query = await db.Collection("users")
                .WhereEqualTo("referralCode", referralCode)
                .GetSnapshotAsync();

            foreach (var doc in query.Documents)
            {
                // 본인의 추천인 코드는 제외
                if (doc.Id == currentUserId)
                    continue;

                // ✅ 추가: 탈퇴한 회원의 코드는 존재하지 않는 것으로 처리
                var dict = doc.ToDictionary();
                if (dict.ContainsKey("isWithdrawn") && Convert.ToBoolean(dict["isWithdrawn"]))
                    continue;

                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"추천인 코드 확인 실패: {e.Message}");
            throw;
        }
    }


    /// <summary>
    /// 추천인 코드로 사용자 ID 가져오기
    /// </summary>
    public async Task<string> GetUserIdByReferralCode(string referralCode)
    {
        await EnsureFirebaseInitialized(); // ✅ 추가
    
        try
        {
            var snapshot = await db.Collection("users")
                .WhereEqualTo("referralCode", referralCode)
                .Limit(1)
                .GetSnapshotAsync();

            var doc = snapshot.Documents.FirstOrDefault();

            return doc?.Id;
        }
        catch (Exception e)
        {
            Debug.LogError($"추천인 사용자 ID 가져오기 실패: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// ✅ 신규: 사용자 가입 완료 처리 (BirthInfo 입력 후 호출)
    /// - hasData = true로 변경
    /// - 추천인 보상 지급 (referredBy가 있는 경우)
    /// </summary>
    public async Task CompleteUserRegistration(string userId)
    {
        await EnsureFirebaseInitialized(); // ✅ 추가
    
        try
        {
            var userDoc = db.Collection("users").Document(userId);
            var snapshot = await userDoc.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                Debug.LogError($"사용자 문서 없음: {userId}");
                return;
            }

            var userData = ConvertToUserData(snapshot);

            // 1. hasData = true로 변경
            await userDoc.UpdateAsync("hasData", true);

            // 2. 추천인 보상 지급 (referredBy가 있고, 아직 보상 받지 않은 경우)
            if (!string.IsNullOrEmpty(userData.referredBy) && !userData.hasReceivedReferralReward)
            {
                // 추천인 찾기
                string referrerId = await GetUserIdByReferralCode(userData.referredBy);

                if (!string.IsNullOrEmpty(referrerId))
                {
                    // 추천인에게 질문권 10개 지급
                    var referrerDoc = db.Collection("users").Document(referrerId);
                    await referrerDoc.UpdateAsync("questionTokens", FieldValue.Increment(10));

                    // 신규 가입자에게 질문권 5개 지급 & 보상 수령 플래그
                    await userDoc.UpdateAsync(new Dictionary<string, object>
                {
                    { "questionTokens", FieldValue.Increment(5) },
                    { "hasReceivedReferralReward", true }
                });

                    Debug.Log($"추천인 보상 지급 완료 - 추천인: {referrerId} (+10), 신규: {userId} (+5)");
                }
            }

            Debug.Log($"사용자 가입 완료: {userId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"사용자 가입 완료 처리 실패: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// ✅ 신규: referredBy 필드만 저장 (ReferralCodePanel에서 사용)
    /// 보상은 지급하지 않고, BirthInfo 완료 시 지급
    /// </summary>
    public async Task SaveReferredBy(string userId, string referralCode)
    {
        await EnsureFirebaseInitialized(); // ✅ 추가
    
        try
        {
            var userDoc = db.Collection("users").Document(userId);
            await userDoc.UpdateAsync("referredBy", referralCode);

            Debug.Log($"추천인 코드 저장 완료: {userId} → {referralCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"추천인 코드 저장 실패: {e.Message}");
            throw;
        }
    }



    #endregion

    #region Saju Calculation

    /// <summary>
    /// Firebase Functions를 호출하여 사주 계산
    /// </summary>
    public async Task<SajuData> CalculateSaju(string userId, BirthInfo birthInfo)
    {
        await EnsureFirebaseInitialized(); // ✅ 추가
    
        try
        {
            // Firebase Functions 호출
            var data = new Dictionary<string, object>
            {
                { "userId", userId },
                { "year", birthInfo.year },
                { "month", birthInfo.month },
                { "day", birthInfo.day },
                { "hour", birthInfo.hour },
                { "minute", birthInfo.minute },
                { "isLunar", birthInfo.isLunar },
                { "gender", birthInfo.gender }
            };

            var callable = functions.GetHttpsCallable("calculateSaju");
            var result = await callable.CallAsync(data);

            // 결과를 SajuData로 변환
            var sajuData = ConvertToSajuData(result.Data as Dictionary<string, object>);

            // Firestore에 저장 (Functions에서도 저장하지만, 클라이언트에서도 확인용)
            Debug.Log($"사주 계산 완료: {userId}");
            return sajuData;
        }
        catch (Exception e)
        {
            Debug.LogError($"사주 계산 실패: {e.Message}");
            throw;
        }
    }

    #endregion

    #region Conversion Helpers

    /// <summary>
    /// UserData → Dictionary (Firestore 저장용)
    /// </summary>
    private Dictionary<string, object> ConvertToDictionary(UserData userData)
    {
        
        var dict = new Dictionary<string, object>
    {
        { "email", userData.email ?? "" },
        { "hasData", userData.hasData }, // ✅ 추가
        { "hasReceivedReferralReward", userData.hasReceivedReferralReward }, // ✅ 추가
        { "isWithdrawn", userData.isWithdrawn }, // ✅ 추가
        { "loginProvider", userData.loginProvider },
        { "referralCode", userData.referralCode },
        { "referredBy", userData.referredBy ?? "" },
        { "questionTokens", userData.questionTokens },
        { "hasAdRemoval", userData.hasAdRemoval },
        { "createdAt", Timestamp.FromDateTime(userData.createdAt.ToUniversalTime()) }
    };

        // ✅ 추가: 탈퇴 일시
        if (userData.withdrawnAt.HasValue)
        {
            dict["withdrawnAt"] = Timestamp.FromDateTime(userData.withdrawnAt.Value.ToUniversalTime());
        }

        if (userData.adRemovalPurchasedAt.HasValue)
        {
            dict["adRemovalPurchasedAt"] = Timestamp.FromDateTime(userData.adRemovalPurchasedAt.Value.ToUniversalTime());
        }

        if (userData.birthInfo != null)
        {
            dict["birthInfo"] = ConvertBirthInfoToDictionary(userData.birthInfo);
        }

        if (userData.sajuData != null)
        {
            dict["sajuData"] = ConvertSajuDataToDictionary(userData.sajuData);
        }

        return dict;
    }


    /// <summary>
    /// Dictionary → UserData (Firestore 불러오기용)
    /// </summary>
    private UserData ConvertToUserData(DocumentSnapshot snapshot)
    {
        
        var dict = snapshot.ToDictionary();
        var userData = new UserData(
            snapshot.Id,
            dict.ContainsKey("email") ? dict["email"]?.ToString() : "",
            dict["loginProvider"].ToString(),
            dict["referralCode"].ToString()
        );

        // ✅ 추가: 새 필드들 로드
        userData.hasData = dict.ContainsKey("hasData") && Convert.ToBoolean(dict["hasData"]);
        userData.hasReceivedReferralReward = dict.ContainsKey("hasReceivedReferralReward") && Convert.ToBoolean(dict["hasReceivedReferralReward"]);
        userData.isWithdrawn = dict.ContainsKey("isWithdrawn") && Convert.ToBoolean(dict["isWithdrawn"]);

        userData.referredBy = dict.ContainsKey("referredBy") ? dict["referredBy"]?.ToString() : null;
        userData.questionTokens = Convert.ToInt32(dict["questionTokens"]);
        userData.hasAdRemoval = dict.ContainsKey("hasAdRemoval") && Convert.ToBoolean(dict["hasAdRemoval"]);
        userData.createdAt = ((Timestamp)dict["createdAt"]).ToDateTime();

        // ✅ 추가: 탈퇴 일시
        if (dict.ContainsKey("withdrawnAt") && dict["withdrawnAt"] != null)
        {
            userData.withdrawnAt = ((Timestamp)dict["withdrawnAt"]).ToDateTime();
        }

        if (dict.ContainsKey("adRemovalPurchasedAt") && dict["adRemovalPurchasedAt"] != null)
        {
            userData.adRemovalPurchasedAt = ((Timestamp)dict["adRemovalPurchasedAt"]).ToDateTime();
        }

        if (dict.ContainsKey("birthInfo") && dict["birthInfo"] != null)
        {
            var birthInfoDict = dict["birthInfo"] as Dictionary<string, object>;
            userData.birthInfo = ConvertToBirthInfo(birthInfoDict);
        }

        if (dict.ContainsKey("sajuData") && dict["sajuData"] != null)
        {
            var sajuDataDict = dict["sajuData"] as Dictionary<string, object>;
            userData.sajuData = ConvertToSajuData(sajuDataDict);
        }

        return userData;
    }

    private Dictionary<string, object> ConvertBirthInfoToDictionary(BirthInfo birthInfo)
    {
        return new Dictionary<string, object>
        {
            { "year", birthInfo.year },
            { "month", birthInfo.month },
            { "day", birthInfo.day },
            { "hour", birthInfo.hour },
            { "minute", birthInfo.minute },
            { "isLunar", birthInfo.isLunar },
            { "gender", birthInfo.gender },
            { "name", birthInfo.name }
        };
    }

    private BirthInfo ConvertToBirthInfo(Dictionary<string, object> dict)
    {
        return new BirthInfo(
            Convert.ToInt32(dict["year"]),
            Convert.ToInt32(dict["month"]),
            Convert.ToInt32(dict["day"]),
            Convert.ToInt32(dict["hour"]),
            Convert.ToInt32(dict["minute"]),
            Convert.ToBoolean(dict["isLunar"]),
            dict["gender"].ToString(),
            dict["name"].ToString()
        );
    }

    private Dictionary<string, object> ConvertSajuDataToDictionary(SajuData sajuData)
    {
        return new Dictionary<string, object>
        {
            { "yearPillar", new Dictionary<string, object>
                {
                    { "heavenly", sajuData.yearPillar.heavenly },
                    { "earthly", sajuData.yearPillar.earthly }
                }
            },
            { "monthPillar", new Dictionary<string, object>
                {
                    { "heavenly", sajuData.monthPillar.heavenly },
                    { "earthly", sajuData.monthPillar.earthly }
                }
            },
            { "dayPillar", new Dictionary<string, object>
                {
                    { "heavenly", sajuData.dayPillar.heavenly },
                    { "earthly", sajuData.dayPillar.earthly }
                }
            },
            { "hourPillar", new Dictionary<string, object>
                {
                    { "heavenly", sajuData.hourPillar.heavenly },
                    { "earthly", sajuData.hourPillar.earthly }
                }
            },
            { "calculatedAt", Timestamp.FromDateTime(sajuData.calculatedAt.ToUniversalTime()) }
        };
    }

    private SajuData ConvertToSajuData(Dictionary<string, object> dict)
    {
        var sajuData = new SajuData();

        var yearPillarDict = dict["yearPillar"] as Dictionary<string, object>;
        sajuData.yearPillar = new Pillar(yearPillarDict["heavenly"].ToString(), yearPillarDict["earthly"].ToString());

        var monthPillarDict = dict["monthPillar"] as Dictionary<string, object>;
        sajuData.monthPillar = new Pillar(monthPillarDict["heavenly"].ToString(), monthPillarDict["earthly"].ToString());

        var dayPillarDict = dict["dayPillar"] as Dictionary<string, object>;
        sajuData.dayPillar = new Pillar(dayPillarDict["heavenly"].ToString(), dayPillarDict["earthly"].ToString());

        var hourPillarDict = dict["hourPillar"] as Dictionary<string, object>;
        sajuData.hourPillar = new Pillar(hourPillarDict["heavenly"].ToString(), hourPillarDict["earthly"].ToString());

        sajuData.calculatedAt = ((Timestamp)dict["calculatedAt"]).ToDateTime();

        return sajuData;
    }

    #endregion
}