#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
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
        private const string ExpectRevisionOption = "--expect-revision=";
        private const string OwnRunsOnlyOption = "--own-runs-only";
        private const string RunOption = "--run=";

        private const string Usage =
            "RFiDGear.Bridge.SampleClient status\n" +
            "RFiDGear.Bridge.SampleClient run <requestId> [<project.rfPrj>] [--expect-revision=<revision>] [$NAME=value ...]\n" +
            "RFiDGear.Bridge.SampleClient result <requestId>\n" +
            "RFiDGear.Bridge.SampleClient results [<afterResultSeq>]\n" +
            "RFiDGear.Bridge.SampleClient auto\n" +
            "RFiDGear.Bridge.SampleClient keys <reference> [--own-runs-only] [--run=<requestId>]";

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
                    "keys" when args.Length >= 2 => await KeysAsync(args, stop.Token),
                    _ => Fail(Usage),
                };
            }
            catch (OperationCanceledException)
            {
                return 1;
            }
            catch (FormatException e)
            {
                return Fail(e.Message);
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
            Console.WriteLine("start-up complete: " + status.StartupComplete + ", running: " + status.IsRunning + ", project: " + status.Project.TaskCount + " tasks"
                + (status.Project.Revision != null ? ", revision " + status.Project.Revision : string.Empty));
            Console.WriteLine(status.UiResponsive ? "main window: answers" : "main window: DOES NOT ANSWER, the project and the reader state may be old (stale: " + status.Stale + ")");
            Console.WriteLine("reader: " + status.Reader.State + (status.Reader.HasCard ? ", card on it" : ", no card") + ", latest result: " + status.LastResultSeq);

            return 0;
        }

        private static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
        {
            var requestId = args[1];
            var rest = args.Skip(2).ToList();

            // The revision that a person noted from `status` earlier, to see a run refused when the project has changed since.
            var revisionArguments = rest.Where(arg => arg.StartsWith(ExpectRevisionOption, StringComparison.Ordinal)).ToList();
            rest = rest.Where(arg => !arg.StartsWith(ExpectRevisionOption, StringComparison.Ordinal)).ToList();

            // A variable is written $NAME=value and nothing else starts with a dollar sign; everything else is the project, whose
            // path may well contain an equals sign.
            var variableArguments = rest.Where(arg => arg.StartsWith('$')).ToList();
            var projects = rest.Where(arg => !arg.StartsWith('$')).ToList();
            var names = variableArguments.Select(arg => arg.Split('=', 2)[0]).ToList();

            if (projects.Count > 1 || revisionArguments.Count > 1 || variableArguments.Any(arg => !arg.Contains('=')) || names.Distinct(StringComparer.Ordinal).Count() != names.Count)
            {
                return Fail(Usage);
            }

            var variables = variableArguments
                .Select(arg => arg.Split('=', 2))
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

            await using var client = await BridgeSampleClient.ConnectAsync(cancellationToken: cancellationToken);
            string? expectedPath = null;
            int? expectedTasks = null;
            string? expectedRevision = revisionArguments.Count == 1 ? revisionArguments[0][ExpectRevisionOption.Length..] : null;

            if (projects.Count == 1)
            {
                var loaded = await client.Service.LoadProject(new LoadProjectRequest { Path = System.IO.Path.GetFullPath(projects[0]) });
                Console.WriteLine("loaded " + loaded.TaskCount + " tasks");

                // The load and the start are two calls. The run says which project it expects, so that a project that is replaced in
                // between is refused instead of run. The revision also catches a task that was changed in the window, which the
                // path and the count do not; it is only there when this RFiDGear offers it.
                expectedPath = loaded.Path;
                expectedTasks = loaded.TaskCount;

                try
                {
                    expectedRevision ??= client.RevisionToExpect(loaded);
                }
                catch (InvalidOperationException e)
                {
                    // Offered but not given: starting anyway would run whatever is loaded.
                    return Fail(e.Message);
                }
            }
            else
            {
                Console.WriteLine("no project given: the run uses whichever project RFiDGear has loaded");
            }

            // Written before the call, so that an operator can ask about it even if this program dies in the middle.
            Console.WriteLine("request id: " + requestId);
            var outcome = await client.RunOnceAsync(requestId, variables, expectedPath, expectedTasks, expectedRevision, cancellationToken);

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
                if (e is AutoModeChangedEvent changed)
                {
                    Console.WriteLine("automatic mode " + (changed.Enabled ? "on" : "off (" + changed.LastStopReason + ")"));
                }
                else if (e is AutoRunCompletedEvent completed)
                {
                    Console.WriteLine("card processed: #" + completed.Result.ResultSeq + " " + completed.Result.Status);
                }
            };

            // The default agreement: the mode ends when this program does.
            await client.Service.StartAutoMode(new StartAutoModeRequest());
            Console.WriteLine("automatic mode is on; Ctrl+C ends it");

            if (await WaitUntilEndedAsync(client, cancellationToken))
            {
                Console.WriteLine("RFiDGear closed the connection; the mode ends with it");

                return 0;
            }

            await client.Service.StopAutoMode(new StopAutoModeRequest());

            return 0;
        }

        // Waits for Ctrl+C or for the end of the connection, whichever comes first.
        private static async Task<bool> WaitUntilEndedAsync(BridgeSampleClient client, CancellationToken cancellationToken)
        {
            var interrupted = Task.Delay(Timeout.Infinite, cancellationToken);
            var finished = await Task.WhenAny(interrupted, client.Completion);

            return finished == client.Completion;
        }

        // Serves one reference with a key that is typed at a prompt that does not show it: a key does not belong in an argument, a
        // file or an environment variable. The key is erased when the command ends.
        // With --own-runs-only the sample asks to be bothered with the runs it started itself, and with --run=<requestId> it starts one
        // right away (on the connection that supplies the keys: a program that does both needs one connection for both, because the
        // bridge serves one client at a time), prints how it ended, and goes.
        private static async Task<int> KeysAsync(string[] args, CancellationToken cancellationToken)
        {
            var reference = args[1];
            var options = args.Skip(2).ToList();
            var ownRunsOnly = options.Remove(OwnRunsOnlyOption);
            var runOptions = options.Where(option => option.StartsWith(RunOption, StringComparison.Ordinal)).ToList();
            options = options.Where(option => !option.StartsWith(RunOption, StringComparison.Ordinal)).ToList();

            if (options.Count > 0 || runOptions.Count > 1 || (runOptions.Count == 1 && runOptions[0].Length == RunOption.Length))
            {
                return Fail(Usage);
            }

            var requestId = runOptions.Count == 1 ? runOptions[0][RunOption.Length..] : null;
            var key = ReadKey();

            try
            {
                using var vault = new SampleVault(reference, key);
                await using var client = await BridgeSampleClient.ConnectAsync(cancellationToken: cancellationToken);
                client.KeyVault = vault.ResolveAsync;
                var registered = await client.RegisterKeyProviderAsync(ownRunsOnly);
                Console.WriteLine("registered as the supplier of keys; name the provider \"" + registered.ProviderId + "\" and the reference \"" + reference + "\" in a task");
                Console.WriteLine("every other reference is refused" + (ownRunsOnly ? ", and so is every run that this program did not start" : string.Empty) + "; Ctrl+C ends it");

                if (requestId != null)
                {
                    // Written before the call, as for `run`.
                    Console.WriteLine("request id: " + requestId);
                    var outcome = await client.RunOnceAsync(requestId, new Dictionary<string, string>(), cancellationToken: cancellationToken);
                    var code = Print(outcome);
                    Console.WriteLine("keys handed out: " + vault.Served);

                    return code;
                }

                var lost = await WaitUntilEndedAsync(client, cancellationToken);
                Console.WriteLine((lost ? "RFiDGear closed the connection; " : string.Empty) + "keys handed out: " + vault.Served);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }

            return 0;
        }

        private static byte[] ReadKey()
        {
            var digits = new char[2 * BridgeProtocol.MaxKeyBytes];
            var count = 0;

            try
            {
                Console.Write("Key as hexadecimal digits (not shown), then Enter: ");

                while (true)
                {
                    var pressed = Console.ReadKey(intercept: true);

                    if (pressed.Key == ConsoleKey.Enter)
                    {
                        break;
                    }

                    if (pressed.Key == ConsoleKey.Backspace)
                    {
                        if (count > 0)
                        {
                            digits[--count] = '\0';
                        }

                        continue;
                    }

                    // Nothing is skipped silently: the prompt shows nothing, so a key that was changed on the way would never be noticed.
                    if (!Uri.IsHexDigit(pressed.KeyChar) || count == digits.Length)
                    {
                        throw new FormatException("A key is at most " + digits.Length + " hexadecimal digits. Start again.");
                    }

                    digits[count++] = pressed.KeyChar;
                }

                Console.WriteLine();

                if (count == 0 || count % 2 != 0)
                {
                    throw new FormatException("A key is an even number of hexadecimal digits.");
                }

                var key = new byte[count / 2];

                for (var i = 0; i < key.Length; i++)
                {
                    key[i] = (byte)((Uri.FromHex(digits[2 * i]) << 4) | Uri.FromHex(digits[(2 * i) + 1]));
                }

                return key;
            }
            finally
            {
                Array.Clear(digits);
            }
        }

        private static int Print(RunOutcome outcome)
        {
            Console.WriteLine(outcome.Kind + ": " + outcome.Detail);

            if (outcome.Result != null)
            {
                foreach (var task in outcome.Result.TaskOutcomes)
                {
                    Console.WriteLine("  task " + task.Position + " " + task.TaskTypeName + " " + (task.IsSuccess switch { true => "ok", false => "FAILED", null => "not run" }) + " " + task.NormalizedErrorCode);

                    // What a check compared, for the operator who has to say why it failed.
                    if (task.VerificationDetails != null)
                    {
                        foreach (var line in VerificationText.Describe(task.VerificationDetails))
                        {
                            Console.WriteLine("    " + line);
                        }
                    }
                }
            }

            // The project asks for a failure that it handles (completedWithIntentionalFailures): that is a successful run.
            var succeeded = outcome.Kind == RunOutcomeKind.Completed && outcome.Result?.Status is RunStatuses.Success or RunStatuses.CompletedWithIntentionalFailures;

            return succeeded ? 0 : 1;
        }
    }
}
