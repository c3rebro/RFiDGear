using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The new task type is saved in project files by name, next to the others. Old projects must keep loading and the
    /// ones with the new task must come back as they were saved.
    /// </summary>
    public class CheckFileSettingsTaskTypeTests
    {
        [Fact]
        public void TheValuesOfTheEarlierTaskTypes_AreUnchanged_AndTheNewOneComesLast()
        {
            // Project files hold the name, but nothing may depend on the numbers shifting either: members are added at
            // the end, never inserted.
            var expected = new Dictionary<string, int>
            {
                ["None"] = 0,
                ["FormatDesfireCard"] = 1,
                ["PICCMasterKeyChangeover"] = 2,
                ["PICCMasterKeySettingsChangeover"] = 3,
                ["CreateApplication"] = 4,
                ["DeleteApplication"] = 5,
                ["ApplicationKeyChangeover"] = 6,
                ["ApplicationKeySettingsChangeover"] = 7,
                ["CreateFile"] = 8,
                ["ChangeFileSettings"] = 9,
                ["DeleteFile"] = 10,
                ["ReadData"] = 11,
                ["WriteData"] = 12,
                ["AppExistCheck"] = 13,
                ["AuthenticateApplication"] = 14,
                ["ReadAppSettings"] = 15,
                ["CheckAppKeyCount"] = 16,
                ["ChangeDefault"] = 17,
                ["CheckFileSettings"] = 18,
            };

            var actual = Enum.GetValues(typeof(TaskType_MifareDesfireTask))
                .Cast<TaskType_MifareDesfireTask>()
                .ToDictionary(value => value.ToString(), value => (int)value);

            Assert.Equal(expected.OrderBy(pair => pair.Key), actual.OrderBy(pair => pair.Key));
        }

        [Fact]
        public async Task ATaskThatChecksFileSettings_SurvivesSavingAndLoadingAProject()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var task = new MifareDesfireSetupViewModel
                {
                    SelectedTaskType = TaskType_MifareDesfireTask.CheckFileSettings,
                    CurrentTaskIndex = "7",
                    AppNumberCurrent = "0xF482D0",
                    FileNumberCurrent = "3",
                    FileSizeCurrent = "14",
                    SelectedDesfireFileType = FileType_MifareDesfireFileType.BackupFile,
                    SelectedDesfireFileCryptoMode = EncryptionMode.CM_MAC,
                    SelectedDesfireFileAccessRightRead = TaskAccessRights.AR_KEY1,
                    SelectedDesfireFileAccessRightWrite = TaskAccessRights.AR_KEY2,
                    SelectedDesfireFileAccessRightReadWrite = TaskAccessRights.AR_FREE,
                    SelectedDesfireFileAccessRightChange = TaskAccessRights.AR_KEY0
                };
                var project = new ChipTaskHandlerModel();
                project.TaskCollection.Add(task);

                var serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));
                string xml;
                using (var writer = new StringWriter())
                {
                    serializer.Serialize(writer, project);
                    xml = writer.ToString();
                }

                // By name, and with nothing of a comparison from a run in it.
                Assert.Contains("<SelectedTaskType>CheckFileSettings</SelectedTaskType>", xml);
                Assert.DoesNotContain("Comparison", xml);

                ChipTaskHandlerModel loaded;
                using (var reader = new StringReader(xml))
                {
                    loaded = (ChipTaskHandlerModel)serializer.Deserialize(reader)!;
                }

                var roundTripped = Assert.IsType<MifareDesfireSetupViewModel>(Assert.Single(loaded.TaskCollection));
                Assert.Equal(TaskType_MifareDesfireTask.CheckFileSettings, roundTripped.SelectedTaskType);
                Assert.Equal("7", roundTripped.CurrentTaskIndex);
                Assert.Equal(task.AppNumberCurrent, roundTripped.AppNumberCurrent);
                Assert.Equal("3", roundTripped.FileNumberCurrent);
                Assert.Equal("14", roundTripped.FileSizeCurrent);
                Assert.Equal(FileType_MifareDesfireFileType.BackupFile, roundTripped.SelectedDesfireFileType);
                Assert.Equal(EncryptionMode.CM_MAC, roundTripped.SelectedDesfireFileCryptoMode);
                Assert.Equal(TaskAccessRights.AR_KEY1, roundTripped.SelectedDesfireFileAccessRightRead);
                Assert.Equal(TaskAccessRights.AR_KEY2, roundTripped.SelectedDesfireFileAccessRightWrite);
                Assert.Equal(TaskAccessRights.AR_FREE, roundTripped.SelectedDesfireFileAccessRightReadWrite);
                Assert.Equal(TaskAccessRights.AR_KEY0, roundTripped.SelectedDesfireFileAccessRightChange);
            });
        }

        [Fact]
        public async Task AProjectWithoutTheNewTask_StillLoads()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var project = new ChipTaskHandlerModel();
                project.TaskCollection.Add(new MifareDesfireSetupViewModel { SelectedTaskType = TaskType_MifareDesfireTask.ReadData });
                project.TaskCollection.Add(new MifareDesfireSetupViewModel { SelectedTaskType = TaskType_MifareDesfireTask.CheckAppKeyCount });

                var serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));
                using var writer = new StringWriter();
                serializer.Serialize(writer, project);
                using var reader = new StringReader(writer.ToString());
                var loaded = (ChipTaskHandlerModel)serializer.Deserialize(reader)!;

                Assert.Equal(
                    new[] { TaskType_MifareDesfireTask.ReadData, TaskType_MifareDesfireTask.CheckAppKeyCount },
                    loaded.TaskCollection.Cast<MifareDesfireSetupViewModel>().Select(task => task.SelectedTaskType));
            });
        }
    }
}
