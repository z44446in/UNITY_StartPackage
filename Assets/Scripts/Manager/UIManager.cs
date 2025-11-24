using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// UI Panel 전환 관리자
/// Scene 내의 모든 Panel을 관리하고 전환 처리
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject loginPanel; // 로그인/회원가입 버튼
    [SerializeField] private GameObject signUpPanel; // 소셜 로그인 선택
    [SerializeField] private GameObject referralCodePanel; // 추천인 코드 입력
    [SerializeField] private GameObject birthInfoPanel; // 생년월일시 입력

    private Stack<GameObject> panelHistory = new Stack<GameObject>(); // Panel 히스토리 (뒤로가기용)
    private GameObject currentPanel;

    private void Start()
    {
        // 초기화: 모든 Panel 비활성화 후 LoginPanel만 활성화
        HideAllPanels();
        ShowPanel(loginPanel);
    }

    #region Panel Navigation

    /// <summary>
    /// Panel 표시 (다른 Panel은 모두 숨김)
    /// </summary>
    public void ShowPanel(GameObject panel)
    {
        if (panel == null)
        {
            Debug.LogError("Panel이 null입니다.");
            return;
        }

        // 현재 Panel을 히스토리에 추가
        if (currentPanel != null && currentPanel != panel)
        {
            panelHistory.Push(currentPanel);
            currentPanel.SetActive(false);
        }

        // 새 Panel 활성화
        panel.SetActive(true);
        currentPanel = panel;

        Debug.Log($"Panel 전환: {panel.name}");
    }

    /// <summary>
    /// 모든 Panel 숨기기
    /// </summary>
    private void HideAllPanels()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (signUpPanel != null) signUpPanel.SetActive(false);
        if (referralCodePanel != null) referralCodePanel.SetActive(false);
        if (birthInfoPanel != null) birthInfoPanel.SetActive(false);
    }

    /// <summary>
    /// 이전 Panel로 돌아가기
    /// </summary>
    public void GoBack()
    {
        if (panelHistory.Count > 0)
        {
            if (currentPanel != null)
            {
                currentPanel.SetActive(false);
            }

            currentPanel = panelHistory.Pop();
            currentPanel.SetActive(true);

            Debug.Log($"이전 Panel로 이동: {currentPanel.name}");
        }
        else
        {
            Debug.LogWarning("이전 Panel이 없습니다.");
        }
    }

    #endregion

    #region Panel Shortcuts

    public void ShowLoginPanel() => ShowPanel(loginPanel);
    public void ShowSignUpPanel() => ShowPanel(signUpPanel);
    public void ShowReferralCodePanel() => ShowPanel(referralCodePanel);
    public void ShowBirthInfoPanel() => ShowPanel(birthInfoPanel);

    #endregion

    #region Utility

    /// <summary>
    /// 현재 활성화된 Panel 가져오기
    /// </summary>
    public GameObject GetCurrentPanel()
    {
        return currentPanel;
    }

    /// <summary>
    /// Panel 히스토리 초기화
    /// </summary>
    public void ClearHistory()
    {
        panelHistory.Clear();
    }

    #endregion
}