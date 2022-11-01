using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using BindingFlags = System.Reflection.BindingFlags;

namespace AtomActor;

public class Actors
{
    public static readonly Actors Global = new();

    private readonly ConcurrentDictionary<Type, ActorBag> items = new();

    internal ActorBag<A> GetBag<A>() where A : class =>
        Unsafe.As<ActorBag<A>>(items.GetOrAdd(typeof(A), _ => ActorBag<A>.Create()));

    internal ActorBag<A>? TryGetBag<A>() where A : class
    {
        items.TryGetValue(typeof(A), out var bag);
        return Unsafe.As<ActorBag<A>?>(bag);
    }

    public void Add<A>(A inst) where A : class
    {
        var bag = GetBag<A>();
        bag.Add(inst);
    }

    public void AddByCores<A>(Func<A> ctor) where A : class =>
        AddN(Environment.ProcessorCount, ctor);

    public void AddN<A>(int nums, Func<A> ctor) where A : class
    {
        Debug.Assert(nums > 0);
        var bag = GetBag<A>();
        for (int i = 0; i < nums; i++)
        {
            bag.Add(ctor());
        }
    }

    public Actor<A> Get<A>() where A : class
    {
        var bag = GetBag<A>();
        return new Actor<A>(this, bag);
    }
}

internal abstract class ActorBag
{
    public static void Send<A, M>(ActorBag<A> self, M msg) where A : class, IActor<M>
    {
        var bag = Unsafe.As<ActorBag<A, M>>(self);
        bag.Post(msg);
    }
}

internal abstract class ActorBag<A> : ActorBag where A : class
{
    private static readonly ConcurrentDictionary<Type, Func<ActorBag<A>>> ctor_cache = new();

    public static ActorBag<A> Create()
    {
        var type = typeof(A);
        var fn = ctor_cache.GetOrAdd(type, static type => {
            var exist = false;
            Func<ActorBag<A>> fn = null!;
            foreach (var i_interface in type.GetInterfaces())
            {
                if (!i_interface.IsGenericType) continue;
                var def = i_interface.GetGenericTypeDefinition();
                if (def != typeof(IActor<>)) continue;
                if (exist)
                    throw new ArgumentException(
                        $"Type {type.Name} duplicates implementation of interface {typeof(IActor<>).Name}");
                exist = true;
                var m_type = i_interface.GenericTypeArguments[0];
                var bag_type = typeof(ActorBag<,>).MakeGenericType(type, m_type);
                var ctor_info = bag_type.GetConstructor(Array.Empty<Type>())!;
                var e_ctor = Expression.New(ctor_info);
                var e_cast = Expression.TypeAs(e_ctor, typeof(ActorBag<A>));
                var lambda = Expression.Lambda<Func<ActorBag<A>>>(e_cast);
                fn = lambda.Compile();
            }

            if (!exist)
                throw new ArgumentException($"Type {type.Name} not implemented interface {typeof(IActor<>).Name}");

            return fn;
        });
        return fn();
    }

    public abstract void Add(A actor);
}

internal class ActorBag<A, M> : ActorBag<A> where A : class, IActor<M>
{
    internal readonly ConcurrentDictionary<A, ActorTask> bag = new();
    internal readonly Channel<M> channel = Channel.CreateUnbounded<M>();

    public override void Add(A actor)
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var task = Task.Run(async () => {
            while (!token.IsCancellationRequested)
            {
                for (var retry = 0; retry < 100; retry++)
                {
                    if (channel.Reader.TryRead(out var msg))
                    {
                        actor.Receive(msg);
                        retry = 0;
                    }
                }

                await channel.Reader.WaitToReadAsync(token);
            }
        }, token);
        bag.TryAdd(actor, new ActorTask(cts, task));
    }

    public void Post(M msg)
    {
        channel.Writer.TryWrite(msg);
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
