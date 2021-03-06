﻿using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;


namespace UniGLTF.AltTask
{
    /// <summary>
    /// Importer 向けに Task を wrap した。
    /// 
    /// * EditorやUnitTestなどで、Unity の MainLoop を進行させずに 非同期を進行させることが目的
    /// 
    /// Global変数 SynchronizationContext.Current を一時的に変更したいのだが、
    /// システムに予期せぬ副作用を与える可能性があるのでこれを回避。
    /// UniGLTF.AltTask.TaskQueue.Current に同じ機能を与えて、タスクのPost先を制御することにした。
    /// </summary>

    public interface IAwaiter : INotifyCompletion
    {
        bool IsCompleted { get; }
        void GetResult();
    }

    public interface IAwaitable
    {
        IAwaiter GetAwaiter();
    }

    public struct AwaitableMethodBuilder
    {
        // https://referencesource.microsoft.com/#mscorlib/system/runtime/compilerservices/AsyncMethodBuilder.cs
        private AsyncTaskMethodBuilder _methodBuilder;

        public static AwaitableMethodBuilder Create() =>
            new AwaitableMethodBuilder { _methodBuilder = AsyncTaskMethodBuilder.Create() };

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            _methodBuilder.Start(ref stateMachine);
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            _methodBuilder.SetStateMachine(stateMachine);
        }
        public void SetException(Exception exception)
        {
            _methodBuilder.SetException(exception);
        }
        public void SetResult()
        {
            _methodBuilder.SetResult();
        }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            _methodBuilder.AwaitOnCompleted(ref awaiter, ref stateMachine);
        }
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            _methodBuilder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
        }

        public Awaitable Task => new Awaitable(_methodBuilder.Task);
    }

    [AsyncMethodBuilder(typeof(AwaitableMethodBuilder))]
    public struct Awaitable : IAwaitable
    {
        private Task _task;

        public Awaitable(Task task)
        {
            _task = task;
            if (_task.Exception != null)
            {
                throw _task.Exception;
            }
        }

        public bool IsCompleted => _task.IsCompleted;

        public Exception Exception => _task.Exception;

        public IAwaiter GetAwaiter()
        {
            return new Awaiter(this);
        }

        public void ContinueWith(Action action)
        {
            _task.ContinueWith((Task, _) =>
            {
                action();
            }, null);
        }

        public static Awaitable<T> Run<T>(Func<T> action)
        {
            return new Awaitable<T>(Task.Run(action));
        }

        public static Awaitable Run(Action action)
        {
            return new Awaitable(Task.Run(action));
        }

        public static Awaitable<T> FromResult<T>(T result)
        {
            return new Awaitable<T>(Task.FromResult(result));
        }
    }

    public class Awaiter : IAwaiter
    {
        Awaitable m_task;

        public bool IsCompleted
        {
            get
            {
                return m_task.IsCompleted;
            }
        }

        public void GetResult()
        {
            if (m_task.Exception != null)
            {
                throw m_task.Exception;
            }
        }

        public void OnCompleted(Action continuation)
        {
            var context = TaskQueue.Current;
            this.m_task.ContinueWith(() =>
            {
                context.Post(_ => continuation(), null);
            });
        }

        public Awaiter(Awaitable task)
        {
            m_task = task;
        }
    }
}
