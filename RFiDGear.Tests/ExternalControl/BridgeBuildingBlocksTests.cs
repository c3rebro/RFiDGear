#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Services.ExternalControl;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The parts of the transport that need no pipe: the scanner that refuses a message that is too long before anything of it
    /// is kept, the stream around the pipe, the queue of events with its two kinds, the catalog the capability list is built
    /// from, and the pipe name.
    /// </summary>
    public class BridgeBuildingBlocksTests
    {
        private static byte[] Frame(string body) => Encoding.ASCII.GetBytes($"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}");

        private static HeaderScanResult FeedInPieces(HeaderScanner scanner, byte[] data, int pieceSize)
        {
            for (var offset = 0; offset < data.Length; offset += pieceSize)
            {
                var result = scanner.Feed(data.AsSpan(offset, Math.Min(pieceSize, data.Length - offset)));

                if (result != HeaderScanResult.Ok)
                {
                    return result;
                }
            }

            return HeaderScanResult.Ok;
        }

        // ── The scanner ──

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(7)]
        [InlineData(1000)]
        public void MessagesAreFollowedInAnySplit_AlsoBackToBack(int pieceSize)
        {
            var data = Frame("{\"a\":1}").Concat(Frame("{\"b\":\"two\"}")).Concat(Frame(string.Empty)).Concat(Frame("{}")).ToArray();

            Assert.Equal(HeaderScanResult.Ok, FeedInPieces(new HeaderScanner(1024), data, pieceSize));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(4096)]
        public void AMessageOverTheLimit_IsRefusedAtTheEndOfItsHeader_BeforeAnyOfItsBodyIsSeen(int pieceSize)
        {
            var scanner = new HeaderScanner(1000);
            var header = Encoding.ASCII.GetBytes("Content-Length: 1001\r\n\r\n");

            Assert.Equal(HeaderScanResult.TooLarge, FeedInPieces(scanner, header, pieceSize));
        }

        [Fact]
        public void AMessageOfExactlyTheLimit_IsAccepted()
        {
            var body = new string('x', 100);

            Assert.Equal(HeaderScanResult.Ok, new HeaderScanner(100).Feed(Frame(body)));
            Assert.Equal(HeaderScanResult.TooLarge, new HeaderScanner(99).Feed(Frame(body)));
        }

        [Fact]
        public void TheHeaderNameIsMatchedWithoutCase_AndOtherHeadersAreIgnored()
        {
            var data = Encoding.ASCII.GetBytes("content-type: application/vscode-jsonrpc; charset=utf-8\r\nCONTENT-LENGTH:   5  \r\n\r\nhello");

            Assert.Equal(HeaderScanResult.Ok, new HeaderScanner(10).Feed(data));
        }

        [Theory]
        [InlineData("\r\n\r\nbody")]
        [InlineData("Content-Length: abc\r\n\r\n")]
        [InlineData("Content-Length: -5\r\n\r\n")]
        [InlineData("Content-Length: 99999999999999999999\r\n\r\n")]
        [InlineData("Content-Length:\r\n\r\n")]
        [InlineData("Content-Length: 5\rX\n\r\n")]
        [InlineData("Content-Type: x\r\n\r\n")]
        public void FramingThatIsNotTheProtocols_IsRefused(string text)
        {
            Assert.Equal(HeaderScanResult.Malformed, new HeaderScanner(1000).Feed(Encoding.ASCII.GetBytes(text)));
        }

        [Fact]
        public void AHeaderLineThatNeverEnds_IsRefusedWithoutBeingKept()
        {
            var scanner = new HeaderScanner(1000);

            Assert.Equal(HeaderScanResult.Malformed, scanner.Feed(new byte[100_000].Select(_ => (byte)'a').ToArray()));
        }

        [Fact]
        public void TheBodyIsSkippedWhateverItHolds_AFakeHeaderInsideItIsNotAHeader()
        {
            // A body that looks like a header must not be read as one.
            var inner = "Content-Length: 99999999\r\n\r\n";
            var data = Frame(inner).Concat(Frame("{}")).ToArray();

            Assert.Equal(HeaderScanResult.Ok, new HeaderScanner(1024).Feed(data));
        }

        // ── The stream ──

        [Fact]
        public async Task TheStream_PassesWhatIsWithinTheLimit_AndEndsTheConnectionAtAnOversizedHeader()
        {
            var good = Frame("{\"ok\":true}");
            using var fine = new CappedReadStream(new MemoryStream(good), 1024);
            var buffer = new byte[good.Length];
            Assert.Equal(good.Length, await fine.ReadAsync(buffer));
            Assert.Equal(good, buffer);

            var oversized = Encoding.ASCII.GetBytes("Content-Length: 2000000000\r\n\r\n");
            using var refused = new CappedReadStream(new MemoryStream(oversized), 1024 * 1024);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await refused.ReadAsync(new byte[256]));
            Assert.True(refused.EndedBecauseTooLarge);
        }

        [Fact]
        public async Task TheStream_WritesPassThroughUnchanged()
        {
            var target = new MemoryStream();
            using var stream = new CappedReadStream(target, 1024);

            await stream.WriteAsync(new byte[] { 1, 2, 3 });

            Assert.Equal(new byte[] { 1, 2, 3 }, target.ToArray());
        }

        // ── The queue ──

        private static BridgeEventFactory Event(string tag) =>
            (seq, session, at) => new ProjectChangedEvent { Seq = seq, SessionId = session, At = at, Path = tag };

        [Fact]
        public async Task Events_AreTakenInTheOrderTheyWerePutIn()
        {
            var queue = new BridgeEventQueue();
            queue.EnqueueState("readerChanged", Event("a"));
            queue.EnqueueDurable("runCompleted", Event("b"));
            queue.EnqueueState("projectChanged", Event("c"));

            Assert.Equal("readerChanged", (await queue.DequeueAsync(CancellationToken.None))!.Name);
            Assert.Equal("runCompleted", (await queue.DequeueAsync(CancellationToken.None))!.Name);
            Assert.Equal("projectChanged", (await queue.DequeueAsync(CancellationToken.None))!.Name);
        }

        [Fact]
        public async Task AStateEvent_IsReplacedByANewerOneOfTheSameName_AndKeepsItsPlace()
        {
            var queue = new BridgeEventQueue();
            queue.EnqueueState("readerChanged", Event("old"));
            queue.EnqueueDurable("runCompleted", Event("result"));
            queue.EnqueueState("readerChanged", Event("new"));

            var first = (await queue.DequeueAsync(CancellationToken.None))!;
            var second = (await queue.DequeueAsync(CancellationToken.None))!;

            Assert.Equal("readerChanged", first.Name);
            Assert.Equal("new", ((ProjectChangedEvent)first.Create(1, "s", DateTimeOffset.UnixEpoch)).Path);
            Assert.Equal("runCompleted", second.Name);
            queue.Complete();
            Assert.Null(await queue.DequeueAsync(CancellationToken.None));
        }

        [Fact]
        public async Task DurableEvents_AreNeverMerged_AndTheLimitSaysSoInsteadOfDroppingOne()
        {
            var queue = new BridgeEventQueue();

            for (var i = 0; i < BridgeEventQueue.DurableCap; i++)
            {
                Assert.True(queue.EnqueueDurable("runCompleted", Event(i.ToString())));
            }

            Assert.False(queue.EnqueueDurable("runCompleted", Event("one too many")));

            // What is in stays in, in order; taking one makes room again.
            var first = (await queue.DequeueAsync(CancellationToken.None))!;
            Assert.Equal("0", ((ProjectChangedEvent)first.Create(1, "s", DateTimeOffset.UnixEpoch)).Path);
            Assert.True(queue.EnqueueDurable("runCompleted", Event("fits again")));
        }

        [Fact]
        public async Task AnEmptyQueue_WaitsForTheNextEvent_AndAnEndedOneAnswersNull()
        {
            var queue = new BridgeEventQueue();
            var waiting = queue.DequeueAsync(CancellationToken.None).AsTask();

            Assert.False(waiting.IsCompleted);
            queue.EnqueueState("readerChanged", Event("x"));
            Assert.Equal("readerChanged", (await waiting.WaitAsync(TimeSpan.FromSeconds(5)))!.Name);

            queue.Complete();
            Assert.Null(await queue.DequeueAsync(CancellationToken.None));
            Assert.False(queue.EnqueueState("readerChanged", Event("late")));
            Assert.False(queue.EnqueueDurable("runCompleted", Event("late")));
        }

        [Fact]
        public async Task AWaitingConsumer_CanBeCancelled()
        {
            var queue = new BridgeEventQueue();
            using var cancel = new CancellationTokenSource();
            var waiting = queue.DequeueAsync(cancel.Token).AsTask();

            cancel.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        }

        // ── The catalog and the pipe name ──

        [Fact]
        public void TheCapabilityList_IsBuiltFromTheAttributes_AndEqualsTheContractsList()
        {
            Assert.Equal(BridgeCapabilities.All, BridgeCatalog.AllCapabilities);
            Assert.Equal(typeof(IBridgeService).GetMethods().Length, BridgeCatalog.MethodCapabilities.Count);
            Assert.Equal(typeof(IBridgeClientEvents).GetMethods().Length, BridgeCatalog.EventCapabilities.Count);
            Assert.Equal(BridgeCapabilities.ProjectLoad, BridgeCatalog.MethodCapabilities[BridgeMethods.LoadProject]);
            Assert.Equal(BridgeCapabilities.EventsExecution, BridgeCatalog.EventCapabilities[BridgeEvents.RunCompleted]);
            Assert.All(BridgeEvents.All, name => Assert.True(BridgeCatalog.EventCapabilities.ContainsKey(name)));
        }

        [Fact]
        public void TheServiceClass_HasNoPublicMethodThatTheContractDoesNotName()
        {
            // The connection registers the service by its interface, so nothing else could be called; this keeps the class itself
            // from growing a public method that a registration by class would expose.
            var contract = typeof(IBridgeService).GetMethods().Select(method => method.Name).ToHashSet(StringComparer.Ordinal);

            var extra = typeof(BridgeService)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .Where(name => !contract.Contains(name));

            Assert.Empty(extra);
        }

        [Fact]
        public void ThePipeName_IsThePrefixAndTheSidOfTheUserWhoRunsTheProcess()
        {
            var name = PipeNames.ForCurrentUser();

            Assert.StartsWith(BridgeProtocol.PipeNamePrefix, name, StringComparison.Ordinal);
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            Assert.Equal(BridgeProtocol.PipeNamePrefix + identity.User!.Value, name);
            Assert.StartsWith("S-1-", name[BridgeProtocol.PipeNamePrefix.Length..], StringComparison.Ordinal);
        }
    }
}
