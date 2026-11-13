#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;

namespace RFiDGear.Bridge.SampleClient
{
    /// <summary>
    /// A command line around <see cref="BridgeSampleClient"/>, to try the bridge by hand: RFiDGear has to be running with
    /// external control switched on (the setting, or the start-up argument <c>EXTERNALCONTROL=1</c>).
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class Program
    {
        private const string Usage =
            "RFiDGear.Bridge.SampleClient status\n" +
            "RFiDGear.Bridge.SampleClient run <requestId> [<project.rfPrj>] [$NAME=value ...]\n" +
            "RFiDGear.Bridge.SampleClient result <requestId>\n" +
            "RFiDGear.Bridge.SampleClient results [<afterResultSeq>]\n" +
            "RFiDGear.Bridge.SampleClient auto";

        private static async Task<int> Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine(Usage);

                return 2;
            }

            using var stop = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                stop.Cancel();
            };

            try
            {
                return args[0] switch
                {
                    "status" => await StatusAsync(stop.Token),
                    "run" when args.Length >= 2 => await RunAsync(args, stop.Token),
                    "result" when args.Length == 2 => await ResultAsync(args[1], stop.Token),
                    "results" => await ResultsAsync(args.Length > 1 ? long.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 0, stop.Token),
                    "auto" => await AutoAsync(stop.Token),
                    _ => Fail(Usage),
                };
            }
            catch (OperationCanceledException)
            {
                return 1;
            }
        }

        private static int Fail(string message)
        {
            Console.Error.WriteLine(message);

            return 2;
        }

        private static async Task<int> StatusAsync(CancellationToken cancellationToken)
        {
            await using var client = await BridgeSampleClient.ConnectAsync(cancellationToken: cancellationToken);
            var status = await client.Service.GetStatus(new GetStatusRequest());

            Console.WriteLine("RFiDGear " + client.Hello.AppVersion + ", protocol " + client.Hello.Protocol.Major + "." + client.Hello.Protocol.Minor + ", instance " + client.Hello.InstanceId);
            Console.WriteLine("offers: " + string.Join(", ", client.Hello.Capabilities));
            Console.WriteLine("start-up complete: " + status.StartupComplete + ", running: " + status.IsRunning + ", project: " + status.Project.TaskCount + " tasks");
            Console.WriteLine("reader: " + status.Reader.State + (status.Reader.HasCard ? ", card on it" : ", no card") + ", latest result: " + status.LastResultSeq);

            return 0;
        }

        private static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
        {
            var requestId = args[1];
            var rest = args.Skip(2).ToList();
            var project = rest.FirstOrDefault(arg => !arg.StartsWith('$') && !arg.Contains('='));
            var variables = rest
                .Where(arg => arg.StartsWith('$') && arg.Contains('='))
                .Select(arg => arg.Split('=', 2))
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

            await using var client = await BridgeSampleClient.ConnectAsync(cancellationToken: cancellationToken);

            if (project != null)
            {
                var loaded = await client.Service.LoadProject(new LoadProjectRequest { Path = System.IO.Path.GetFullPath(project) });
                Console.WriteLine("loaded " + loaded.TaskCount + " tasks");
            }

            // Written before the call, so that an operator can ask about it even if this program dies in the middle.
            Console.WriteLine("request id: " + requestId);
            var outcome = await client.RunOnceAsync(requestId, variables, cancellationToken);

            return Print(outcome);
        }

        private static async Task<int> ResultAsync(string requestId, CancellationToken cancellationToken)
        {
            await using var client = await BridgeSampleClient.ConnectAsync(cancellationToken: cancellationToken);

            return Print(await client.ResumeAsync(requestId, cancellationToken));
        }

        private static async Task<int> ResultsAsync(long after, CancellationToken cancellationToken)
        {
            await using var client = await BridgeSampleClient.ConnectAsync(cancellationToken: cancellationToken);
            var list = await client.CatchUpAsync(after, cancellationToken);

            Console.WriteLine("instance " + list.InstanceId + (list.Gap ? ", SOME RESULTS WERE DROPPED FROM THE HISTORY" : string.Empty));

            foreach (var result in list.Results)
            {
                Console.WriteLine("#" + result.ResultSeq + " " + result.Source + " " + result.Status + " " + result.FailedTasks + "/" + result.TotalTasks + " failed, run " + result.RunId);
            }

            Console.WriteLine("latest: " + list.LastResultSeq);

            return 0;
        }

        private static async Task<int> AutoAsync(CancellationToken cancellationToken)
        {
            var events = new[] { BridgeEvents.AutoModeChanged, BridgeEvents.AutoRunCompleted };
            await using var client = await BridgeSampleClient.ConnectAsync(events: events, cancellationToken: cancellationToken);
            client.EventReceived += e =>
            {
                switch (e)
                {
                    case AutoModeChangedEvent changed:
                        Console.WriteLine("automatic mode " + (changed.Enabled ? "on" : "off (" + changed.LastStopReason + ")"));
                        break;
                    case AutoRunCompletedEvent completed:
                        Console.WriteLine("card processed: #" + completed.Result.ResultSeq + " " + completed.Result.Status);
                        break;
                }
            };

            // The default agreement: the mode ends when this program does.
            await client.Service.StartAutoMode(new StartAutoModeRequest());
            Console.WriteLine("automatic mode is on; Ctrl+C ends it");

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await client.Service.StopAutoMode(new StopAutoModeRequest());
            }

            return 0;
        }

        private static int Print(RunOutcome outcome)
        {
            Console.WriteLine(outcome.Kind + ": " + outcome.Detail);

            if (outcome.Result != null)
            {
                foreach (var task in outcome.Result.TaskOutcomes)
                {
                    Console.WriteLine("  task " + task.Position + " " + task.TaskTypeName + " " + (task.IsSuccess switch { true => "ok", false => "FAILED", null => "not run" }) + " " + task.NormalizedErrorCode);
                }
            }

            return outcome.Kind == RunOutcomeKind.Completed && outcome.Result?.Status == RunStatuses.Success ? 0 : 1;
        }
    }
}
