using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence;
using Akka.Persistence.MongoDb;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Collections.Concurrent;

// Test Poco used by Hyperion benchmarks
public class Poco
{
    public required string StringProp { get; set; }
    public int IntProp { get; set; }
    public Guid GuidProp { get; set; }
    public DateTime DateProp { get; set; }
    public required List<Poco2> ListProp { get; set; }
}

public class Poco2
{
    public required string StringProp { get; set; }
    public int IntProp { get; set; }
    public Guid GuidProp { get; set; }
    public DateTime DateProp { get; set; }
}


// Persistent actor
public class BenchmarkingPersistentActor : ReceivePersistentActor
{
    private static Poco s_Event = new Poco
    {
        StringProp = "hello",
        IntProp = 123,
        GuidProp = Guid.NewGuid(),
        DateProp = DateTime.Now,
        ListProp = [
            new Poco2
            {
                StringProp = "hello",
                IntProp = 123,
                GuidProp = Guid.NewGuid(),
                DateProp = DateTime.Now
            },
            new Poco2
            {
                StringProp = "hello",
                IntProp = 123,
                GuidProp = Guid.NewGuid(),
                DateProp = DateTime.Now
            },
            new Poco2
            {
                StringProp = "hello",
                IntProp = 123,
                GuidProp = Guid.NewGuid(),
                DateProp = DateTime.Now
            },
            new Poco2
            {
                StringProp = "hello",
                IntProp = 123,
                GuidProp = Guid.NewGuid(),
                DateProp = DateTime.Now
            },
            new Poco2
            {
                StringProp = "hello",
                IntProp = 123,
                GuidProp = Guid.NewGuid(),
                DateProp = DateTime.Now
            }]
    };

    private ConcurrentBag<IActorRef> _initializedActors;

    public BenchmarkingPersistentActor(ConcurrentBag<IActorRef> initializedActors)
    {
        _initializedActors = initializedActors;

        this.Command<string>(s =>
        {
            Persist(s_Event, _ => { });

            Context.Sender.Tell(s);
        });
    }

    public override string PersistenceId => $"BenchmarkingPersistentActor_{Guid.NewGuid()}";

    protected override void OnReplaySuccess()
    {
        base.OnReplaySuccess();
        _initializedActors.Add(Self);
    }
}

public class BenchmarkClass
{
    readonly List<IActorRef> _actors = [];

    public BenchmarkClass()
    {
        var config = ConfigurationFactory.ParseString(@"
akka {
  persistence {
    journal {
      plugin = ""akka.persistence.journal.mongodb""
      mongodb {
        class = ""Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb""
        connection-string = ""mongodb://localhost:27017/akka-benchmark?maxPoolSize=10010&maxConnecting=10010""
        collection = ""EventJournal""
        auto-initialize = on
      }
    }

    snapshot-store {
      plugin = ""akka.persistence.snapshot-store.mongodb""
      mongodb {
        class = ""Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb""
        connection-string = ""mongodb://localhost:27017/akka-benchmark?maxPoolSize=10010&maxConnecting=10010""
        collection = ""SnapshotStore""
        auto-initialize = on
      }
    }
  }
}");

        var actorSystem = ActorSystem.Create("AkkaBenchmarks", config);
        MongoDbPersistence.Get(actorSystem);

        var initializedActors = new ConcurrentBag<IActorRef>();
        var numberOfActors = 10000;

        for (int i = 0; i < numberOfActors; i++)
        {
            _actors.Add(actorSystem.ActorOf(Props.Create(() => new BenchmarkingPersistentActor(initializedActors)), Guid.NewGuid().ToString()));
        }

        while (initializedActors.Count < numberOfActors)
        {
            Task.Delay(1000).Wait();
        }
    }

    [Benchmark]
    public void Persist100Events() => PersistEvents(100);

    [Benchmark]
    public void Persist1000Events() => PersistEvents(1000);

    [Benchmark]
    public void Persist10000Events() => PersistEvents(10000);

    private void PersistEvents(int numberOfEvents)
    {
        var persistTasks = new List<Task>();

        for (int i = 0; i < numberOfEvents; i++)
        {
            var actor = _actors[i];
            persistTasks.Add(actor.Ask(string.Empty));
        }

        Task.WaitAll([.. persistTasks]);
    }
}

internal static class Program
{
    private static void Main()
    {
        var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
    }
}

/*
| Method             | Mean        | Error     | StdDev    |
|------------------- |------------:|----------:|----------:|
| Persist100Events   |    27.90 ms |  0.527 ms |  0.627 ms |
| Persist1000Events  |   275.67 ms |  5.572 ms | 15.898 ms |
| Persist10000Events | 3,027.55 ms | 40.364 ms | 37.757 ms |
*/