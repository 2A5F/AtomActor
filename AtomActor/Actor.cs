namespace AtomActor;

public struct Actor<T> where T : class
{
    public Actors Actors { get; }
    internal readonly ActorBag<T> bag;

    internal Actor(Actors actors, ActorBag<T> bag)
    {
        Actors = actors;
        this.bag = bag;
    }
}

public static class ActorOperators
{
    public static void Send<A, M>(in this Actor<A> self, M msg) where A : class, IPort<M> =>
        ActorBag.Send(self.bag, msg);

    public static void Send<A, M1, M2>(in this Actor<A> self, M1 m1, M2 m2) where A : class, IPort<M1, M2> =>
        ActorBag.Send(self.bag, m1, m2);

    public static void Send<A, M1, M2, M3>(in this Actor<A> self, M1 m1, M2 m2, M3 m3)
        where A : class, IPort<M1, M2, M3> =>
        ActorBag.Send(self.bag, m1, m2, m3);
}
