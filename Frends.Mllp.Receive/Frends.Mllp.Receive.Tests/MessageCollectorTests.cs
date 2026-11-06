using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Frends.Mllp.Receive.Helpers;
using NUnit.Framework;

namespace Frends.Mllp.Receive.Tests;

[TestFixture]
public class MessageCollectorTests
{
    [Test]
    public void Should_Return_Added_Items_In_Order()
    {
        var collector = new MessageCollector();
        Assert.That(collector.TryAdd("a"), Is.True);
        Assert.That(collector.TryAdd("b"), Is.True);

        Assert.That(collector.Close(), Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void Should_Refuse_Items_After_Close()
    {
        var collector = new MessageCollector();
        collector.TryAdd("a");
        collector.Close();

        Assert.That(collector.TryAdd("late"), Is.False);
        Assert.That(collector.Close(), Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public async Task Every_Accepted_Item_Must_Be_In_Snapshot_When_Closed_Concurrently()
    {
        const int writerCount = 8;
        const int itemsPerWriter = 2000;
        var racedRounds = 0;

        for (var round = 0; round < 200; round++)
        {
            var collector = new MessageCollector();
            var accepted = new ConcurrentBag<string>();
            var refused = 0;
            var acceptedCount = 0;
            string[] snapshot = null;

            // All writers and the closer start together, and the closer closes while writers are mid-way,
            // so Close() actually races with TryAdd() instead of running after the writers are done.
            using var start = new Barrier(writerCount + 1);

            var writers = Enumerable.Range(0, writerCount).Select(w => Task.Factory.StartNew(
                () =>
                {
                    start.SignalAndWait();
                    for (var i = 0; i < itemsPerWriter; i++)
                    {
                        var item = $"{w}-{i}";
                        if (collector.TryAdd(item))
                        {
                            accepted.Add(item);
                            Interlocked.Increment(ref acceptedCount);
                        }
                        else
                        {
                            Interlocked.Increment(ref refused);
                        }
                    }
                },
                TaskCreationOptions.LongRunning)).ToArray();

            var closer = Task.Factory.StartNew(
                () =>
                {
                    start.SignalAndWait();
                    SpinWait.SpinUntil(() => Volatile.Read(ref acceptedCount) >= writerCount * itemsPerWriter / 4);
                    snapshot = collector.Close();
                },
                TaskCreationOptions.LongRunning);

            await Task.WhenAll(writers.Append(closer));

            if (refused > 0)
                racedRounds++;

            Assert.That(snapshot, Has.Length.EqualTo(accepted.Count));
            Assert.That(snapshot, Is.EquivalentTo(accepted));
            Assert.That(snapshot.Length + refused, Is.EqualTo(writerCount * itemsPerWriter));
        }

        Assert.That(racedRounds, Is.GreaterThan(0), "Close() never raced with TryAdd(); the test did not exercise the race.");
    }
}
