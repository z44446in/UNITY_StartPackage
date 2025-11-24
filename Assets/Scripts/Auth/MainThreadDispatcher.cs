using System.Collections.Generic;
using UnityEngine;

public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher instance;
    private readonly Queue<System.Action> executionQueue = new Queue<System.Action>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        lock (executionQueue)
        {
            while (executionQueue.Count > 0)
            {
                executionQueue.Dequeue()?.Invoke();
            }
        }
    }

    public static void RunOnMainThread(System.Action action)
    {
        if (instance != null)
        {
            lock (instance.executionQueue)
            {
                instance.executionQueue.Enqueue(action);
            }
        }
    }
}
