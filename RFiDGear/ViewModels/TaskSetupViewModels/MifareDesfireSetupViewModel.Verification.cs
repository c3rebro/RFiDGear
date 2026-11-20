#nullable enable
using System.Diagnostics.CodeAnalysis;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Services.TaskExecution;

namespace RFiDGear.ViewModel.TaskSetupViewModels
{
    /// <summary>
    /// The task "Check: File Settings" reports what it compared as <see cref="VerificationDetails"/>, for a program that has to say why
    /// a check failed. The numbers come from <see cref="MifareDesfireSetupViewModel.LastFileSettingsComparison"/>, which the task
    /// replaces at the start of every attempt and every run.
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S2333:Partial is gratuitous in this context", Justification = "The other half is MifareDesfireSetupViewModel.cs; the analysis reads one file at a time and cannot see it.")]
    public partial class MifareDesfireSetupViewModel : ITaskVerificationSource
    {
        /// <inheritdoc />
        VerificationDetails? ITaskVerificationSource.GetVerificationDetails()
        {
            var comparison = LastFileSettingsComparison;

            return SelectedTaskType == TaskType_MifareDesfireTask.CheckFileSettings && comparison != null
                ? DesfireFileSettingsVerification.From(comparison)
                : null;
        }
    }
}
