using System.Diagnostics;
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

    class Summer : IPort<int>
    {
        private readonly IntBox box;

        public Summer(IntBox box)
        {
            this.box = box;
        }

        public void Port(int msg)
        {
            Interlocked.Add(ref box.count, msg);
        }
    }

    [Test]
    public void Test1()
    {
        var actors = new Actors();

        var box = new IntBox();
        actors.AddByCores(() => new Summer(box));

        var summer = actors.Get<Summer>();

        ParallelEnumerable.Range(0, 1000).ForAll(i => {
            summer.Send(i + 1);
        });

        Thread.Sleep(1000);

        Assert.That(box.count, Is.EqualTo(500500));
    }

    class Adder : IPort<int>
    {
        private readonly Actor<Summer> summer;

        public Adder(Actors actors)
        {
            summer = actors.Get<Summer>();
        }

        public void Port(int msg)
        {
            summer.Send(msg + msg);
        }
    }

    [Test]
    public void Test2()
    {
        var actors = new Actors();

        var count = new IntBox();
        actors.AddByCores(() => new Summer(count));
        actors.AddByCores(() => new Adder(actors));

        var adder = actors.Get<Adder>();

        ParallelEnumerable.Range(0, 1000).ForAll(i => {
            adder.Send(i + 1);
        });

        Thread.Sleep(1000);

        Assert.That(count.count, Is.EqualTo(1001000));
    }

    class Fiber : IPort<int>, IPort<int, int, int>
    {
        private Actor<Fiber> fiber;
        private Box<TaskCompletionSource<int>> res;

        public Fiber(Actors actors, Box<TaskCompletionSource<int>> res)
        {
            fiber = actors.Get<Fiber>();
            this.res = res;
        }

        public void Port(int n, int a, int b)
        {
            if (n == 0) res.value.SetResult(a);
            fiber.Send(n - 1, b, a + b);
        }

        public void Port(int n)
        {
            fiber.Send(n, 0, 1);
        }
    }

    class Box<T>
    {
        public T value;
    }

    [Test]
    public async Task Test3()
    {
        var actors = new Actors();

        var res = new Box<TaskCompletionSource<int>>();
        actors.AddN(2, () => new Fiber(actors, res));

        var fiber = actors.Get<Fiber>();

        for (int i = 0; i < 1000; i++)
        {
            res.value = new();

            var sw = new Stopwatch();
            sw.Start();

            fiber.Send(30);

            var r = await res.value.Task;

            sw.Stop();
            Console.WriteLine(sw.Elapsed);

            Assert.That(r, Is.EqualTo(832040));
        }
    }

    class FiberTask1
    {
        async Task<int> Port(int n, int a, int b)
        {
            await Task.Yield();
            if (n == 0) return a;
            return await Port(n - 1, b, a + b);
        }

        public async Task<int> Port(int n)
        {
            return await Port(n, 0, 1);
        }
    }

    [Test]
    public async Task Test3Task1()
    {
        var fiber = new FiberTask1();

        for (int i = 0; i < 1000; i++)
        {
            var sw = new Stopwatch();
            sw.Start();
            var r = await fiber.Port(30);
            sw.Stop();
            Console.WriteLine(sw.Elapsed);

            Assert.That(r, Is.EqualTo(832040));
        }
    }
}
