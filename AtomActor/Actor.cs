namespace AtomActor;

public struct Actor<A> where A : class
{
    public Actors Actors { get; }
    internal readonly ActorBag<A> bag;

    internal Actor(Actors actors, ActorBag<A> bag)
    {
        Actors = actors;
        this.bag = bag;
    }
}

public static class ActorOperators
{
    public static void Send<A, M>(in this Actor<A> self, M msg) where A : class, IActor<M> =>
        ActorBag.Send(self.bag, msg);
}
