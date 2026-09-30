#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RFiDGear.HIL.Tests
{
    internal static class StaTestRunner
    {
        /// <summary>
        /// Runs <paramref name="action"/> on a new STA thread with a WPF Dispatcher pump.
        /// Required for any code that touches WPF types, COM objects, or reader SDKs that
        /// rely on STA-thread affinity.
        /// </summary>
        public static Task RunOnStaThreadAsync(Action action)
        {
            return RunOnStaThreadAsync(() =>
            {
                action();
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Runs <paramref name="action"/> on a new STA thread with a WPF Dispatcher pump.
        /// </summary>
        public static Task RunOnStaThreadAsync(Func<Task> action)
        {
            var tcs = new TaskCompletionSource<object>();

            var thread = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());

                Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await action();
                        tcs.SetResult(null!);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                    finally
                    {
                        Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                    }
                });

                Dispatcher.Run();
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            return tcs.Task;
        }
    }
}
