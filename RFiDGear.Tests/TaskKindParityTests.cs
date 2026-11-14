using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using RFiDGear.Models;
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
