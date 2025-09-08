using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class MainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> actions = new Queue<Action>();
    private static MainThreadDispatcher instance;

    private void Awake()
    {
        instance = this;
    }

    private void Update()
    {
        lock (actions)
        {
            while (actions.Count > 0)
            {
                actions.Dequeue().Invoke();
            }
        }
    }

    public static void ExecuteOnMainThread(Action action)
    {
        lock (actions)
        {
            actions.Enqueue(action);
        }
    }
    
    public static Task ExecuteOnMainThreadAsync(Action action)
    {
        var tcs = new TaskCompletionSource<bool>();
        
        ExecuteOnMainThread(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
        });
        
        return tcs.Task;
    }
}
