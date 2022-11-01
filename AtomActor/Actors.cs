using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AtomActor;

public class Actors
{
    public static readonly Actors Global = new();

    private readonly ConcurrentDictionary<Type, ActorBag> items = new();

    internal ActorBag<T> GetBag<T>() where T : class =>
        Unsafe.As<ActorBag<T>>(items.GetOrAdd(typeof(T), _ => new ActorBag<T>()));

    internal ActorBag<T>? TryGetBag<T>() where T : class
    {
        items.TryGetValue(typeof(T), out var bag);
        return Unsafe.As<ActorBag<T>?>(bag);
    }

    public void Add<T>(T inst) where T : class
    {
        var bag = GetBag<T>();
        bag.Add(inst);
    }

    public void AddByCores<T>(Func<T> ctor) where T : class =>
        AddN(Environment.ProcessorCount, ctor);

    public void AddN<T>(int nums, Func<T> ctor) where T : class
    {
        Debug.Assert(nums > 0);
        var bag = GetBag<T>();
        for (int i = 0; i < nums; i++)
        {
            bag.Add(ctor());
        }
    }

    public Actor<T> Get<T>() where T : class
    {
        var bag = GetBag<T>();
        return new Actor<T>(this, bag);
    }
}

internal class ActorBag
{
    public static void Send<A, M>(ActorBag<A> self, M msg) where A : class, IPort<M>
    {
        self.Post(typeof(IPort<M>), msg, static (_, queue) => actor => {
            var msg = queue.StrictDequeue();
            actor.Port(msg);
        });
    }

    public static void Send<A, M1, M2>(ActorBag<A> self, M1 m1, M2 m2) where A : class, IPort<M1, M2>
    {
        self.Post(typeof(IPort<M1, M2>), (m1, m2), static (_, queue) => actor => {
            var (m1, m2) = queue.StrictDequeue();
            actor.Port(m1, m2);
        });
    }

    public static void Send<A, M1, M2, M3>(ActorBag<A> self, M1 m1, M2 m2, M3 m3) where A : class, IPort<M1, M2, M3>
    {
        self.Post(typeof(IPort<M1, M2, M3>), (m1, m2, m3), static (_, queue) => actor => {
            var (m1, m2, m3) = queue.StrictDequeue();
            actor.Port(m1, m2, m3);
        });
    }
}

internal class ActorBag<T> : ActorBag where T : class
{
    internal readonly ConcurrentDictionary<T, ActorTask> bag = new();
    internal readonly ActorChannel channel = new();
    internal readonly ConcurrentDictionary<Type, ActorQueue> queues = new();
    internal readonly ConcurrentDictionary<Type, Action<T>> actions = new();

    #region Post

    internal ActorQueue<M> UnsafeGetQueue<M>(Type type) =>
        Unsafe.As<ActorQueue<M>>(queues.GetOrAdd(type, _ => new ActorQueue<M>()));

    internal void EnsureAction<M>(ActorQueue<M> queue, Type type, Func<Type, ActorQueue<M>, Action<T>> fn) =>
        actions.GetOrAdd(type, fn, queue);

    internal void Post<M>(ActorQueue<M> queue, Type type, M msg)
    {
        queue.Enqueue(msg);
        channel.Post(type);
    }

    public void Post<M>(Type type, M msg, Func<Type, ActorQueue<M>, Action<T>> fn)
    {
        var queue = UnsafeGetQueue<M>(type);
        EnsureAction(queue, type, fn);
        Post(queue, type, msg);
    }

    #endregion

    public void Add(T actor)
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var task = Task.Factory.StartNew(() => {
            while (!token.IsCancellationRequested)
            {
                var type = channel.Take(token);
                if (actions.TryGetValue(type, out var action))
                {
                    action(actor);
                }
                else throw new NotImplementedException("never");
            }
        }, TaskCreationOptions.LongRunning);
        bag.TryAdd(actor, new ActorTask(cts, task));
    }
}

internal class ActorTask
{
    public readonly CancellationTokenSource cts;
    public readonly Task task;

    public ActorTask(CancellationTokenSource cts, Task task)
    {
        this.cts = cts;
        this.task = task;
    }
}

internal class ActorQueue { }

internal class ActorQueue<T> : ActorQueue
{
    public readonly ConcurrentQueue<T> queue = new();

    public T StrictDequeue()
    {
        if (queue.TryDequeue(out var msg)) return msg;
        throw new NotImplementedException("never");
    }

    public void Enqueue(T msg) => queue.Enqueue(msg);
}

internal class ActorChannel
{
    public readonly ConcurrentQueue<Type> queue = new();
    public readonly ManualResetEventSlim signal = new();

    public void Post(Type type)
    {
        queue.Enqueue(type);
        signal.Set();
    }

    public Type Take(CancellationToken token)
    {
        for (;;)
        {
            if (queue.TryDequeue(out var type)) return type;
            signal.Wait(token);
            signal.Reset();
        }
    }
}
