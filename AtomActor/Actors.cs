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
        AddN(ctor, Environment.ProcessorCount);

    public void AddN<T>(Func<T> ctor, int nums) where T : class
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
        var type = typeof(IPort<M>);
        var queue = Unsafe.As<ActorQueue<M>>(self.queues.GetOrAdd(type, _ => new ActorQueue<M>())).queue;
        self.actions.GetOrAdd(type, static (_, queue) => actor => {
            if (queue.TryDequeue(out var m)) actor.Port(m);
            else throw new NotImplementedException("never");
        }, queue);
        queue.Enqueue(msg);
        self.channel.Post(type);
    }
}

internal class ActorBag<T> : ActorBag where T : class
{
    internal readonly ConcurrentDictionary<T, ActorTask> bag = new();
    internal readonly ActorChannel channel = new();
    internal readonly ConcurrentDictionary<Type, ActorQueue> queues = new();
    internal readonly ConcurrentDictionary<Type, Action<T>> actions = new();

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
