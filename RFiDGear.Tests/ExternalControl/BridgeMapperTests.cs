#nullable enable
using System;
using System.Linq;
using System.Reflection;

using RFiDGear.Bridge;
using RFiDGear.Contracts;
using RFiDGear.Services.ExternalControl;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The mapping from what the host knows to what the wire carries. The wire types are written by hand, so a member that the
    /// host gains must make a test fail until somebody has decided whether a client gets it.
    /// </summary>
    public class BridgeMapperTests
    {
        private static string[] PublicInstanceMembers(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetIndexParameters().Length == 0 && property.Name != "EqualityContract")
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

        [Fact]
        public void TheMembersOfTheHostTypes_AreTheOnesTheMapperHandles()
        {
            // Adding a member to one of these types fails this test: map it (or leave it out on purpose) and update the list.
            Assert.Equal(new[] { "Credentials", "HasCard", "ReaderName", "State" }, PublicInstanceMembers(typeof(ReaderSnapshot)));
            Assert.Equal(new[] { "DisplayName", "IsPrimary", "TypeCode", "Uid" }, PublicInstanceMembers(typeof(CredentialInfo)));
        }

        [Fact]
        public void EveryStateOfAReaderScan_HasANameOnTheWire_ThatIsNotUnknown()
        {
            foreach (var state in Enum.GetValues<ReaderScanState>())
            {
                Assert.NotEqual(ReaderStates.Unknown, BridgeMapper.ToWire(state));
            }

            // The names are distinct, so no two states merge on the wire.
            var names = Enum.GetValues<ReaderScanState>().Select(BridgeMapper.ToWire).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void AStateTheMapperDoesNotKnow_BecomesUnknown_NotAnError()
        {
            Assert.Equal(ReaderStates.Unknown, BridgeMapper.ToWire((ReaderScanState)999));
        }

        [Fact]
        public void ASnapshotWithACard_IsMappedCompletely()
        {
            var snapshot = new ReaderSnapshot(
                "Reader 1",
                ReaderScanState.Ready,
                new[] { new CredentialInfo("04AABB", 7, "MIFARE DESFire", true), new CredentialInfo("04CCDD", 3, "MIFARE Classic", false) });

            var dto = BridgeMapper.ToDto(snapshot);

            Assert.Equal("Reader 1", dto.Name);
            Assert.Equal(ReaderStates.Ready, dto.State);
            Assert.True(dto.HasCard);
            Assert.Equal(new[] { "04AABB", "04CCDD" }, dto.Credentials.Select(c => c.Uid));
            Assert.Equal(new[] { true, false }, dto.Credentials.Select(c => c.IsPrimary));
            Assert.Equal(new[] { 7, 3 }, dto.Credentials.Select(c => c.TypeCode));
            Assert.Equal(new[] { "MIFARE DESFire", "MIFARE Classic" }, dto.Credentials.Select(c => c.DisplayName));
        }

        [Fact]
        public void AReaderWithoutAName_HasNoNameOnTheWire_AndNoCardMeansNoCredentials()
        {
            var dto = BridgeMapper.ToDto(new ReaderSnapshot(null, ReaderScanState.NoReader, Array.Empty<CredentialInfo>()));

            Assert.Null(dto.Name);
            Assert.Equal(ReaderStates.NoReader, dto.State);
            Assert.False(dto.HasCard);
            Assert.Empty(dto.Credentials);
        }

        [Fact]
        public void ANullSnapshot_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => BridgeMapper.ToDto(null!));
        }
    }
}
