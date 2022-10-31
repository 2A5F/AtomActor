using AtomActor;

namespace Tests;

public class Tests
{
    [SetUp]
    public void Setup() { }

    class IntBox
    {
        public int count;
    }

    class Incrementer : IPort<int>
    {
        private readonly IntBox box;

        public Incrementer(IntBox box)
        {
            this.box = box;
        }

        public ValueTask Port(int msg)
        {
            Interlocked.Add(ref box.count, msg);
            return ValueTask.CompletedTask;
        }
    }

    [Test]
    public void Test1()
    {
        var actors = new Actors();

        var box = new IntBox();
        actors.AddByCores(() => new Incrementer(box));

        var incrementer = actors.Get<Incrementer>();

        ParallelEnumerable.Range(0, 1000).ForAll(i => {
            incrementer.Send(i + 1);
        });
        
        Thread.Sleep(1000);

        Assert.That(box.count, Is.EqualTo(500500));
    }
    
    class Adder : IPort<int>
    {
        private readonly Actor<Incrementer> incrementer;

        public Adder(Actors actors)
        {
            incrementer = actors.Get<Incrementer>();
        }

        public ValueTask Port(int msg)
        {
            incrementer.Send(msg + msg);
            return ValueTask.CompletedTask;
        }
    }
    
    [Test]
    public void Test2()
    {
        var actors = new Actors();

        var count = new IntBox();
        actors.AddByCores(() => new Incrementer(count));
        actors.AddByCores(() => new Adder(actors));

        var adder = actors.Get<Adder>();

        ParallelEnumerable.Range(0, 1000).ForAll(i => {
            adder.Send(i + 1);
        });
        
        Thread.Sleep(1000);

        Assert.That(count.count, Is.EqualTo(1001000));
    }
}
