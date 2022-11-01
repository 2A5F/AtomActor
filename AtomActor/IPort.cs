namespace AtomActor;

public interface IPort<in T>
{
    public void Port(T msg);
}
