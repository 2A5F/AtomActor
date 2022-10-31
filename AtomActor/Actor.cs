namespace AtomActor;

public struct Actor<T>
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
    public static void Send<A, M>(in this Actor<A> self, M msg) where A : IPort<M> => ActorBag.Send(self.bag, msg);
}
