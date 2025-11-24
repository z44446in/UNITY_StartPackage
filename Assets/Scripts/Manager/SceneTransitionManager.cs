using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
using System;

/// <summary>
/// Scene 전환 관리자
/// DOTween을 사용한 Fade In/Out 효과 제공
/// Single Scene Loading 방식
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField] private Image fadeImage; // Fade용 검은색 이미지
    [SerializeField] private float fadeDuration = 0.5f; // Fade 시간

    private static SceneTransitionManager instance;
    public static SceneTransitionManager Instance
    {
        get
        {
            if (instance == null)
            {
                var obj = new GameObject("SceneTransitionManager");
                instance = obj.AddComponent<SceneTransitionManager>();
                DontDestroyOnLoad(obj);
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // FadeImage가 없으면 생성
        if (fadeImage == null)
        {
            CreateFadeCanvas();
        }
    }

    /// <summary>
    /// Fade용 Canvas 및 Image 자동 생성
    /// </summary>
    private void CreateFadeCanvas()
    {
        // Canvas 생성
        var canvasObj = new GameObject("FadeCanvas");
        canvasObj.transform.SetParent(transform);

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // 최상위 렌더링

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Fade Image 생성
        var imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;

        // 전체 화면 크기로 설정
        var rectTransform = fadeImage.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;

        // 초기 상태: 투명
        fadeImage.color = new Color(0, 0, 0, 0);
    }

    /// <summary>
    /// Scene 전환 (Fade 효과 포함)
    /// </summary>
    /// <param name="sceneName">이동할 Scene 이름</param>
    /// <param name="onComplete">전환 완료 후 콜백</param>
    public void LoadScene(string sceneName, Action onComplete = null)
    {
        // Fade Out → Scene Load → Fade In
        Sequence sequence = DOTween.Sequence();

        // 1. Fade Out (검은색으로 페이드)
        sequence.Append(fadeImage.DOFade(1f, fadeDuration));

        // 2. Scene Load
        sequence.AppendCallback(() =>
        {
            SceneManager.LoadScene(sceneName);
        });

        // 3. Fade In (투명하게 페이드)
        sequence.Append(fadeImage.DOFade(0f, fadeDuration));

        // 4. 완료 콜백
        sequence.OnComplete(() =>
        {
            onComplete?.Invoke();
        });
    }

    /// <summary>
    /// Scene 전환 (비동기, Fade 효과 포함)
    /// </summary>
    public void LoadSceneAsync(string sceneName, Action<float> onProgress = null, Action onComplete = null)
    {
        Sequence sequence = DOTween.Sequence();

        // 1. Fade Out
        sequence.Append(fadeImage.DOFade(1f, fadeDuration));

        // 2. Async Scene Load
        sequence.AppendCallback(() =>
        {
            var asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            asyncLoad.completed += (op) =>
            {
                // Fade In
                fadeImage.DOFade(0f, fadeDuration).OnComplete(() =>
                {
                    onComplete?.Invoke();
                });
            };

            // Progress 콜백 (필요 시)
            if (onProgress != null)
            {
                DOVirtual.Float(0f, 1f, 2f, (progress) =>
                {
                    onProgress?.Invoke(asyncLoad.progress);
                });
            }
        });
    }

    /// <summary>
    /// 현재 Scene 이름 가져오기
    /// </summary>
    public string GetCurrentSceneName()
    {
        return SceneManager.GetActiveScene().name;
    }
}