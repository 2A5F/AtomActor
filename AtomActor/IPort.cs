namespace AtomActor;

public interface IPort<in T>
{
    public ValueTask Port(T msg);
}
