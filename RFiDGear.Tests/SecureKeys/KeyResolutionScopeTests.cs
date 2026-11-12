using RFiDGear.Services.SecureKeys;

using System;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>
    /// Where a command finds the providers it may ask. The scope of a task attempt must reach the command that attempt
    /// awaits, only that command, and must give way to what was there before when the attempt ends.
    /// </summary>
    public class KeyResolutionScopeTests : IDisposable
    {
        public KeyResolutionScopeTests()
        {
            KeyResolutionScope.SetDefault(null);
        }

        public void Dispose()
        {
            KeyResolutionScope.SetDefault(null);
        }

        private static KeyResolutionContext Context(string taskIndex, bool interactive = false) =>
            new KeyResolutionContext(SecureKeyRegistry.Empty, "run", taskIndex, null, interactive);

        [Fact]
        public void Without_a_scope_or_a_default_there_is_no_context()
        {
            Assert.Null(KeyResolutionScope.Current);
        }

        [Fact]
        public void A_scope_is_current_until_it_is_disposed_and_then_what_was_there_before_is_back()
        {
            var outer = Context("outer");
            var inner = Context("inner");

            using (KeyResolutionScope.Open(outer))
            {
                Assert.Same(outer, KeyResolutionScope.Current);

                using (KeyResolutionScope.Open(inner))
                {
                    Assert.Same(inner, KeyResolutionScope.Current);
                }

                Assert.Same(outer, KeyResolutionScope.Current);
            }

            Assert.Null(KeyResolutionScope.Current);
        }

        [Fact]
        public void Disposing_a_scope_twice_does_not_undo_a_scope_opened_since()
        {
            var first = Context("first");
            var second = Context("second");
            var firstScope = KeyResolutionScope.Open(first);

            firstScope.Dispose();
            using (KeyResolutionScope.Open(second))
            {
                firstScope.Dispose();

                Assert.Same(second, KeyResolutionScope.Current);
            }
        }

        [Fact]
        public void The_default_is_used_when_no_scope_is_open_and_a_scope_wins_over_it()
        {
            var fallback = Context("default", interactive: true);
            var scoped = Context("scoped");
            KeyResolutionScope.SetDefault(fallback);

            Assert.Same(fallback, KeyResolutionScope.Current);

            using (KeyResolutionScope.Open(scoped))
            {
                Assert.Same(scoped, KeyResolutionScope.Current);
            }

            Assert.Same(fallback, KeyResolutionScope.Current);
        }

        [Fact]
        public async Task Two_flows_at_the_same_time_each_see_their_own_scope()
        {
            async Task<string> TaskIndexSeenAsync(string taskIndex)
            {
                using (KeyResolutionScope.Open(Context(taskIndex)))
                {
                    await Task.Delay(50);
                    await Task.Yield();

                    return KeyResolutionScope.Current!.TaskIndex;
                }
            }

            var seen = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => TaskIndexSeenAsync("task-" + i))));

            Assert.Equal(Enumerable.Range(0, 8).Select(i => "task-" + i), seen);
        }

        [Fact]
        public async Task A_scope_opened_by_a_method_that_is_awaited_does_not_reach_the_caller()
        {
            async Task OpenAndWaitAsync()
            {
                using (KeyResolutionScope.Open(Context("inside")))
                {
                    await Task.Delay(10);
                    Assert.NotNull(KeyResolutionScope.Current);
                }
            }

            await OpenAndWaitAsync();

            Assert.Null(KeyResolutionScope.Current);
        }

        [Fact]
        public void A_context_needs_a_registry()
        {
            Assert.Throws<ArgumentNullException>(() => new KeyResolutionContext(null!, "run", "0", null, false));
            Assert.Throws<ArgumentNullException>(() => KeyResolutionScope.Open(null!));
        }
    }
}
