using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace AtomActor;

public class Actors
{
    public static readonly Actors Global = new();

    private readonly ConcurrentDictionary<Type, ActorBag> items = new();

    internal ActorBag<T> GetBag<T>() => Unsafe.As<ActorBag<T>>(items.GetOrAdd(typeof(T), _ => new ActorBag<T>()));

    internal ActorBag<T>? TryGetBag<T>()
    {
        items.TryGetValue(typeof(T), out var bag);
        return Unsafe.As<ActorBag<T>?>(bag);
    }

    public void Add<T>(T inst)
    {
        var bag = GetBag<T>();
        bag.Add(inst);
    }

    public void AddByCores<T>(Func<T> ctor) => AddN(ctor, Environment.ProcessorCount);

    public void AddN<T>(Func<T> ctor, int nums)
    {
        Debug.Assert(nums > 0);
        var bag = GetBag<T>();
        for (int i = 0; i < nums; i++)
        {
            bag.Add(ctor());
        }
    }

    public Actor<T> Get<T>()
    {
        var bag = GetBag<T>();
        return new Actor<T>(this, bag);
    }
}

internal class ActorBag
{
    public static void Send<A, M>(ActorBag<A> self, M msg) where A : IPort<M>
    {
        var type = typeof(M);
        var queue = Unsafe.As<ActorQueue<M>>(self.queues.GetOrAdd(type, _ => new ActorQueue<M>())).queue;
        self.actions.GetOrAdd(type, static (_, queue) => async actor => {
            if (queue.TryDequeue(out var m)) await actor.Port(m);
            else throw new NotImplementedException("never");
        }, queue);
        queue.Enqueue(msg);
        self.channel.Writer.TryWrite(type);
    }
}

internal class ActorBag<T> : ActorBag
{
    // internal readonly ConcurrentBag<T> bag = new();
    internal readonly Channel<Type> channel = Channel.CreateUnbounded<Type>();
    internal readonly ConcurrentDictionary<Type, ActorQueue> queues = new();
    internal readonly ConcurrentDictionary<Type, Func<T, ValueTask>> actions = new();

    public void Add(T actor)
    {
        // bag.Add(actor);
        Task.Run(async () => {
            for (;;)
            {
                var type = await channel.Reader.ReadAsync();
                if (actions.TryGetValue(type, out var action))
                {
                    await action(actor);
                }
                else throw new NotImplementedException("never");
            }
        });
    }
}

internal class ActorQueue { }

internal class ActorQueue<T> : ActorQueue
{
    public readonly ConcurrentQueue<T> queue = new();
}
