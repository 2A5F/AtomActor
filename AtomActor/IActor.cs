namespace AtomActor;

public interface IActor { }

public interface IPort<in T> : IActor
{
    public void Port(T msg);
}

public interface IPort<in A, in B> : IActor
{
    public void Port(A a, B b);
}

public interface IPort<in A, in B, in C> : IActor
{
    public void Port(A a, B b, C c);
}
