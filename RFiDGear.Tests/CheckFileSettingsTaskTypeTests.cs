using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
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
        public void TheTaskTypes_KeepTheirNamesAndOrder_AndCheckFileSettingsComesLast()
        {
            // Project files hold the name and the task-type drop-down lists the members in the order of their values,
            // so a member is appended at the end and "Check: File Settings" stays the last entry of the list.
            // "Change default values" was removed on purpose (it only rewrote the settings file and the Settings dialog
            // has the real default-keys tab), which moved CheckFileSettings from 18 to 17; nothing stores the number.
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
                ["CheckFileSettings"] = 17,
            };

            var actual = Enum.GetValues(typeof(TaskType_MifareDesfireTask))
                .Cast<TaskType_MifareDesfireTask>()
                .ToDictionary(value => value.ToString(), value => (int)value);

            Assert.Equal(expected.OrderBy(pair => pair.Key), actual.OrderBy(pair => pair.Key));
            Assert.Equal(nameof(TaskType_MifareDesfireTask.CheckFileSettings), Enum.GetNames(typeof(TaskType_MifareDesfireTask)).Last());
        }

        [Fact]
        public void EveryDesfireTaskType_HasAName_InBothLanguages()
        {
            // A missing key gives a blank entry in the task-type drop-down, so this also guards against deleting the
            // wrong one of the several ChangeDefault keys when a task type is removed.
            var resources = new ResourceManager("RFiDGear.Resources.Manifest", typeof(MifareDesfireSetupViewModel).Assembly);

            foreach (var name in Enum.GetNames(typeof(TaskType_MifareDesfireTask)))
            {
                var key = "ENUM.TaskType_MifareDesfireTask." + name;

                Assert.False(string.IsNullOrWhiteSpace(resources.GetString(key, new CultureInfo("en"))), key + " (en)");
                Assert.False(string.IsNullOrWhiteSpace(resources.GetString(key, new CultureInfo("de"))), key + " (de)");
            }
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

        // A project as an earlier version wrote it, with no mention of the new task type.
        private const string ProjectWithoutTheNewTask =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<ChipTaskHandlerModel xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
            "<ManifestVersion>2.0.10</ManifestVersion>" +
            "<TaskCollection>" +
            "<anyType xsi:type=\"MifareDesfireSetupViewModel\"><SelectedTaskType>ReadData</SelectedTaskType><CurrentTaskIndex>0</CurrentTaskIndex></anyType>" +
            "<anyType xsi:type=\"MifareDesfireSetupViewModel\"><SelectedTaskType>CheckAppKeyCount</SelectedTaskType><CurrentTaskIndex>1</CurrentTaskIndex></anyType>" +
            "</TaskCollection>" +
            "</ChipTaskHandlerModel>";

        [Fact]
        public async Task AProjectWithoutTheNewTask_StillLoads()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var serializer = new XmlSerializer(typeof(ChipTaskHandlerModel));
                using var reader = new StringReader(ProjectWithoutTheNewTask);
                var loaded = (ChipTaskHandlerModel)serializer.Deserialize(reader)!;

                Assert.Equal(
                    new[] { TaskType_MifareDesfireTask.ReadData, TaskType_MifareDesfireTask.CheckAppKeyCount },
                    loaded.TaskCollection.Cast<MifareDesfireSetupViewModel>().Select(task => task.SelectedTaskType));
            });
        }
    }
}
