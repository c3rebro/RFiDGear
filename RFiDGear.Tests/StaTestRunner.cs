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
            var tcs = new TaskCompletionSource<object>();

            var thread = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());

                Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        action();
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
