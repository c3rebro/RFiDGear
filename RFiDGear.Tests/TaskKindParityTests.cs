using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Models;
using RFiDGear.ViewModel.TaskSetupViewModels;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// A project holds its tasks in an <c>ObservableCollection&lt;object&gt;</c> that <c>XmlSerializer</c> writes. It writes
    /// only the kinds the model names with <c>[XmlInclude]</c>: any other fails the save with an exception, which is how
    /// the Ultralight task could be created in the application and never saved (#220).
    /// </summary>
    public class TaskKindParityTests
    {
        // The five kinds that the "create task" menu offers. The test below finds task kinds by the interface, so a kind that
        // forgot the interface would not be found at all: this one names them.
        [Fact]
        public void EveryKindThatTheCreateTaskMenuOffers_IsAnIGenericTaskAndKnownToTheProjectFormat()
        {
            var included = typeof(ChipTaskHandlerModel)
                .GetCustomAttributes<XmlIncludeAttribute>()
                .Select(attribute => attribute.Type)
                .ToHashSet();

            var kinds = new[]
            {
                typeof(CommonTaskViewModel),
                typeof(GenericChipTaskViewModel),
                typeof(MifareClassicSetupViewModel),
                typeof(MifareDesfireSetupViewModel),
                typeof(MifareUltralightSetupViewModel)
            };

            Assert.All(kinds, kind =>
            {
                Assert.True(typeof(IGenericTask).IsAssignableFrom(kind), kind.Name + " is not an IGenericTask: the run loop, the reset and the ordering of the list skip or stop at it.");
                Assert.True(included.Contains(kind), kind.Name + " has no [XmlInclude] on ChipTaskHandlerModel: a project that holds one cannot be saved.");
            });
        }

        [Fact]
        public void EveryTaskKindOfTheApplication_IsKnownToTheProjectFormat()
        {
            var included = typeof(ChipTaskHandlerModel)
                .GetCustomAttributes<XmlIncludeAttribute>()
                .Select(attribute => attribute.Type)
                .ToHashSet();

            var taskKinds = typeof(ChipTaskHandlerModel).Assembly
                .GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && typeof(IGenericTask).IsAssignableFrom(type))
                .ToList();

            Assert.NotEmpty(taskKinds);

            var missing = taskKinds.Where(kind => !included.Contains(kind)).Select(kind => kind.Name).ToList();

            Assert.True(
                missing.Count == 0,
                "These task kinds are IGenericTask but have no [XmlInclude] on ChipTaskHandlerModel, so a project that holds one cannot be saved: "
                + string.Join(", ", missing));
        }
    }
}
