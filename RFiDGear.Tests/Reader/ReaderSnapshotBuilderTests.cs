using System;
using System.Collections.Generic;
using System.Linq;

using RFiDGear.Contracts;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Models;

using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The picture of the chip tree the providers fill: no reader, no card, one card, a card with an LF and a LEGIC
    /// credential read together with it, and a tree that is odd. The snapshot keeps nothing of the tree.
    /// </summary>
    public class ReaderSnapshotBuilderTests
    {
        private static GenericChipModel Chip(string? uid, CARD_TYPE type, params GenericChipModel[] children) =>
            new GenericChipModel(uid!, type) { Childs = children.Length == 0 ? null! : children.ToList() };

        [Fact]
        public void WithoutAReader_ThereIsNoCard_EvenIfTheTreeStillHoldsOne()
        {
            var snapshot = ReaderSnapshotBuilder.Build("Elatec", isConnected: false, Chip("04AA", CARD_TYPE.Mifare1K));

            Assert.Equal(ReaderScanState.NoReader, snapshot.State);
            Assert.Equal("Elatec", snapshot.ReaderName);
            Assert.Empty(snapshot.Credentials);
        }

        [Fact]
        public void WithAReaderButNoChip_TheReaderWasReadAndThereIsNoCard()
        {
            var snapshot = ReaderSnapshotBuilder.Build(null, isConnected: true, chip: null);

            Assert.Equal(ReaderScanState.Ready, snapshot.State);
            Assert.Equal(string.Empty, snapshot.ReaderName);
            Assert.False(snapshot.HasCard);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void AChipWithoutAUid_IsNoCard(string? uid)
        {
            Assert.Empty(ReaderSnapshotBuilder.CredentialsOf(Chip(uid, CARD_TYPE.NOTAG)));
        }

        [Fact]
        public void OneCard_IsOneCredential_WithItsUidInUpperCase_ItsCodeAndItsName()
        {
            var credential = Assert.Single(ReaderSnapshotBuilder.CredentialsOf(Chip(" 04aa1b2c ", CARD_TYPE.Mifare1K)));

            Assert.Equal(new CredentialInfo("04AA1B2C", (int)CARD_TYPE.Mifare1K, "Mifare1K", true), credential);
        }

        [Fact]
        public void ACardWithAnLfAndALegicCredential_ListsThemInOrder_AndOnlyTheFirstIsPrimary()
        {
            var tree = Chip("04AA", CARD_TYPE.DESFireEV1_8K, Chip("0123456789", CARD_TYPE.EM4102), Chip("BEEF", CARD_TYPE.LEGIC));

            var credentials = ReaderSnapshotBuilder.CredentialsOf(tree);

            Assert.Equal(new[] { "04AA", "0123456789", "BEEF" }, credentials.Select(c => c.Uid));
            Assert.Equal(new[] { "DESFireEV1_8K", "EM4102", "LEGIC" }, credentials.Select(c => c.DisplayName));
            Assert.Equal(new[] { true, false, false }, credentials.Select(c => c.IsPrimary));
        }

        [Fact]
        public void AnLfCardAlone_IsAPrimaryCredential()
        {
            var credential = Assert.Single(ReaderSnapshotBuilder.CredentialsOf(Chip("0123456789", CARD_TYPE.EM4102)));

            Assert.True(credential.IsPrimary);
            Assert.Equal((int)CARD_TYPE.EM4102, credential.TypeCode);
        }

        [Fact]
        public void ARootWithoutAUid_IsNoCredential_ButItsChildrenAre_AndTheFirstOfThemIsPrimary()
        {
            var tree = Chip(string.Empty, CARD_TYPE.NOTAG, Chip("0123456789", CARD_TYPE.EM4102), Chip("BEEF", CARD_TYPE.LEGIC));

            var credentials = ReaderSnapshotBuilder.CredentialsOf(tree);

            Assert.Equal(new[] { "0123456789", "BEEF" }, credentials.Select(c => c.Uid));
            Assert.Equal(new[] { true, false }, credentials.Select(c => c.IsPrimary));
        }

        [Fact]
        public void ADeeperTree_IsWalkedDepthFirst_TheNodeBeforeItsChildren()
        {
            var tree = Chip("R", CARD_TYPE.MIFARE, Chip("A", CARD_TYPE.EM4102, Chip("A1", CARD_TYPE.AWID)), Chip("B", CARD_TYPE.LEGIC));

            Assert.Equal(new[] { "R", "A", "A1", "B" }, ReaderSnapshotBuilder.CredentialsOf(tree).Select(c => c.Uid));
        }

        [Fact]
        public void ATreeThatLoopsBack_IsWalkedOnce()
        {
            var root = Chip("R", CARD_TYPE.MIFARE, Chip("A", CARD_TYPE.EM4102));
            root.Childs[0].Childs = new List<GenericChipModel> { root, root.Childs[0] };

            Assert.Equal(new[] { "R", "A" }, ReaderSnapshotBuilder.CredentialsOf(root).Select(c => c.Uid));
        }

        [Fact]
        public void ANullChild_IsSkipped()
        {
            var root = Chip("R", CARD_TYPE.MIFARE);
            root.Childs = new List<GenericChipModel> { null!, Chip("A", CARD_TYPE.EM4102), null! };

            Assert.Equal(new[] { "R", "A" }, ReaderSnapshotBuilder.CredentialsOf(root).Select(c => c.Uid));
        }

        [Fact]
        public void TheSnapshot_KeepsNothingOfTheTree()
        {
            var tree = Chip("04AA", CARD_TYPE.Mifare1K, Chip("BEEF", CARD_TYPE.LEGIC));
            var snapshot = ReaderSnapshotBuilder.Build("Elatec", isConnected: true, tree);

            tree.UID = "FFFF";
            tree.CardType = CARD_TYPE.EM4102;
            tree.Childs.Clear();

            Assert.Equal(new[] { "04AA", "BEEF" }, snapshot.Credentials.Select(c => c.Uid));
            Assert.Equal((int)CARD_TYPE.Mifare1K, snapshot.Credentials[0].TypeCode);
        }

        [Fact]
        public void ATypeThatSharesItsNumberWithAnother_GetsOneOfTheirNames_AndTheSameCode()
        {
            // DESFire and DESFireEV0 are the same number, and so are SmartMX_DESFire and SmartMX_DESFire_Generic.
            var first = Assert.Single(ReaderSnapshotBuilder.CredentialsOf(Chip("01", CARD_TYPE.DESFire)));
            var second = Assert.Single(ReaderSnapshotBuilder.CredentialsOf(Chip("01", CARD_TYPE.DESFireEV0)));

            Assert.Equal(first, second);
            Assert.Contains(first.DisplayName, new[] { nameof(CARD_TYPE.DESFire), nameof(CARD_TYPE.DESFireEV0) });
        }

        [Fact]
        public void ANumberThatIsNoCardType_IsNamedUnknown_ButKeepsItsCode()
        {
            var number = (CARD_TYPE)0x01;
            Assert.False(Enum.IsDefined(number));

            var credential = Assert.Single(ReaderSnapshotBuilder.CredentialsOf(Chip("01", number)));

            Assert.Equal(("Unknown", 0x01), (credential.DisplayName, credential.TypeCode));
        }

        [Fact]
        public void EveryCardTypeThereIs_HasANameAndItsOwnCode()
        {
            foreach (var type in Enum.GetValues<CARD_TYPE>())
            {
                var credential = Assert.Single(ReaderSnapshotBuilder.CredentialsOf(Chip("01", type)));

                Assert.Equal((int)type, credential.TypeCode);
                Assert.NotEqual(ReaderSnapshotBuilder.UnknownTypeName, credential.DisplayName);
                Assert.Equal(type, Enum.Parse<CARD_TYPE>(credential.DisplayName));
            }
        }

        [Fact]
        public void TheCodesOfTheMainCardTypes_AreTheNumbersExtensionsMayRelyOn()
        {
            // The contract promises that a code means the same in every release: these are written out here, so that a
            // renumbering of the card type list is noticed.
            var types = new[] { CARD_TYPE.NOTAG, CARD_TYPE.EM4102, CARD_TYPE.MIFARE, CARD_TYPE.LEGIC, CARD_TYPE.Mifare1K, CARD_TYPE.DESFireEV1 };

            Assert.Equal(
                new[] { 0x00, 0x40, 0x80, 0x83, 0x1100, 0x5000 },
                types.Select(type => ReaderSnapshotBuilder.CredentialsOf(Chip("01", type))[0].TypeCode));
        }

        [Fact]
        public void TheNoReaderSnapshot_HasTheNameAndNothingElse()
        {
            var snapshot = ReaderSnapshotBuilder.NoReader("Elatec");

            Assert.Equal(("Elatec", ReaderScanState.NoReader), (snapshot.ReaderName, snapshot.State));
            Assert.Empty(snapshot.Credentials);
            Assert.Equal(string.Empty, ReaderSnapshotBuilder.NoReader(null).ReaderName);
        }
    }
}
