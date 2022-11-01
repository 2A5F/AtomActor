namespace AtomActor;

public struct Actor<T> where T : class
{
    public readonly Actors actors;
    internal readonly ActorBag<T> bag;

    internal Actor(Actors actors, ActorBag<T> bag)
    {
        this.actors = actors;
        this.bag = bag;
    }
}

public static class ActorOperators
{
    public static void Send<A, M>(in this Actor<A> self, M msg) where A : class, IPort<M> =>
        ActorBag.Send(self.bag, msg);
}
