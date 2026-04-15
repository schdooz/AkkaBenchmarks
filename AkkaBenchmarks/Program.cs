using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence;
using Akka.Persistence.MongoDb;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

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
    private readonly List<string> _items = new();

    public override string PersistenceId => $"BenchmarkingPersistentActor_{Guid.NewGuid()}";

    public BenchmarkingPersistentActor()
    {
        this.Command<string>(s =>
        {
            Persist(
                new Poco
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
                        }
                    ]
                },
                _ => { });

            Context.Sender.Tell(s);
       });
    }

    
}

public class BenchmarkClass
{
    ActorSystem _actorSystem;

    public BenchmarkClass()
    {
        var config = ConfigurationFactory.ParseString(@"
akka {
  persistence {
    journal {
      plugin = ""akka.persistence.journal.mongodb""
      mongodb {
        class = ""Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb""
        connection-string = ""mongodb://localhost:27017/akka-benchmark""
        collection = ""EventJournal""
        auto-initialize = on
      }
    }

    snapshot-store {
      plugin = ""akka.persistence.snapshot-store.mongodb""
      mongodb {
        class = ""Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb""
        connection-string = ""mongodb://localhost:27017/akka-benchmark""
        collection = ""SnapshotStore""
        auto-initialize = on
      }
    }
  }
}");

        var system = ActorSystem.Create("AkkaBenchmarks", config);

        // Initialize MongoDB persistence plugin
        MongoDbPersistence.Get(system);

        _actorSystem = system;
    }

    [Benchmark]
    public void Persist100Events() => PersistEvents(100, 1);

    [Benchmark]
    public void Persist1000Events() => PersistEvents(1000, 1);

    [Benchmark]
    public void Persist10000Events() => PersistEvents(10000, 1);

    private void PersistEvents(int numberOfActors, int numberOfEvents)
    {
        var actors = new List<IActorRef>();

        for (int i = 0; i < numberOfActors; i++)
        {
            actors.Add(_actorSystem.ActorOf(Props.Create(() => new BenchmarkingPersistentActor()), Guid.NewGuid().ToString()));
        }

        var persistTasks = new List<Task>();

        foreach (var actor in actors)
        {
            for (int i = 0; i < numberOfEvents; i++)
            {
                persistTasks.Add(actor.Ask(string.Empty));
            }
        }

        Task.WaitAll(persistTasks.ToArray());
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