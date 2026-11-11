using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Models;
using RFiDGear.Services.TaskExecution;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// Regression tests for the TransportError sentinel fix in TaskExecutionService.
    /// Uses TaskDescriptor-based requests (executor lambdas) to exercise RunTaskLoopAsync
    /// without requiring WPF ViewModels.
    /// </summary>
    public class TaskExecutionServiceTests
    {
        private static TaskExecutionService BuildService(ITaskExecutionLogger logger = null)
        {
            return new TaskExecutionService(
                new StubReaderDeviceProvider(),
                new StubDispatcherTimerAdapter(),
                new StubDispatcherTimerAdapter(),
                logger);
        }

        private static TaskExecutionRequest BuildRequest(IReadOnlyList<TaskDescriptor> descriptors)
        {
            return new TaskExecutionRequest
            {
                TaskDescriptors = descriptors,
                Timeouts = TaskExecutionTimeouts.Default
            };
        }

        [Fact]
        public async Task TransportError_TaskIsInvokedOnceAndLoopCompletes()
        {
            var invokeCount = 0;
            var task = new StubTaskModel { CurrentTaskIndex = "0" };

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, task, ct =>
                {
                    invokeCount++;
                    task.CurrentTaskErrorLevel = ERROR.TransportError;
                    return Task.CompletedTask;
                })
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await BuildService().ExecuteOnceAsync(BuildRequest(descriptors));
            });

            Assert.Equal(1, invokeCount);
        }

        [Fact]
        public async Task TransportError_IsNotInvokedSecondTime()
        {
            var invokeCount = 0;
            var task = new StubTaskModel { CurrentTaskIndex = "0" };

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, task, ct =>
                {
                    invokeCount++;
                    task.CurrentTaskErrorLevel = ERROR.TransportError;
                    return Task.CompletedTask;
                })
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await BuildService().ExecuteOnceAsync(BuildRequest(descriptors));
            });

            Assert.Equal(1, invokeCount);
        }

        [Fact]
        public async Task NoError_LoopAdvancesNormally()
        {
            var invokeCount = 0;
            var task = new StubTaskModel { CurrentTaskIndex = "0" };

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, task, ct =>
                {
                    invokeCount++;
                    task.CurrentTaskErrorLevel = ERROR.NoError;
                    return Task.CompletedTask;
                })
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await BuildService().ExecuteOnceAsync(BuildRequest(descriptors));
            });

            Assert.Equal(1, invokeCount);
        }

        [Fact]
        public async Task TransportError_AttemptIsRecorded()
        {
            var task = new StubTaskModel { CurrentTaskIndex = "0" };

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, task, ct =>
                {
                    task.CurrentTaskErrorLevel = ERROR.TransportError;
                    return Task.CompletedTask;
                })
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await BuildService().ExecuteOnceAsync(BuildRequest(descriptors));
            });

            Assert.Single(task.AttemptResults);
            Assert.Equal(ERROR.TransportError, task.AttemptResults[0].ErrorLevel);
        }

        [Fact]
        public async Task MultipleTasksWithTransportError_AllAdvance()
        {
            var invokeCounts = new int[3];
            var tasks = new[]
            {
                new StubTaskModel { CurrentTaskIndex = "0" },
                new StubTaskModel { CurrentTaskIndex = "1" },
                new StubTaskModel { CurrentTaskIndex = "2" }
            };

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, tasks[0], ct => { invokeCounts[0]++; tasks[0].CurrentTaskErrorLevel = ERROR.TransportError; return Task.CompletedTask; }),
                new TaskDescriptor(1, tasks[1], ct => { invokeCounts[1]++; tasks[1].CurrentTaskErrorLevel = ERROR.TransportError; return Task.CompletedTask; }),
                new TaskDescriptor(2, tasks[2], ct => { invokeCounts[2]++; tasks[2].CurrentTaskErrorLevel = ERROR.NoError; return Task.CompletedTask; })
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await BuildService().ExecuteOnceAsync(BuildRequest(descriptors));
            });

            Assert.Equal(1, invokeCounts[0]);
            Assert.Equal(1, invokeCounts[1]);
            Assert.Equal(1, invokeCounts[2]);
        }

        [Fact]
        public void ExecutionState_DefaultsToNotStarted()
        {
            var task = new StubTaskModel();
            Assert.Equal(TaskExecutionState.NotStarted, task.ExecutionState);
        }


        [Fact]
        public async Task StructuredLogs_EmitCorrelatedPerTaskOutcomesAndSummary()
        {
            var logger = new CapturingTaskExecutionLogger();
            TaskExecutionResult executionResult = null;
            var first = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "10",
                SelectedTaskType = "ReadData",
                SelectedTaskDescription = "Read verification data"
            };
            var second = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "11",
                SelectedTaskType = "WriteData",
                SelectedTaskDescription = "Write personalization data"
            };

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, first, ct =>
                {
                    first.CurrentTaskErrorLevel = ERROR.NoError;
                    first.IsTaskCompletedSuccessfully = true;
                    return Task.CompletedTask;
                }),
                new TaskDescriptor(1, second, ct =>
                {
                    second.CurrentTaskErrorLevel = ERROR.TransportError;
                    second.IsTaskCompletedSuccessfully = false;
                    return Task.CompletedTask;
                })
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                executionResult = await BuildService(logger).ExecuteOnceAsync(BuildRequest(descriptors));
            });

            var outcomes = logger.Entries.Where(entry => entry.Stage == "Task.Outcome").ToList();
            Assert.Equal(2, outcomes.Count);

            var runIds = logger.Entries
                .Where(entry => entry.Stage.StartsWith("TaskLoop", StringComparison.Ordinal) || entry.Stage == "Task.Outcome")
                .Select(entry => GetStringProperty(entry.DetailsJson, "RunId"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct()
                .ToList();

            Assert.Single(runIds);
            Assert.Equal("10", GetStringProperty(outcomes[0].DetailsJson, "TaskId"));
            Assert.Equal("ReadData", GetStringProperty(outcomes[0].DetailsJson, "TaskType"));
            Assert.Equal("Executed", GetStringProperty(outcomes[0].DetailsJson, "Decision"));
            Assert.Equal("Successful", GetStringProperty(outcomes[0].DetailsJson, "Outcome"));
            Assert.Equal("11", GetStringProperty(outcomes[1].DetailsJson, "TaskId"));
            Assert.Equal("Failed", GetStringProperty(outcomes[1].DetailsJson, "Outcome"));

            var summary = Assert.Single(logger.Entries.Where(entry => entry.Stage == "TaskLoop.Summary"));
            Assert.Equal(2, GetIntProperty(summary.DetailsJson, "Executed"));
            Assert.Equal(0, GetIntProperty(summary.DetailsJson, "Skipped"));
            Assert.Equal(1, GetIntProperty(summary.DetailsJson, "Failed"));
            Assert.Equal(1, GetIntProperty(summary.DetailsJson, "Successful"));
            Assert.Equal(0, GetIntProperty(summary.DetailsJson, "AcceptedFailures"));
            Assert.Equal(1, GetIntProperty(summary.DetailsJson, "UnhandledFailures"));
            Assert.Equal("Failure", GetStringProperty(summary.DetailsJson, "TerminalStatus"));

            Assert.NotNull(executionResult);
            Assert.Equal(TaskLoopTerminalStatus.Failure, executionResult.TerminalStatus);
            Assert.Equal(runIds[0], executionResult.RunId);
            Assert.Single(logger.Entries.Where(entry => entry.Stage == "TaskLoop.Failure"));
            Assert.Empty(logger.Entries.Where(entry => entry.Stage == "TaskLoop.Success"));
        }

        [Fact]
        public async Task StructuredLogs_RedactSecretsAndDoNotSerializeTaskKeyProperties()
        {
            const string secret = "00112233445566778899AABBCCDDEEFF";
            var logger = new CapturingTaskExecutionLogger();
            var task = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "20",
                SelectedTaskType = "WriteData",
                SelectedTaskDescription = "Key=" + secret + " payload=" + secret,
                DesfireAppKeyCurrent = secret
            };

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, task, ct =>
                {
                    task.CurrentTaskErrorLevel = ERROR.NoError;
                    task.IsTaskCompletedSuccessfully = true;
                    return Task.CompletedTask;
                })
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await BuildService(logger).ExecuteOnceAsync(BuildRequest(descriptors));
            });

            var outcome = Assert.Single(logger.Entries.Where(entry => entry.Stage == "Task.Outcome"));
            Assert.DoesNotContain(secret, outcome.DetailsJson);
            Assert.DoesNotContain(nameof(LoggingStubTaskModel.DesfireAppKeyCurrent), outcome.DetailsJson);
            Assert.Contains("REDACTED", outcome.DetailsJson);
        }

        [Fact]
        public async Task StructuredLogs_CorrelateKeyFreeFileSettingsComparisonWithRun()
        {
            var logger = new CapturingTaskExecutionLogger();
            var task = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "21",
                SelectedTaskType = "CheckFileSettings",
                FileNumberCurrentAsInt = 2,
                FileSizeCurrentAsInt = 16
            };

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, task, ct =>
                {
                    // What the check finds while the task runs.
                    task.Details = new DesfireFileSettingsComparison
                    {
                        ApplicationId = 1,
                        FileNumber = 2,
                        ExpectedFileType = 0,
                        ActualFileType = 0,
                        ExpectedFileSize = 16,
                        ActualFileSize = 16,
                        ExpectedCommunicationMode = 3,
                        ActualCommunicationMode = 3,
                        ExpectedAccessRights0 = 0x10,
                        ActualAccessRights0 = 0x10,
                        ExpectedAccessRights1 = 0x32,
                        ActualAccessRights1 = 0x32,
                        Matches = true
                    }.ToLogValues();
                    task.CurrentTaskErrorLevel = ERROR.NoError;
                    task.IsTaskCompletedSuccessfully = true;
                    return Task.CompletedTask;
                })
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await BuildService(logger).ExecuteOnceAsync(BuildRequest(descriptors));
            });

            var outcome = Assert.Single(logger.Entries.Where(entry => entry.Stage == "Task.Outcome"));
            using var document = JsonDocument.Parse(outcome.DetailsJson);
            var comparison = document.RootElement
                .GetProperty("IO")
                .GetProperty("FileSettingsComparison");

            Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("RunId").GetString()));
            Assert.True(comparison.GetProperty("Matches").GetBoolean());
            Assert.Equal(0x32, comparison.GetProperty("ActualAccessRights1").GetInt32());
            Assert.Equal(16, comparison.GetProperty("ActualFileSize").GetInt64());
            Assert.DoesNotContain("Key", comparison.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Payload", comparison.GetRawText(), StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void IoMetadata_AttachesTheDetailsOfATaskOnlyWhenItWasExecuted(bool taskExecuted, bool expectDetails)
        {
            // A skipped task has nothing from this run to report.
            var task = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "20",
                Details = new Dictionary<string, object> { ["Matches"] = true }
            };

            var metadata = TaskExecutionService.GetSafeIoMetadata(task, taskExecuted) as IDictionary<string, object>;

            Assert.Equal(expectDetails, metadata != null && metadata.ContainsKey("FileSettingsComparison"));
        }

        [Fact]
        public void IoMetadata_KeepsOnlyNumbersAndFlagsOfWhatATaskReports()
        {
            var task = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "20",
                Details = new Dictionary<string, object>
                {
                    ["Number"] = 7,
                    ["Large"] = 4294967295L,
                    ["Flag"] = true,
                    ["Text"] = "00112233445566778899AABBCCDDEEFF",
                    ["Object"] = new { Secret = "x" },
                    ["List"] = new[] { 1, 2 },
                    ["Nothing"] = null!
                }
            };

            var metadata = TaskExecutionService.GetSafeIoMetadata(task, taskExecuted: true) as IDictionary<string, object>;
            Assert.NotNull(metadata);
            var reported = (IDictionary<string, object>)metadata["FileSettingsComparison"];

            Assert.Equal(new[] { "Flag", "Large", "Number" }, reported.Keys.OrderBy(name => name));
        }

        [Fact]
        public async Task TheDetailsOfAnEarlierRun_AreClearedBeforeTheNextRunStarts()
        {
            var task = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "5",
                SelectedTaskType = "CheckFileSettings",
                Details = new Dictionary<string, object> { ["Matches"] = true }
            };
            var seenAtStart = new List<bool>();
            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, task, ct =>
                {
                    seenAtStart.Add(task.Details != null);
                    task.Calls.Add("execute");
                    task.Details = new Dictionary<string, object> { ["Matches"] = false };
                    task.CurrentTaskErrorLevel = ERROR.NoError;
                    task.IsTaskCompletedSuccessfully = true;
                    return Task.CompletedTask;
                })
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await BuildService().ExecuteOnceAsync(BuildRequest(descriptors));
                await BuildService().ExecuteOnceAsync(BuildRequest(descriptors));
            });

            // What was left over from before the first run, and what the first run found, is gone when the next one starts.
            Assert.Equal(new[] { "clear", "execute", "clear", "execute" }, task.Calls);
            Assert.Equal(new[] { false, false }, seenAtStart);
        }

        [Fact]
        public async Task TheDetailsOfEveryTaskOfTheList_AreClearedWhenARunStarts()
        {
            // The second task is in the list of the project only: the run has no descriptor for it, so only the loop over
            // the list can clear it (the loop over the descriptors is the one the test above needs).
            var first = new LoggingStubTaskModel { CurrentTaskIndex = "0", Details = new Dictionary<string, object> { ["Matches"] = true } };
            var second = new LoggingStubTaskModel { CurrentTaskIndex = "1", Details = new Dictionary<string, object> { ["Matches"] = true } };
            var handler = new ChipTaskHandlerModel();
            handler.TaskCollection.Add(first);
            handler.TaskCollection.Add(second);
            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, first, ct => Task.CompletedTask)
            };
            var request = BuildRequest(descriptors);
            request.TaskHandler = handler;
            request.RunSelectedOnly = true;
            request.SelectedSetupViewModel = first;

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await BuildService().ExecuteOnceAsync(request);
            });

            // Also the task that was not selected: its old result is not shown with the new run.
            Assert.Null(first.Details);
            Assert.Null(second.Details);
        }

        [Fact]
        public async Task StructuredLogs_RecordSanitizedTaskExceptionAndFailureSummary()
        {
            const string secret = "FFEEDDCCBBAA99887766554433221100";
            var logger = new CapturingTaskExecutionLogger();
            var task = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "30",
                SelectedTaskType = "AuthenticateApplication",
                SelectedTaskDescription = "Authentication check"
            };

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, task, ct => throw new InvalidOperationException("Key=" + secret))
            };

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await StaTestRunner.RunOnStaThreadAsync(async () =>
                {
                    await BuildService(logger).ExecuteOnceAsync(BuildRequest(descriptors));
                });
            });

            var outcome = Assert.Single(logger.Entries.Where(entry => entry.Stage == "Task.Outcome"));
            Assert.DoesNotContain(secret, outcome.DetailsJson);
            Assert.Equal("InvalidOperationException", GetStringProperty(outcome.DetailsJson, "ExceptionType"));
            Assert.Contains("REDACTED", GetStringProperty(outcome.DetailsJson, "ExceptionMessage"));

            var summary = Assert.Single(logger.Entries.Where(entry => entry.Stage == "TaskLoop.Summary"));
            Assert.Equal(1, GetIntProperty(summary.DetailsJson, "Executed"));
            Assert.Equal(1, GetIntProperty(summary.DetailsJson, "Failed"));
        }

        [Fact]
        public async Task StructuredLogs_EmitIntentionalFailuresWhenNegativeOutcomeIsConsumedByExecutedBranch()
        {
            var logger = new CapturingTaskExecutionLogger();
            TaskExecutionResult executionResult = null;
            var applicationCheck = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "10",
                SelectedTaskType = "AppExistCheck",
                SelectedTaskDescription = "Check whether application is absent"
            };
            var createApplication = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "20",
                SelectedTaskType = "CreateApplication",
                SelectedTaskDescription = "Create missing application",
                SelectedExecuteConditionTaskIndex = "10",
                SelectedExecuteConditionErrorLevel = ERROR.IsNotTrue
            };

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, applicationCheck, ct =>
                {
                    applicationCheck.CurrentTaskErrorLevel = ERROR.IsNotTrue;
                    applicationCheck.IsTaskCompletedSuccessfully = false;
                    return Task.CompletedTask;
                }),
                new TaskDescriptor(1, createApplication, ct =>
                {
                    createApplication.CurrentTaskErrorLevel = ERROR.NoError;
                    createApplication.IsTaskCompletedSuccessfully = true;
                    return Task.CompletedTask;
                })
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                executionResult = await BuildService(logger).ExecuteOnceAsync(BuildRequest(descriptors));
            });

            var summary = Assert.Single(logger.Entries.Where(entry => entry.Stage == "TaskLoop.Summary"));
            Assert.Equal(1, GetIntProperty(summary.DetailsJson, "Failed"));
            Assert.Equal(1, GetIntProperty(summary.DetailsJson, "AcceptedFailures"));
            Assert.Equal(0, GetIntProperty(summary.DetailsJson, "UnhandledFailures"));
            Assert.Equal("CompletedWithIntentionalFailures", GetStringProperty(summary.DetailsJson, "TerminalStatus"));

            Assert.NotNull(executionResult);
            Assert.Equal(TaskLoopTerminalStatus.CompletedWithIntentionalFailures, executionResult.TerminalStatus);
            Assert.Single(logger.Entries.Where(entry => entry.Stage == "TaskLoop.CompletedWithIntentionalFailures"));
            Assert.Empty(logger.Entries.Where(entry => entry.Stage == "TaskLoop.Failure"));
        }

        [Fact]
        public void TerminalEvaluator_FailsUnhandledWriteErrorWithConditionalFollowUpSkip()
        {
            var writeTask = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "50",
                CurrentTaskErrorLevel = ERROR.Unknown
            };
            var readTask = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "60",
                SelectedExecuteConditionTaskIndex = "50",
                SelectedExecuteConditionErrorLevel = ERROR.NoError
            };
            var observations = new List<TaskExecutionObservation>
            {
                new TaskExecutionObservation(0, "50", writeTask, "Executed", false, ERROR.Unknown),
                new TaskExecutionObservation(1, "60", readTask, "Skipped", null, ERROR.Empty, "ExecuteConditionNotMet")
            };

            var result = TaskLoopTerminalEvaluator.Evaluate(2, observations);

            Assert.Equal(TaskLoopTerminalStatus.Failure, result.TerminalStatus);
            Assert.Equal(1, result.UnhandledFailures);
            Assert.Equal(1, result.Skipped);
            Assert.Equal(0, result.InvalidSkips);
        }

        [Fact]
        public void TerminalEvaluator_FailsNonConditionalSkipWithoutTaskFailure()
        {
            var task = new LoggingStubTaskModel { CurrentTaskIndex = "10" };
            var observations = new List<TaskExecutionObservation>
            {
                new TaskExecutionObservation(0, "10", task, "Skipped", null, ERROR.Empty, "NotExecuted")
            };

            var result = TaskLoopTerminalEvaluator.Evaluate(1, observations);

            Assert.Equal(TaskLoopTerminalStatus.Failure, result.TerminalStatus);
            Assert.Equal(1, result.InvalidSkips);
        }

        [Fact]
        public void TerminalEvaluator_AcceptsNotTakenConditionalBranch()
        {
            var checkTask = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "10",
                CurrentTaskErrorLevel = ERROR.NoError
            };
            var createTask = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "20",
                SelectedExecuteConditionTaskIndex = "10",
                SelectedExecuteConditionErrorLevel = ERROR.IsNotTrue
            };
            var observations = new List<TaskExecutionObservation>
            {
                new TaskExecutionObservation(0, "10", checkTask, "Executed", true, ERROR.NoError),
                new TaskExecutionObservation(1, "20", createTask, "Skipped", null, ERROR.Empty, "ExecuteConditionNotMet")
            };

            var result = TaskLoopTerminalEvaluator.Evaluate(2, observations);

            Assert.Equal(TaskLoopTerminalStatus.Success, result.TerminalStatus);
            Assert.Equal(0, result.InvalidSkips);
        }

        [Fact]
        public void TerminalEvaluator_AcceptsFailureHandledByExplicitErrorRoute()
        {
            var failedTask = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "10",
                CurrentTaskErrorLevel = ERROR.TransportError
            };
            var recoveryTask = new LoggingStubTaskModel
            {
                CurrentTaskIndex = "90",
                CurrentTaskErrorLevel = ERROR.NoError
            };
            var observations = new List<TaskExecutionObservation>
            {
                new TaskExecutionObservation(0, "10", failedTask, "Executed", false, ERROR.TransportError),
                new TaskExecutionObservation(1, "90", recoveryTask, "Executed", true, ERROR.NoError)
            };
            var errorRouting = new Dictionary<ERROR, string>
            {
                [ERROR.TransportError] = "90"
            };

            var result = TaskLoopTerminalEvaluator.Evaluate(2, observations, errorRouting);

            Assert.Equal(TaskLoopTerminalStatus.CompletedWithIntentionalFailures, result.TerminalStatus);
            Assert.Equal(1, result.AcceptedFailures);
            Assert.Equal(0, result.UnhandledFailures);
        }

        // ── tasks the loop never got to ──

        private static TaskExecutionObservation ExecutedOk(int position, string id) =>
            new TaskExecutionObservation(
                position, id, new LoggingStubTaskModel { CurrentTaskIndex = id, CurrentTaskErrorLevel = ERROR.NoError },
                "Executed", true, ERROR.NoError);

        // ── the answer "no" of a check is not a failure ──

        private static TaskExecutionObservation FailedTask(int position, string id, string type, ERROR error) =>
            new TaskExecutionObservation(
                position, id, new LoggingStubTaskModel { CurrentTaskIndex = id, CurrentTaskErrorLevel = error, SelectedTaskType = type },
                "Executed", false, error);

        [Theory]
        [InlineData(ERROR.IsNotTrue)]
        [InlineData(ERROR.IsNotFalse)]
        public void TerminalEvaluator_AcceptsTheNoOfACheck_WithoutAHandler(ERROR answer)
        {
            // A check that says "no" is its result; nothing has to be conditioned on it.
            var observations = new List<TaskExecutionObservation> { FailedTask(0, "10", "ChipIsOfType", answer), ExecutedOk(1, "11") };

            var result = TaskLoopTerminalEvaluator.Evaluate(2, observations);

            Assert.Equal(TaskLoopTerminalStatus.CompletedWithIntentionalFailures, result.TerminalStatus);
            Assert.Equal(1, result.AcceptedFailures);
            Assert.Equal(0, result.UnhandledFailures);
        }

        [Theory]
        [InlineData(ERROR.IsNotTrue)]
        [InlineData(ERROR.IsNotFalse)]
        public void TerminalEvaluator_FailsAProgramThatDidNotStart_EvenWithTheCodeOfACheck(ERROR answer)
        {
            // A missing batch file reports IsNotTrue as well, and that is a failed run.
            var observations = new List<TaskExecutionObservation> { FailedTask(0, "10", "ExecuteProgram", answer), ExecutedOk(1, "11") };

            var result = TaskLoopTerminalEvaluator.Evaluate(2, observations);

            Assert.Equal(TaskLoopTerminalStatus.Failure, result.TerminalStatus);
            Assert.Equal(0, result.AcceptedFailures);
            Assert.Equal(1, result.UnhandledFailures);
        }

        [Fact]
        public void TerminalEvaluator_StillFailsACheckThatFailedForARealReason()
        {
            var observations = new List<TaskExecutionObservation> { FailedTask(0, "10", "ChipIsOfType", ERROR.TransportError), ExecutedOk(1, "11") };

            var result = TaskLoopTerminalEvaluator.Evaluate(2, observations);

            Assert.Equal(TaskLoopTerminalStatus.Failure, result.TerminalStatus);
            Assert.Equal(1, result.UnhandledFailures);
        }

        [Fact]
        public void TerminalEvaluator_FailsAnAnswerOfNo_WhenTheRunDidNotGetToItsEnd()
        {
            // The answer is fine; the run that stopped after it is not.
            var observations = new List<TaskExecutionObservation> { FailedTask(0, "10", "ChipIsOfType", ERROR.IsNotTrue) };

            var result = TaskLoopTerminalEvaluator.Evaluate(3, observations);

            Assert.Equal(TaskLoopTerminalStatus.Failure, result.TerminalStatus);
            Assert.Equal(2, result.NotReached);
        }

        [Fact]
        public void TerminalEvaluator_FailsWhenTasksRemainAfterTheLastReachedTask()
        {
            // The task that ran was fine. The run still must not read as a success: two tasks never started.
            var observations = new List<TaskExecutionObservation> { ExecutedOk(0, "10") };

            var result = TaskLoopTerminalEvaluator.Evaluate(3, observations);

            Assert.Equal(TaskLoopTerminalStatus.Failure, result.TerminalStatus);
            Assert.Equal(2, result.NotReached);
            Assert.Equal(0, result.Failed);
            Assert.Equal(0, result.UnhandledFailures);
            Assert.Equal(0, result.Unknown);
            Assert.Equal(0, result.InvalidSkips);
        }

        [Fact]
        public void TerminalEvaluator_FailsWhenOnlyTheLastTaskIsMissing()
        {
            var observations = new List<TaskExecutionObservation> { ExecutedOk(0, "10"), ExecutedOk(1, "11") };

            var result = TaskLoopTerminalEvaluator.Evaluate(3, observations);

            Assert.Equal(TaskLoopTerminalStatus.Failure, result.TerminalStatus);
            Assert.Equal(1, result.NotReached);
        }

        [Fact]
        public void TerminalEvaluator_CountsEveryTaskAsNotReachedWhenNothingWasObserved()
        {
            var result = TaskLoopTerminalEvaluator.Evaluate(4, new List<TaskExecutionObservation>());

            Assert.Equal(TaskLoopTerminalStatus.Failure, result.TerminalStatus);
            Assert.Equal(4, result.NotReached);
        }

        [Fact]
        public void TerminalEvaluator_SucceedsWhenEveryTaskWasReached()
        {
            var observations = new List<TaskExecutionObservation> { ExecutedOk(0, "10"), ExecutedOk(1, "11"), ExecutedOk(2, "12") };

            var result = TaskLoopTerminalEvaluator.Evaluate(3, observations);

            Assert.Equal(TaskLoopTerminalStatus.Success, result.TerminalStatus);
            Assert.Equal(0, result.NotReached);
        }

        [Fact]
        public void TerminalEvaluator_IgnoresTasksThatErrorRoutingJumpedOver()
        {
            // Task 0 fails with AuthFailure and the route sends the run to "90" (position 3), skipping 1 and 2 on
            // purpose. Positions 1 and 2 lie before a task the run did reach, so they are not "left behind".
            var failedTask = new LoggingStubTaskModel { CurrentTaskIndex = "10", CurrentTaskErrorLevel = ERROR.AuthFailure };
            var recovery = new LoggingStubTaskModel { CurrentTaskIndex = "90", CurrentTaskErrorLevel = ERROR.NoError };
            var observations = new List<TaskExecutionObservation>
            {
                new TaskExecutionObservation(0, "10", failedTask, "Executed", false, ERROR.AuthFailure),
                new TaskExecutionObservation(3, "90", recovery, "Executed", true, ERROR.NoError)
            };
            var errorRouting = new Dictionary<ERROR, string> { [ERROR.AuthFailure] = "90" };

            var result = TaskLoopTerminalEvaluator.Evaluate(4, observations, errorRouting);

            Assert.Equal(TaskLoopTerminalStatus.CompletedWithIntentionalFailures, result.TerminalStatus);
            Assert.Equal(0, result.NotReached);
        }

        [Fact]
        public void TerminalEvaluator_FailsWhenTasksRemainAfterARoutingTarget()
        {
            // Same jump, but the run then stopped before the last task: the tail is still left behind.
            var failedTask = new LoggingStubTaskModel { CurrentTaskIndex = "10", CurrentTaskErrorLevel = ERROR.AuthFailure };
            var recovery = new LoggingStubTaskModel { CurrentTaskIndex = "90", CurrentTaskErrorLevel = ERROR.NoError };
            var observations = new List<TaskExecutionObservation>
            {
                new TaskExecutionObservation(0, "10", failedTask, "Executed", false, ERROR.AuthFailure),
                new TaskExecutionObservation(2, "90", recovery, "Executed", true, ERROR.NoError)
            };
            var errorRouting = new Dictionary<ERROR, string> { [ERROR.AuthFailure] = "90" };

            var result = TaskLoopTerminalEvaluator.Evaluate(5, observations, errorRouting);

            Assert.Equal(TaskLoopTerminalStatus.Failure, result.TerminalStatus);
            Assert.Equal(2, result.NotReached);
        }

        [Fact]
        public void TerminalEvaluator_SelectedTaskOnly_DoesNotExpectTheOtherTasks()
        {
            var observations = new List<TaskExecutionObservation> { ExecutedOk(2, "12") };

            var result = TaskLoopTerminalEvaluator.Evaluate(5, observations, selectedTaskOnly: true);

            Assert.Equal(TaskLoopTerminalStatus.Success, result.TerminalStatus);
            Assert.Equal(0, result.NotReached);
        }

        [Fact]
        public void TerminalEvaluator_SelectedTaskOnly_StillFailsWhenNothingRan()
        {
            var result = TaskLoopTerminalEvaluator.Evaluate(5, new List<TaskExecutionObservation>(), selectedTaskOnly: true);

            Assert.Equal(TaskLoopTerminalStatus.Failure, result.TerminalStatus);
        }

        [Fact]
        public async Task ErrorRoutingJumpingOverATask_InARealRun_DoesNotMakeItAFailure()
        {
            var failing = new StubTaskModel { CurrentTaskIndex = "10" };
            var jumpedOver = new StubTaskModel { CurrentTaskIndex = "20" };
            var recovery = new StubTaskModel { CurrentTaskIndex = "90" };
            var jumpedOverRan = 0;
            var observations = new List<TaskExecutionObservation>();
            var logger = new CapturingTaskExecutionLogger();

            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, failing, ct =>
                {
                    failing.CurrentTaskErrorLevel = ERROR.AuthFailure;
                    failing.IsTaskCompletedSuccessfully = false;
                    return Task.CompletedTask;
                }),
                new TaskDescriptor(1, jumpedOver, ct => { jumpedOverRan++; return Task.CompletedTask; }),
                new TaskDescriptor(2, recovery, ct =>
                {
                    recovery.CurrentTaskErrorLevel = ERROR.NoError;
                    recovery.IsTaskCompletedSuccessfully = true;
                    return Task.CompletedTask;
                })
            };
            var request = BuildRequest(descriptors);
            request.ErrorRouting = new Dictionary<ERROR, string> { [ERROR.AuthFailure] = "90" };
            request.Observations = observations;

            TaskExecutionResult? result = null;
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                result = await BuildService(logger).ExecuteOnceAsync(request);
            });

            Assert.NotNull(result);
            Assert.Equal(0, jumpedOverRan);
            Assert.Equal(new[] { 0, 2 }, observations.Select(o => o.TaskPosition));
            Assert.Equal(TaskLoopTerminalStatus.CompletedWithIntentionalFailures, result.TerminalStatus);
            var summary = Assert.Single(logger.Entries.Where(entry => entry.Stage == "TaskLoop.Summary"));
            Assert.Equal(0, GetIntProperty(summary.DetailsJson, "NotReached"));
        }

        [Fact]
        public async Task ARunThatEndsBeforeItsLastTask_InARealRun_ReturnsFailureAndLogsTheUnreachedTail()
        {
            // The Release watchdog that ends a run early is compiled out of this build, so an error-routing target
            // outside the task list stands in for it: the loop leaves the list after the first task and never
            // returns to the other two. Every task that ran succeeded, so only the unreached tail can fail the run.
            var first = new StubTaskModel { CurrentTaskIndex = "10" };
            var second = new StubTaskModel { CurrentTaskIndex = "11" };
            var outsideTheList = new StubTaskModel { CurrentTaskIndex = "90" };
            var logger = new CapturingTaskExecutionLogger();
            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, first, ct =>
                {
                    first.CurrentTaskErrorLevel = ERROR.ProtocolConstraint;   // routed
                    first.IsTaskCompletedSuccessfully = true;
                    return Task.CompletedTask;
                }),
                new TaskDescriptor(1, second, ct => Task.CompletedTask),
                new TaskDescriptor(7, outsideTheList, ct => Task.CompletedTask), // Index 7 is past the end of the list
            };
            var request = BuildRequest(descriptors);
            request.ErrorRouting = new Dictionary<ERROR, string> { [ERROR.ProtocolConstraint] = "90" };

            TaskExecutionResult? result = null;
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                result = await BuildService(logger).ExecuteOnceAsync(request);
            });

            Assert.NotNull(result);
            Assert.Equal(TaskLoopTerminalStatus.Failure, result.TerminalStatus);
            var summary = Assert.Single(logger.Entries.Where(entry => entry.Stage == "TaskLoop.Summary"));
            Assert.Equal(2, GetIntProperty(summary.DetailsJson, "NotReached"));
            Assert.Equal(0, GetIntProperty(summary.DetailsJson, "UnhandledFailures"));
        }

        [Fact]
        public async Task ARunTheWatchdogGaveUp_EndsAsFailureInsteadOfFaulting()
        {
            // The watchdog fires while the second task is still busy; the task then returns. The loop must stop
            // cleanly: no fault, the stalled task recorded as failed, the third task never started.
            var logger = new CapturingTaskExecutionLogger();
            var observations = new List<TaskExecutionObservation>();
            var service = BuildService(logger);
            var tasks = new[]
            {
                new StubTaskModel { CurrentTaskIndex = "10" },
                new StubTaskModel { CurrentTaskIndex = "11" },
                new StubTaskModel { CurrentTaskIndex = "12" }
            };
            var thirdRan = 0;
            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, tasks[0], ct => { tasks[0].IsTaskCompletedSuccessfully = true; return Task.CompletedTask; }),
                new TaskDescriptor(1, tasks[1], ct =>
                {
                    service.AbandonRun();
                    tasks[1].IsTaskCompletedSuccessfully = true;   // reports success when it finally returns
                    return Task.CompletedTask;
                }),
                new TaskDescriptor(2, tasks[2], ct => { thirdRan++; return Task.CompletedTask; })
            };
            var request = BuildRequest(descriptors);
            request.Observations = observations;

            TaskExecutionResult? result = null;
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                result = await service.ExecuteOnceAsync(request);
            });

            Assert.NotNull(result);
            Assert.Equal(TaskLoopTerminalStatus.Failure, result.TerminalStatus);
            Assert.Equal(0, thirdRan);
            Assert.Equal(2, service.CurrentTaskIndex);   // the stalled task's own increment, no sentinel
            Assert.Equal(new[] { 0, 1 }, observations.Select(o => o.TaskPosition));
            Assert.False(observations[1].WasSuccessful);
            Assert.Single(logger.Entries.Where(entry => entry.Stage == "TaskLoop.Abandoned"));
        }

        [Fact]
        public async Task ErrorRoutingAfterTheWatchdogGaveUp_DoesNotReviveTheLoop()
        {
            var failing = new StubTaskModel { CurrentTaskIndex = "10" };
            var recovery = new StubTaskModel { CurrentTaskIndex = "90" };
            var recoveryRan = 0;
            var service = BuildService();
            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, failing, ct =>
                {
                    service.AbandonRun();
                    failing.CurrentTaskErrorLevel = ERROR.AuthFailure;   // would be routed to the recovery task
                    failing.IsTaskCompletedSuccessfully = false;
                    return Task.CompletedTask;
                }),
                new TaskDescriptor(1, new StubTaskModel { CurrentTaskIndex = "11" }, ct => Task.CompletedTask),
                new TaskDescriptor(2, recovery, ct => { recoveryRan++; return Task.CompletedTask; })
            };
            var request = BuildRequest(descriptors);
            request.ErrorRouting = new Dictionary<ERROR, string> { [ERROR.AuthFailure] = "90" };

            TaskExecutionResult? result = null;
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                result = await service.ExecuteOnceAsync(request);
            });

            Assert.NotNull(result);
            Assert.Equal(0, recoveryRan);
            Assert.Equal(1, service.CurrentTaskIndex);   // only the task's own increment, not moved by routing
            Assert.Equal(TaskLoopTerminalStatus.Failure, result.TerminalStatus);
        }

        [Fact]
        public async Task ATaskThatRunsAgainAfterABackwardJump_IsEvaluatedOnItsLatestAttempt()
        {
            // The first task fails once, is routed back to itself and succeeds on the second attempt. The evaluator
            // must see the second attempt only; the caller's sink receives both.
            var task = new StubTaskModel { CurrentTaskIndex = "10" };
            var attempts = 0;
            var observations = new List<TaskExecutionObservation>();
            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, task, ct =>
                {
                    attempts++;
                    task.CurrentTaskErrorLevel = attempts == 1 ? ERROR.AuthFailure : ERROR.NoError;
                    task.IsTaskCompletedSuccessfully = attempts > 1;
                    return Task.CompletedTask;
                })
            };
            var request = BuildRequest(descriptors);
            request.ErrorRouting = new Dictionary<ERROR, string> { [ERROR.AuthFailure] = "10" };
            request.Observations = observations;

            TaskExecutionResult? result = null;
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                result = await BuildService().ExecuteOnceAsync(request);
            });

            Assert.NotNull(result);
            Assert.Equal(2, attempts);
            Assert.Equal(new bool?[] { false, true }, observations.Select(o => o.WasSuccessful));
            Assert.Equal(TaskLoopTerminalStatus.Success, result.TerminalStatus);
        }

        [Fact]
        public async Task ARunCutShortByAFault_LogsHowManyTasksWereNeverReached()
        {
            var logger = new CapturingTaskExecutionLogger();
            var tasks = new[]
            {
                new StubTaskModel { CurrentTaskIndex = "10" },
                new StubTaskModel { CurrentTaskIndex = "11" },
                new StubTaskModel { CurrentTaskIndex = "12" }
            };
            var descriptors = new List<TaskDescriptor>
            {
                new TaskDescriptor(0, tasks[0], ct => { tasks[0].IsTaskCompletedSuccessfully = true; return Task.CompletedTask; }),
                new TaskDescriptor(1, tasks[1], ct => throw new InvalidOperationException("reader gone")),
                new TaskDescriptor(2, tasks[2], ct => Task.CompletedTask)
            };

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => BuildService(logger).ExecuteOnceAsync(BuildRequest(descriptors)));
            });

            // The summary is written even when the loop throws; the third task was never started.
            var summary = Assert.Single(logger.Entries.Where(entry => entry.Stage == "TaskLoop.Summary"));
            Assert.Equal(1, GetIntProperty(summary.DetailsJson, "NotReached"));
            Assert.Equal("Failure", GetStringProperty(summary.DetailsJson, "TerminalStatus"));
        }

        [Fact]
        public async Task RunSelectedOnly_InARealRun_IsNotAFailureBecauseTheOtherTasksDidNotRun()
        {
            var tasks = new[]
            {
                new StubTaskModel { CurrentTaskIndex = "0" },
                new StubTaskModel { CurrentTaskIndex = "1" },
                new StubTaskModel { CurrentTaskIndex = "2" }
            };
            var invokeCounts = new int[3];
            var descriptors = tasks
                .Select((task, i) => new TaskDescriptor(i, task, ct =>
                {
                    invokeCounts[i]++;
                    task.CurrentTaskErrorLevel = ERROR.NoError;
                    task.IsTaskCompletedSuccessfully = true;
                    return Task.CompletedTask;
                }))
                .ToList();
            var handler = new ChipTaskHandlerModel();
            foreach (var task in tasks)
            {
                handler.TaskCollection.Add(task);
            }

            var request = BuildRequest(descriptors);
            request.TaskHandler = handler;
            request.RunSelectedOnly = true;
            request.SelectedSetupViewModel = tasks[1];

            TaskExecutionResult? result = null;
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                result = await BuildService().ExecuteOnceAsync(request);
            });

            Assert.NotNull(result);
            Assert.Equal(new[] { 0, 1, 0 }, invokeCounts);
            Assert.Equal(TaskLoopTerminalStatus.Success, result.TerminalStatus);
        }

        private static string GetStringProperty(string json, string propertyName)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetProperty(propertyName).ValueKind == JsonValueKind.Null
                ? null
                : document.RootElement.GetProperty(propertyName).GetString();
        }

        private static int GetIntProperty(string json, string propertyName)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetProperty(propertyName).GetInt32();
        }

        private sealed class CapturingTaskExecutionLogger : ITaskExecutionLogger
        {
            public List<LogEntry> Entries { get; } = new List<LogEntry>();

            public void LogInformation(string stage, object details = null)
            {
                Entries.Add(new LogEntry(stage, JsonSerializer.Serialize(details)));
            }

            public void LogError(string stage, Exception exception, object details = null)
            {
                _ = exception;
                Entries.Add(new LogEntry(stage, JsonSerializer.Serialize(details)));
            }
        }

        private sealed class LogEntry
        {
            public LogEntry(string stage, string detailsJson)
            {
                Stage = stage;
                DetailsJson = detailsJson;
            }

            public string Stage { get; }
            public string DetailsJson { get; }
        }

        private sealed class LoggingStubTaskModel : IGenericTask, ITaskOutcomeDetails
        {
            public bool? IsTaskCompletedSuccessfully { get; set; }
            public ERROR SelectedExecuteConditionErrorLevel { get; set; }
            public string SelectedExecuteConditionTaskIndex { get; set; } = string.Empty;
            public ERROR CurrentTaskErrorLevel { get; set; }
            public string CurrentTaskIndex { get; set; } = string.Empty;
            public int SelectedTaskIndexAsInt => int.TryParse(CurrentTaskIndex, out var index) ? index : -1;
            public ObservableCollection<TaskAttemptResult> AttemptResults { get; } = new ObservableCollection<TaskAttemptResult>();
            public TaskExecutionState ExecutionState { get; set; } = TaskExecutionState.NotStarted;
            public string SelectedTaskType { get; set; }
            public string SelectedTaskDescription { get; set; }
            public string DesfireAppKeyCurrent { get; set; }
            public int FileNumberCurrentAsInt { get; set; }
            public int FileSizeCurrentAsInt { get; set; }
            public IReadOnlyDictionary<string, object>? Details { get; set; }
            public List<string> Calls { get; } = new List<string>();

            string ITaskOutcomeDetails.OutcomeDetailsSection => "FileSettingsComparison";

            IReadOnlyDictionary<string, object> ITaskOutcomeDetails.GetOutcomeDetails() => Details!;

            void ITaskOutcomeDetails.ClearOutcomeDetails()
            {
                Calls.Add("clear");
                Details = null;
            }
        }

        private sealed class StubReaderDeviceProvider : IReaderDeviceProvider
        {
            public RFiDGear.Infrastructure.ReaderProviders.ReaderDevice GetInstance() => null;
        }

        private sealed class StubDispatcherTimerAdapter : IDispatcherTimerAdapter
        {
            public bool IsEnabled { get; set; }
            public object Tag { get; set; }
            public void Start() { }
            public void Stop() { }
            public void Dispose() { }
        }
    }
}
