using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System.Reflection;
using Xunit;

namespace RFiDGear.Tests
{
    public class MifareDesfireSetupViewModelTests
    {
        [Fact]
        public void Constructor_DisablesTabsByDefault()
        {
            var viewModel = new MifareDesfireSetupViewModel();

            Assert.False(viewModel.IsDesfireFileAuthoringTabEnabled);
            Assert.False(viewModel.IsDataExplorerEditTabEnabled);
            Assert.False(viewModel.IsDesfirePICCAuthoringTabEnabled);
            Assert.False(viewModel.IsDesfireAuthenticationTabEnabled);
            Assert.False(viewModel.IsDesfireAppAuthenticationTabEnabled);
            Assert.False(viewModel.IsDesfireAppAuthoringTabEnabled);
            Assert.False(viewModel.IsDesfireAppCreationTabEnabled);
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ChangeDefault, true, true, true, true, true, true, true)]
        [InlineData(TaskType_MifareDesfireTask.ReadAppSettings, false, false, true, false, false, false, false)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, false, false, false, false, true, true, false)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeySettingsChangeover, false, false, false, false, true, true, false)]
        [InlineData(TaskType_MifareDesfireTask.AuthenticateApplication, false, false, false, true, true, true, false)]
        public void SelectedTaskType_SetsExpectedTabAvailability(
            TaskType_MifareDesfireTask taskType,
            bool fileAuthoring,
            bool dataExplorerEdit,
            bool desfirePiccAuthoring,
            bool desfireAuthentication,
            bool desfireAppAuthentication,
            bool desfireAppAuthoring,
            bool desfireAppCreation)
        {
            var viewModel = new MifareDesfireSetupViewModel
            {
                SelectedTaskType = taskType
            };

            Assert.Equal(fileAuthoring, viewModel.IsDesfireFileAuthoringTabEnabled);
            Assert.Equal(dataExplorerEdit, viewModel.IsDataExplorerEditTabEnabled);
            Assert.Equal(desfirePiccAuthoring, viewModel.IsDesfirePICCAuthoringTabEnabled);
            Assert.Equal(desfireAuthentication, viewModel.IsDesfireAuthenticationTabEnabled);
            Assert.Equal(desfireAppAuthentication, viewModel.IsDesfireAppAuthenticationTabEnabled);
            Assert.Equal(desfireAppAuthoring, viewModel.IsDesfireAppAuthoringTabEnabled);
            Assert.Equal(desfireAppCreation, viewModel.IsDesfireAppCreationTabEnabled);
        }

        [Theory]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeyChangeover, true, false)]
        [InlineData(TaskType_MifareDesfireTask.ApplicationKeySettingsChangeover, false, true)]
        [InlineData(TaskType_MifareDesfireTask.ChangeDefault, true, true)]
        public void SelectedTaskType_TogglesKeyInputVisibility(
            TaskType_MifareDesfireTask taskType,
            bool showTarget,
            bool showSettings)
        {
            var viewModel = new MifareDesfireSetupViewModel
            {
                SelectedTaskType = taskType
            };

            Assert.Equal(showTarget, viewModel.ShowAppKeyTargetInputs);
            Assert.Equal(showSettings, viewModel.ShowAppKeySettingsInputs);
        }
    }
}
