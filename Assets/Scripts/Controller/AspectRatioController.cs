using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace Nursing.Core
{
    /// <summary>
    /// 전역 Canvas Scaler 관리 싱글톤
    /// 모든 씬에서 자동으로 Canvas들의 Match 값을 조정
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class AspectRatioController : MonoBehaviour
    {
        [Header("Target Resolution")]
        [SerializeField] private int targetWidth = 1080;
        [SerializeField] private int targetHeight = 1920;
        
        public static AspectRatioController Instance { get; private set; }
        
        private float targetAspectRatio;
        private Vector2Int lastScreenSize;
        private List<Canvas> managedCanvases = new List<Canvas>();
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeController();
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        
        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        
        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        
        private void Update()
        {
            // 화면 크기가 변경된 경우에만 업데이트
            if (HasScreenSizeChanged())
            {
                UpdateAllCanvases();
                lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private void InitializeController()
        {
            targetAspectRatio = (float)targetWidth / targetHeight;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            
            // 현재 씬의 Canvas들 검색
            FindAndRegisterCanvases();
        }
        
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 새 씬 로드 시 Canvas들 재검색
            FindAndRegisterCanvases();
        }
        
        private void FindAndRegisterCanvases()
{
    managedCanvases.RemoveAll(canvas => canvas == null);
    
    // 비활성화된 Canvas도 포함
    Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    
    foreach (Canvas canvas in allCanvases)
    {
        if ((canvas.renderMode == RenderMode.ScreenSpaceOverlay || 
             canvas.renderMode == RenderMode.ScreenSpaceCamera) &&
            !managedCanvases.Contains(canvas))
        {
            managedCanvases.Add(canvas);
        }
    }
    
    UpdateAllCanvases();
}
        
        private void UpdateAllCanvases()
        {
            if (managedCanvases.Count == 0) return;

            float currentAspectRatio = (float)Screen.width / Screen.height;
            float matchValue = currentAspectRatio > targetAspectRatio ? 1f : 0f;

            // 파괴된 Canvas 제거 후 업데이트
            managedCanvases.RemoveAll(canvas => canvas == null);
            
            foreach (Canvas canvas in managedCanvases)
            {
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    scaler.matchWidthOrHeight = matchValue;
                }
            }
        }
        
        private bool HasScreenSizeChanged()
        {
            return lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height;
        }
        
        #endregion
    }
}