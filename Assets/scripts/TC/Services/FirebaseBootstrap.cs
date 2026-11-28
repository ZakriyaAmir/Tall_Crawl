using System;
using System.Threading.Tasks;
using Firebase;

/// <summary>
/// The only CheckAndFixDependenciesAsync in the game. Analytics and push both await ReadyTask.
/// A second call throws "Don't call other Firebase functions while CheckDependencies is running."
/// </summary>
public static class FirebaseBootstrap
{
    static readonly object Gate = new object();
    static Task<DependencyStatus> _ready;

    public static Task<DependencyStatus> ReadyTask
    {
        get
        {
            lock (Gate)
            {
                if (_ready == null)
                {
                    try
                    {
                        _ready = FirebaseApp.CheckAndFixDependenciesAsync();
                    }
                    catch (Exception e)
                    {
                        var pending = new TaskCompletionSource<DependencyStatus>();
                        pending.SetException(e);
                        _ready = pending.Task;
                    }
                }
                return _ready;
            }
        }
    }
}
