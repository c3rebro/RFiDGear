using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RFiDGear.Tests
{
    internal static class StaTestRunner
    {
        public static Task RunOnStaThreadAsync(Action action)
        {
            return RunOnStaThreadAsync(() =>
            {
                action();
                return Task.CompletedTask;
            });
        }

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
                        tcs.SetResult(null);
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
