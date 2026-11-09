using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Models;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// A reader whose reads the test scripts: what a read returns, the chip it leaves behind, a gate that holds it, or a
    /// failure. It looks at nothing real. Install it with <see cref="ReaderDeviceScope"/>.
    /// </summary>
    internal sealed class ScriptedReadProvider : ElatecNetProvider
    {
        private readonly object _sync = new object();
        private readonly Queue<Step> _steps = new Queue<Step>();
        private readonly List<bool> _chipWasEmptyAtStart = new List<bool>();
        private readonly List<TaskCompletionSource<bool>> _started = new List<TaskCompletionSource<bool>>();
        private int _reads;
        private int _lastReadThreadId;

        /// <summary>Gets or sets whether the reader reports itself as connected.</summary>
        public bool Connected { get; set; } = true;

        /// <inheritdoc />
        public override bool IsConnected => Connected;

        /// <summary>Gets or sets whether the reader behaves like a PC/SC reader (see <see cref="ReaderDevice.IsPcscReader"/>).</summary>
        public bool Pcsc { get; set; }

        /// <inheritdoc />
        public override bool IsPcscReader => Pcsc;

        /// <summary>Gets the managed thread the last read ran on.</summary>
        public int LastReadThreadId => Volatile.Read(ref _lastReadThreadId);

        /// <summary>Gets how many reads were started.</summary>
        public int ReadCount => Volatile.Read(ref _reads);

        /// <summary>Gets, for each read, whether the shared chip was empty (no UID, no children) when the read started.</summary>
        public IReadOnlyList<bool> ChipWasEmptyAtStart
        {
            get
            {
                lock (_sync)
                {
                    return new List<bool>(_chipWasEmptyAtStart);
                }
            }
        }

        /// <summary>The next read that is not scripted returns <c>NoError</c> and leaves the chip as it is.</summary>
        /// <param name="result">What the read returns.</param>
        /// <param name="chip">The chip the read leaves behind; <c>null</c> leaves the chip as it was.</param>
        /// <param name="gate">The read waits for this task before it goes on.</param>
        /// <param name="failure">The read throws this instead of returning.</param>
        public void Script(ERROR result, GenericChipModel? chip = null, Task? gate = null, Exception? failure = null)
        {
            lock (_sync)
            {
                _steps.Enqueue(new Step(result, chip, gate, failure));
            }
        }

        /// <summary>Returns a task that completes when the read with this number (1 is the first) has started.</summary>
        /// <param name="number">The number of the read.</param>
        /// <returns>The task.</returns>
        public Task ReadStarted(int number)
        {
            lock (_sync)
            {
                while (_started.Count < number)
                {
                    _started.Add(new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
                }

                return _started[number - 1].Task;
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> ReadChipPublic()
        {
            Step step;
            int number;
            Volatile.Write(ref _lastReadThreadId, Environment.CurrentManagedThreadId);
            lock (_sync)
            {
                number = Interlocked.Increment(ref _reads);
                _chipWasEmptyAtStart.Add(GenericChip != null && GenericChip.UID == null && GenericChip.Childs == null);
                step = _steps.Count > 0 ? _steps.Dequeue() : new Step(ERROR.NoError, null, null, null);
            }

            _ = ReadStarted(number);
            lock (_sync)
            {
                _started[number - 1].TrySetResult(true);
            }

            if (step.Gate != null)
            {
                await step.Gate.ConfigureAwait(false);
            }

            if (step.Failure != null)
            {
                throw step.Failure;
            }

            if (step.Chip != null)
            {
                GenericChip = step.Chip;
            }

            return step.Result;
        }

        private sealed record Step(ERROR Result, GenericChipModel? Chip, Task? Gate, Exception? Failure);
    }
}
