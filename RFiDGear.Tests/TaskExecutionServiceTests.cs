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
            TaskExecutionResult? executionResult = null;
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
            Assert.Equal("Failed", GetStringProperty(summary.DetailsJson, "TerminalStatus"));

            Assert.NotNull(executionResult);
            Assert.Equal(TaskExecutionTerminalStatus.Failed, executionResult!.TerminalStatus);
            Assert.Equal(runIds[0], executionResult.RunId);
            Assert.Single(logger.Entries.Where(entry => entry.Stage == "TaskLoop.Failure"));
            Assert.Empty(logger.Entries.Where(entry => entry.Stage == "TaskLoop.Success"));
        }

        [Fact]
        public async Task StructuredLogs_EmitSuccessWhenNegativeOutcomeIsConsumedByExecutedBranch()
        {
            var logger = new CapturingTaskExecutionLogger();
            TaskExecutionResult? executionResult = null;
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
            Assert.Equal("Succeeded", GetStringProperty(summary.DetailsJson, "TerminalStatus"));

            Assert.NotNull(executionResult);
            Assert.Equal(TaskExecutionTerminalStatus.Succeeded, executionResult!.TerminalStatus);
            Assert.Single(logger.Entries.Where(entry => entry.Stage == "TaskLoop.Success"));
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

            Assert.Equal(TaskExecutionTerminalStatus.Failed, result.TerminalStatus);
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

            Assert.Equal(TaskExecutionTerminalStatus.Failed, result.TerminalStatus);
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

            Assert.Equal(TaskExecutionTerminalStatus.Succeeded, result.TerminalStatus);
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

            Assert.Equal(TaskExecutionTerminalStatus.Succeeded, result.TerminalStatus);
            Assert.Equal(1, result.AcceptedFailures);
            Assert.Equal(0, result.UnhandledFailures);
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

        private sealed class LoggingStubTaskModel : IGenericTask
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
