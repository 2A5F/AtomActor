namespace AtomActor;

public interface IActor<in T> 
{
    public void Receive(T msg);
}
