using System.IO;
using System.Xml.Serialization;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Models;
using Xunit;

namespace RFiDGear.Tests
{
    public class ErrorEnumCompatibilityTests
    {
        [Fact]
        public void Deserialize_WhenLegacyAuthenticationErrorValue_MapsToAuthFailure()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Checkpoint>
  <ErrorLevel>AuthenticationError</ErrorLevel>
</Checkpoint>";

            var serializer = new XmlSerializer(typeof(Checkpoint));

            using var reader = new StringReader(xml);
            var checkpoint = Assert.IsType<Checkpoint>(serializer.Deserialize(reader));

            Assert.Equal(ERROR.AuthFailure, checkpoint.ErrorLevel);
        }

        [Fact]
        public void NormalizeLegacyProjectXml_ReplacesAuthenticationErrorValue()
        {
            var projectXml = "<Checkpoint><ErrorLevel>AuthenticationError</ErrorLevel></Checkpoint>";

            var projectManager = new ProjectManager();
            var normalizedXml = projectManager.NormalizeLegacyProjectXml(projectXml);

            Assert.Equal("<Checkpoint><ErrorLevel>AuthFailure</ErrorLevel></Checkpoint>", normalizedXml);
        }
    }
}
