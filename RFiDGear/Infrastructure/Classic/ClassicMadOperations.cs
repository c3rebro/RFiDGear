#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RFiDGear.Infrastructure.Classic
{
    /// <summary>What the MAD operations found out about the chip, next to the normalized <see cref="ERROR"/>.</summary>
    public enum ClassicMadStatus
    {
        /// <summary>Nothing to report.</summary>
        Ok = 0,

        /// <summary>The chip has no MAD: the general purpose byte of sector 0 does not say that there is one (a chip as it leaves the factory has <c>69</c> there).</summary>
        NoMad = 1,

        /// <summary>The MAD is there, but the CRC of MAD1 or MAD2 is wrong (or MAD2 is announced on a chip that has no sector 16): nothing is written over it.</summary>
        Damaged = 2,

        /// <summary>No sector of the MAD carries the AID.</summary>
        NotFound = 3,

        /// <summary>The sectors of the application hold fewer bytes than were asked for.</summary>
        TooShort = 4,

        /// <summary>A sector that the write needs belongs to another application.</summary>
        SectorTaken = 5,

        /// <summary>The data needs more sectors than the chip and its MAD have from the start sector on (sector 0 and the MAD2 sector hold no application).</summary>
        NoRoom = 6,

        /// <summary>The request cannot be carried out as it is: no data, no keys, an AID that is a code of the administration, a negative sector.</summary>
        WrongRequest = 7,

        /// <summary>A trailer is to be written, and a key that goes into it is neither new nor given as the current one.</summary>
        KeysMissing = 8
    }

    /// <summary>The result of <see cref="ClassicMadOperations.ReadMadAsync(ClassicSectorKeys)"/>.</summary>
    public sealed class ClassicMadReadResult
    {
        internal ClassicMadReadResult(ERROR error, ClassicMadStatus status, ClassicMad? mad, byte generalPurposeByte)
        {
            Error = error;
            Status = status;
            Mad = mad;
            GeneralPurposeByte = generalPurposeByte;
        }

        /// <summary><see cref="ERROR.NoError"/> when the MAD was read and is valid; otherwise why not (a key that did not log in, a chip that went away, or <see cref="ERROR.ProtocolConstraint"/> with a <see cref="Status"/>).</summary>
        public ERROR Error { get; }

        /// <summary>Gets what was found: <see cref="ClassicMadStatus.NoMad"/> or <see cref="ClassicMadStatus.Damaged"/> say that the sectors could be read and the MAD is not usable.</summary>
        public ClassicMadStatus Status { get; }

        /// <summary>Gets the directory, also when it is damaged (see <see cref="ClassicMad.Problems"/>); <c>null</c> when none was read.</summary>
        public ClassicMad? Mad { get; }

        /// <summary>Gets the general purpose byte of sector 0 (0 when it was not read).</summary>
        public byte GeneralPurposeByte { get; }

        /// <summary>Gets a value that says whether a valid MAD was read.</summary>
        public bool Succeeded => Error == ERROR.NoError;
    }

    /// <summary>The result of <see cref="ClassicMadOperations.ReadApplicationAsync(ushort, int, ClassicSectorKeys, ClassicSectorKeys)"/>.</summary>
    public sealed class ClassicApplicationReadResult
    {
        internal ClassicApplicationReadResult(ERROR error, ClassicMadStatus status, byte[]? data, IReadOnlyList<int> sectors)
        {
            Error = error;
            Status = status;
            Data = data;
            Sectors = sectors;
        }

        /// <summary>Gets how it went, as in <see cref="ClassicMadReadResult.Error"/>; <see cref="ERROR.PermissionDenied"/> when a block of the application cannot be read with the keys.</summary>
        public ERROR Error { get; }

        /// <summary>Gets what was found.</summary>
        public ClassicMadStatus Status { get; }

        /// <summary>Gets the bytes of the application (the data blocks of its sectors, in order), as many as were asked for; <c>null</c> when the read failed.</summary>
        public byte[]? Data { get; }

        /// <summary>Gets the sectors that the MAD lists for the application.</summary>
        public IReadOnlyList<int> Sectors { get; }

        /// <summary>Gets a value that says whether the data was read.</summary>
        public bool Succeeded => Error == ERROR.NoError;
    }

    /// <summary>What <see cref="ClassicMadOperations.WriteApplicationAsync(ClassicApplicationWrite)"/> is to do.</summary>
    public sealed class ClassicApplicationWrite
    {
        /// <summary>Gets the application identifier. The codes of the administration (0 to 5) are no application.</summary>
        public ushort Aid { get; init; }

        /// <summary>
        /// Gets the first sector of the application, or 0 for the sectors that the MAD already lists for the AID. A sector that is listed for
        /// another AID is not taken, and neither are sector 0 and the sector of MAD2.
        /// </summary>
        public int StartSector { get; init; }

        /// <summary>Gets the data of the application: exactly these bytes are written, from the first block of the first sector on; what lies behind them in the last sector stays as it is.</summary>
        public byte[]? Data { get; init; }

        /// <summary>Gets the keys that the MAD sectors have now.</summary>
        public ClassicSectorKeys? MadKeys { get; init; }

        /// <summary>Gets the keys that the sectors of the application have now.</summary>
        public ClassicSectorKeys? ApplicationKeys { get; init; }

        /// <summary>Gets the keys that the sectors that the data reaches are to get; <c>null</c> to leave them as they are.</summary>
        public ClassicSectorKeys? NewApplicationKeys { get; init; }

        /// <summary>Gets the access bits that the sectors that the data reaches are to get (never ones that can not be changed again: such a request is refused before the first block); <c>null</c> to leave them as they are.</summary>
        public ClassicAccessBits? NewAccessBits { get; init; }

        /// <summary>Gets a value that says whether a MAD is made when the chip has none (the MAD sectors get the standard access bits, the keys <see cref="NewMadKeys"/> or else <see cref="MadKeys"/>).</summary>
        public bool CreateMad { get; init; }

        /// <summary>Gets the keys that the MAD sectors are to get (needed with both keys when a MAD is made); <c>null</c> to leave them as they are.</summary>
        public ClassicSectorKeys? NewMadKeys { get; init; }

        /// <summary>Gets the general purpose byte of the MAD sectors that are written (see <see cref="ClassicMadGpb"/>); a byte that is no MAD byte is replaced by <c>C1</c> or <c>C2</c> for the version of the MAD.</summary>
        public byte MadGeneralPurposeByte { get; init; } = 0xC1;

        /// <summary>Makes the same request with a copy of the data, so that what is checked is what is written whatever the caller does to its array.</summary>
        /// <returns>The copy.</returns>
        internal ClassicApplicationWrite WithDataCopy() =>
            new ClassicApplicationWrite
            {
                Aid = Aid,
                StartSector = StartSector,
                Data = Data?.ToArray(),
                MadKeys = MadKeys,
                ApplicationKeys = ApplicationKeys,
                NewApplicationKeys = NewApplicationKeys,
                NewAccessBits = NewAccessBits,
                CreateMad = CreateMad,
                NewMadKeys = NewMadKeys,
                MadGeneralPurposeByte = MadGeneralPurposeByte
            };
    }

    /// <summary>The result of <see cref="ClassicMadOperations.WriteApplicationAsync(ClassicApplicationWrite)"/>.</summary>
    public sealed class ClassicApplicationWriteResult
    {
        internal ClassicApplicationWriteResult(ERROR error, ClassicMadStatus status, IReadOnlyList<int> sectors, int blocksWritten, int? failedBlock)
        {
            Error = error;
            Status = status;
            Sectors = sectors;
            BlocksWritten = blocksWritten;
            FailedBlock = failedBlock;
        }

        /// <summary>Gets how it went; <see cref="ERROR.NoError"/> when the data, the MAD and the trailers that were asked for are written and read back.</summary>
        public ERROR Error { get; }

        /// <summary>Gets what was found out (see <see cref="ClassicMadStatus"/>); a request that is refused before any block is written says why here.</summary>
        public ClassicMadStatus Status { get; }

        /// <summary>Gets the sectors of the application.</summary>
        public IReadOnlyList<int> Sectors { get; }

        /// <summary>Gets how many blocks were written (and read back as written) before the call ended: the data first, then the MAD, then the trailers.</summary>
        public int BlocksWritten { get; }

        /// <summary>Gets the block that ended the call, or <c>null</c>.</summary>
        public int? FailedBlock { get; }

        /// <summary>Gets a value that says whether everything was written.</summary>
        public bool Succeeded => Error == ERROR.NoError;
    }

    /// <summary>
    /// The MIFARE Application Directory on top of <see cref="ClassicOperations"/>: for every reader the same, because it uses nothing but the
    /// reads and writes of that class. It reads the directory (MAD1, and MAD2 where the chip announces it), reads an application by its AID, and
    /// writes one: the data first (exactly the bytes that were given, what lies behind them stays), then the directory, then the keys of the
    /// sectors if they are to change. Each block is one attempt that is read back and never repeated, so a call that ends early leaves a state that is
    /// described by <see cref="ClassicApplicationWriteResult.BlocksWritten"/>: data without a directory entry (harmless), and a directory entry is
    /// never written before the data of the application.
    /// </summary>
    public sealed class ClassicMadOperations
    {
        private static readonly ClassicWriteOptions DataOnly = ClassicWriteOptions.DataOnly;
        private static readonly ClassicWriteOptions WithTrailer = new ClassicWriteOptions { AllowTrailer = true };

        private readonly ClassicOperations operations;

        /// <summary>Creates the MAD operations.</summary>
        /// <param name="operations">The operations on the chip.</param>
        public ClassicMadOperations(ClassicOperations operations)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        }

        /// <summary>
        /// Reads the directory: sector 0 (blocks 1 and 2) and, when the general purpose byte announces MAD2 and the chip has the sector, sector 16.
        /// A chip without a MAD (no valid byte with the DA bit) is <see cref="ClassicMadStatus.NoMad"/>, a MAD with a wrong CRC is
        /// <see cref="ClassicMadStatus.Damaged"/>; nothing is written.
        /// </summary>
        /// <param name="madKeys">The keys of the MAD sectors (key A of the MAD is <see cref="ClassicMad.DefaultKeyA"/> on a chip that follows the specification).</param>
        /// <returns>The directory, or why not.</returns>
        public async Task<ClassicMadReadResult> ReadMadAsync(ClassicSectorKeys madKeys)
        {
            ArgumentNullException.ThrowIfNull(madKeys);

            var first = await operations.ReadSectorAsync(ClassicMad.Mad1Sector, madKeys);

            if (!first.IsAuthenticated)
            {
                return new ClassicMadReadResult(first.Error, ClassicMadStatus.Ok, null, 0);
            }

            var trailer = first.Blocks[^1].Data!;

            if (!ClassicMadGpb.TryParse(trailer[9], out var gpb) || !gpb.MadAvailable)
            {
                return new ClassicMadReadResult(ERROR.ProtocolConstraint, ClassicMadStatus.NoMad, null, trailer[9]);
            }

            var mad1 = BlockBytes(first, 1, 2);

            if (mad1 == null)
            {
                return new ClassicMadReadResult(ERROR.PermissionDenied, ClassicMadStatus.Ok, null, trailer[9]);
            }

            var mad2 = Array.Empty<byte>();

            if (gpb.Version == ClassicMadVersion.Mad2)
            {
                var second = await operations.ReadSectorAsync(ClassicMad.Mad2Sector, madKeys);

                if (second.Error == ERROR.ProtocolConstraint)
                {
                    // MAD2 is announced on a chip that has no sector 16.
                    return new ClassicMadReadResult(ERROR.ProtocolConstraint, ClassicMadStatus.Damaged, null, trailer[9]);
                }

                var bytes = second.IsAuthenticated ? BlockBytes(second, 0, 1, 2) : null;

                if (bytes == null)
                {
                    return new ClassicMadReadResult(second.IsAuthenticated ? ERROR.PermissionDenied : second.Error, ClassicMadStatus.Ok, null, trailer[9]);
                }

                mad2 = bytes;
            }

            ClassicMad.TryParse(mad1, mad2, out var mad);

            return mad!.Problems == ClassicMadProblems.None
                ? new ClassicMadReadResult(ERROR.NoError, ClassicMadStatus.Ok, mad, trailer[9])
                : new ClassicMadReadResult(ERROR.ProtocolConstraint, ClassicMadStatus.Damaged, mad, trailer[9]);
        }

        /// <summary>
        /// Reads the data of an application: the sectors that the MAD lists for the AID, in ascending order, each with its data blocks (never its
        /// trailer), as many bytes as are asked for.
        /// </summary>
        /// <param name="aid">The application identifier (not one of the codes of the administration).</param>
        /// <param name="length">How many bytes, at least 1.</param>
        /// <param name="madKeys">The keys of the MAD sectors.</param>
        /// <param name="applicationKeys">The keys of the sectors of the application.</param>
        /// <returns>The data, or why not.</returns>
        public async Task<ClassicApplicationReadResult> ReadApplicationAsync(ushort aid, int length, ClassicSectorKeys madKeys, ClassicSectorKeys applicationKeys)
        {
            ArgumentNullException.ThrowIfNull(madKeys);
            ArgumentNullException.ThrowIfNull(applicationKeys);

            if (aid <= ClassicMadAid.NotApplicable || length < 1)
            {
                return new ClassicApplicationReadResult(ERROR.ProtocolConstraint, ClassicMadStatus.WrongRequest, null, Array.Empty<int>());
            }

            var directory = await ReadMadAsync(madKeys);

            if (!directory.Succeeded)
            {
                return new ClassicApplicationReadResult(directory.Error, directory.Status, null, Array.Empty<int>());
            }

            var sectors = directory.Mad!.FindSectors(aid);

            if (sectors.Count == 0)
            {
                return new ClassicApplicationReadResult(ERROR.ProtocolConstraint, ClassicMadStatus.NotFound, null, sectors);
            }

            var bytes = new List<byte>();

            foreach (var sector in sectors)
            {
                var read = await operations.ReadSectorAsync(sector, applicationKeys);
                var data = read.IsAuthenticated ? DataBlocksOf(read) : null;

                if (data == null)
                {
                    return new ClassicApplicationReadResult(read.IsAuthenticated ? ERROR.PermissionDenied : read.Error, ClassicMadStatus.Ok, null, sectors);
                }

                bytes.AddRange(data);

                if (bytes.Count >= length)
                {
                    break;
                }
            }

            return bytes.Count < length
                ? new ClassicApplicationReadResult(ERROR.ProtocolConstraint, ClassicMadStatus.TooShort, null, sectors)
                : new ClassicApplicationReadResult(ERROR.NoError, ClassicMadStatus.Ok, bytes.Take(length).ToArray(), sectors);
        }

        /// <summary>
        /// Writes an application. In this order: the data (exactly the bytes given), the entries of the directory (a directory is made first when
        /// there is none and the request says so), and, if the request asks for new keys or access bits, the trailers of the sectors of the application.
        /// Nothing is written when the request cannot be carried out as it is (see <see cref="ClassicMadStatus"/>).
        /// </summary>
        /// <param name="requested">What to write.</param>
        /// <returns>How far it came, and how it went.</returns>
        public async Task<ClassicApplicationWriteResult> WriteApplicationAsync(ClassicApplicationWrite requested)
        {
            ArgumentNullException.ThrowIfNull(requested);

            // The data is copied before it is checked: the array of a caller can change while the call waits for the reader (the editor of a
            // dialog does), and what was checked is what is written.
            var request = requested.WithDataCopy();

            if (!IsPossible(request))
            {
                return Refused(ClassicMadStatus.WrongRequest);
            }

            var directory = await LoadDirectoryAsync(request);

            if (directory.Mad == null)
            {
                return new ClassicApplicationWriteResult(directory.Error, directory.Status, Array.Empty<int>(), 0, null);
            }

            var plan = PlanSectors(request, directory.Mad);

            if (plan.Status != ClassicMadStatus.Ok)
            {
                return Refused(plan.Status);
            }

            // What a later step needs is checked before the first block is written: a trailer cannot be built without its two keys.
            if (!KeysAreThere(request, directory))
            {
                return Refused(ClassicMadStatus.KeysMissing);
            }

            var written = 0;
            var data = await WriteDataAsync(request, plan.Sectors, written);

            if (!data.Succeeded)
            {
                return data;
            }

            written = data.BlocksWritten;

            var registered = await RegisterAsync(request, directory, plan.Sectors, written);

            if (!registered.Succeeded)
            {
                return registered;
            }

            written = registered.BlocksWritten;

            return await RekeyAsync(request, plan.Sectors, written);
        }

        // ── a request ──

        /// <summary>
        /// Tells whether a request can be carried out at all, before the chip is asked anything: it has data, an AID that is an application, keys, and
        /// access bits that can be changed again.
        /// </summary>
        /// <param name="request">What is to be written.</param>
        /// <returns><c>false</c> for a request that is refused as <see cref="ClassicMadStatus.WrongRequest"/>.</returns>
        internal static bool IsPossible(ClassicApplicationWrite request) =>
            request.Aid > ClassicMadAid.NotApplicable
            && request.StartSector >= 0
            && request.Data is { Length: > 0 }
            && request.MadKeys != null
            && request.ApplicationKeys != null
            // Access bits that can never be written again are for good, and a call that has written data and a directory first must not find that out.
            && request.NewAccessBits is not { AccessBitsAreFinal: true };

        private static bool KeysAreThere(ClassicApplicationWrite request, Directory directory)
        {
            var madKeys = request.NewMadKeys ?? request.MadKeys!;
            var madOk = !(directory.Created || request.NewMadKeys != null) || (madKeys.HasKeyA && madKeys.HasKeyB);
            var rekeys = request.NewApplicationKeys != null || request.NewAccessBits != null;

            return madOk && (!rekeys || (KeyOf(ClassicKeyType.KeyA, request) != null && KeyOf(ClassicKeyType.KeyB, request) != null));
        }

        private static ClassicApplicationWriteResult Refused(ClassicMadStatus status) =>
            new ClassicApplicationWriteResult(ERROR.ProtocolConstraint, status, Array.Empty<int>(), 0, null);

        private static ClassicApplicationWriteResult Complete(IReadOnlyList<int> sectors, int written) =>
            new ClassicApplicationWriteResult(ERROR.NoError, ClassicMadStatus.Ok, sectors, written, null);

        private static ClassicApplicationWriteResult Failed(ERROR error, IReadOnlyList<int> sectors, int written, int? block) =>
            new ClassicApplicationWriteResult(error, ClassicMadStatus.Ok, sectors, written, block);

        // ── the directory ──

        private sealed class Directory
        {
            public Directory(ClassicMad? mad, bool created, ERROR error, ClassicMadStatus status)
            {
                Mad = mad;
                Created = created;
                Error = error;
                Status = status;
            }

            public ClassicMad? Mad { get; }

            public bool Created { get; }

            public ERROR Error { get; }

            public ClassicMadStatus Status { get; }

            // The bytes of the directory as the chip holds them (before the change), to write only what changed.
            public byte[]? Before1 { get; set; }

            public byte[]? Before2 { get; set; }
        }

        private async Task<Directory> LoadDirectoryAsync(ClassicApplicationWrite request)
        {
            var read = await ReadMadAsync(request.MadKeys!);

            if (read.Succeeded)
            {
                return new Directory(read.Mad, false, ERROR.NoError, ClassicMadStatus.Ok)
                {
                    Before1 = read.Mad!.ToMad1Bytes(),
                    Before2 = read.Mad.Version == ClassicMadVersion.Mad2 ? read.Mad.ToMad2Bytes() : null
                };
            }

            if (read.Status != ClassicMadStatus.NoMad || !request.CreateMad)
            {
                return new Directory(null, false, read.Error, read.Status);
            }

            // A chip without a MAD gets one: MAD2 where the chip has the sector for it.
            var version = ClassicLayout.HasSector(operations.ChipSize, ClassicMad.Mad2Sector + 1) ? ClassicMadVersion.Mad2 : ClassicMadVersion.Mad1;

            return new Directory(ClassicMad.Create(version), true, ERROR.NoError, ClassicMadStatus.Ok);
        }

        // ── the sectors ──

        private sealed class SectorPlan
        {
            public SectorPlan(ClassicMadStatus status, List<int> sectors)
            {
                Status = status;
                Sectors = sectors;
            }

            public ClassicMadStatus Status { get; }

            public List<int> Sectors { get; }
        }

        private SectorPlan PlanSectors(ClassicApplicationWrite request, ClassicMad mad)
        {
            var needed = request.Data!.Length;

            if (request.StartSector == 0)
            {
                var listed = mad.FindSectors(request.Aid).ToList();

                if (listed.Count == 0)
                {
                    return new SectorPlan(ClassicMadStatus.NotFound, listed);
                }

                if (listed.Sum(DataBytes) < needed)
                {
                    return new SectorPlan(ClassicMadStatus.NoRoom, listed);
                }

                // Only the sectors that the data reaches: the later ones of the application stay as they are, untouched.
                var covered = new List<int>();

                foreach (var listedSector in listed)
                {
                    if (needed <= 0)
                    {
                        break;
                    }

                    covered.Add(listedSector);
                    needed -= DataBytes(listedSector);
                }

                return new SectorPlan(ClassicMadStatus.Ok, covered);
            }

            var sectors = new List<int>();
            var sector = request.StartSector;

            while (needed > 0)
            {
                if (sector == ClassicMad.Mad2Sector && mad.Version == ClassicMadVersion.Mad2)
                {
                    sector++;
                }

                if (!mad.Covers(sector) || !ClassicLayout.HasSector(operations.ChipSize, sector))
                {
                    return new SectorPlan(ClassicMadStatus.NoRoom, sectors);
                }

                var current = mad.GetAid(sector);

                if (current != ClassicMadAid.Free && current != request.Aid)
                {
                    return new SectorPlan(ClassicMadStatus.SectorTaken, sectors);
                }

                sectors.Add(sector);
                needed -= DataBytes(sector);
                sector++;
            }

            return new SectorPlan(ClassicMadStatus.Ok, sectors);
        }

        private static int DataBytes(int sector) => ClassicLayout.DataBlocksInSector(sector) * ClassicLayout.BytesPerBlock;

        // ── the data ──

        private async Task<ClassicApplicationWriteResult> WriteDataAsync(ClassicApplicationWrite request, List<int> sectors, int writtenBefore)
        {
            var offset = 0;
            var written = writtenBefore;
            var all = request.Data!;

            foreach (var sector in sectors)
            {
                var size = DataBytes(sector);
                var chunk = all.AsSpan(offset, Math.Min(size, all.Length - offset)).ToArray();
                var whole = chunk.Length == size ? chunk : await CompleteWithWhatIsThereAsync(sector, chunk, request.ApplicationKeys!);

                if (whole == null)
                {
                    return Failed(ERROR.PermissionDenied, sectors, written, ClassicLayout.FirstBlock(sector));
                }

                var result = await operations.WriteSectorAsync(sector, whole, request.ApplicationKeys!, DataOnly);

                written += result.BlocksWritten;

                if (!result.Succeeded)
                {
                    return result.Violation == ClassicWriteViolation.None
                        ? Failed(result.Error, sectors, written, result.FailedBlock)
                        : Refused(ClassicMadStatus.WrongRequest);
                }

                offset += chunk.Length;
            }

            return Complete(sectors, written);
        }

        // The last sector is only partly covered: what lies behind the data stays as it is, so the sector is read first.
        private async Task<byte[]?> CompleteWithWhatIsThereAsync(int sector, byte[] chunk, ClassicSectorKeys keys)
        {
            var read = await operations.ReadSectorAsync(sector, keys);
            var current = read.IsAuthenticated ? DataBlocksOf(read) : null;

            if (current == null)
            {
                return null;
            }

            chunk.CopyTo(current, 0);

            return current;
        }

        // ── the directory entries ──

        private async Task<ClassicApplicationWriteResult> RegisterAsync(ClassicApplicationWrite request, Directory directory, List<int> sectors, int writtenBefore)
        {
            var mad = directory.Mad!;
            var written = writtenBefore;

            foreach (var sector in sectors)
            {
                mad.SetAid(sector, request.Aid);
            }

            var blocks = ChangedMadBlocks(mad, directory);

            foreach (var (block, bytes) in blocks)
            {
                var result = await operations.WriteBlockAsync(block, bytes, request.MadKeys!, DataOnly);

                if (!result.Succeeded)
                {
                    return result.Violation == ClassicWriteViolation.None
                        ? Failed(result.Error, sectors, written, block)
                        : Refused(ClassicMadStatus.WrongRequest);
                }

                written += result.BlocksWritten;
            }

            var trailers = await WriteMadTrailersAsync(request, directory, sectors, written);

            return trailers;
        }

        // The blocks of the directory that differ from what the chip holds (all of them for a directory that is made).
        private static List<(int Block, byte[] Bytes)> ChangedMadBlocks(ClassicMad mad, Directory directory)
        {
            var changed = new List<(int, byte[])>();

            AddChanged(changed, ClassicMad.Mad1FirstBlock, mad.ToMad1Bytes(), directory.Before1, directory.Created);

            if (mad.Version == ClassicMadVersion.Mad2)
            {
                AddChanged(changed, ClassicMad.Mad2FirstBlock, mad.ToMad2Bytes(), directory.Before2, directory.Created);
            }

            return changed;
        }

        private static void AddChanged(List<(int, byte[])> changed, int firstBlock, byte[] now, byte[]? before, bool all)
        {
            for (var i = 0; i < now.Length / ClassicLayout.BytesPerBlock; i++)
            {
                var block = now.AsSpan(i * ClassicLayout.BytesPerBlock, ClassicLayout.BytesPerBlock);

                if (all || before == null || !block.SequenceEqual(before.AsSpan(i * ClassicLayout.BytesPerBlock, ClassicLayout.BytesPerBlock)))
                {
                    changed.Add((firstBlock + i, block.ToArray()));
                }
            }
        }

        // A directory that is made (or whose keys are to change) gets its trailers: the standard access bits, the keys, the general purpose byte.
        private async Task<ClassicApplicationWriteResult> WriteMadTrailersAsync(ClassicApplicationWrite request, Directory directory, List<int> sectors, int writtenBefore)
        {
            var wanted = directory.Created || request.NewMadKeys != null;

            if (!wanted)
            {
                return Complete(sectors, writtenBefore);
            }

            var keys = request.NewMadKeys ?? request.MadKeys!;

            if (!keys.HasKeyA || !keys.HasKeyB)
            {
                return Refused(ClassicMadStatus.KeysMissing);
            }

            var gpb = ClassicMadGpb.TryParse(request.MadGeneralPurposeByte, out var parsed) && parsed.Version == directory.Mad!.Version
                ? request.MadGeneralPurposeByte
                : ClassicMadGpb.Create(directory.Mad!.Version, true);
            var trailer = ClassicAccessBits.MadStandard.BuildTrailer(keys.Get(ClassicKeyType.KeyA), gpb, keys.Get(ClassicKeyType.KeyB));
            var written = writtenBefore;

            foreach (var block in directory.Mad.Version == ClassicMadVersion.Mad2
                         ? new[] { ClassicMad.Mad1TrailerBlock, ClassicMad.Mad2TrailerBlock }
                         : new[] { ClassicMad.Mad1TrailerBlock })
            {
                var result = await operations.WriteBlockAsync(block, trailer, request.MadKeys!, WithTrailer);

                if (!result.Succeeded)
                {
                    return result.Violation == ClassicWriteViolation.None
                        ? Failed(result.Error, sectors, written, block)
                        : Refused(ClassicMadStatus.WrongRequest);
                }

                written += result.BlocksWritten;
            }

            return Complete(sectors, written);
        }

        // ── the keys of the application ──

        private async Task<ClassicApplicationWriteResult> RekeyAsync(ClassicApplicationWrite request, List<int> sectors, int writtenBefore)
        {
            if (request.NewApplicationKeys == null && request.NewAccessBits == null)
            {
                return Complete(sectors, writtenBefore);
            }

            var written = writtenBefore;

            foreach (var sector in sectors)
            {
                var read = await operations.ReadSectorAsync(sector, request.ApplicationKeys!);
                var bits = request.NewAccessBits ?? read.AccessBits;

                if (!read.IsAuthenticated || bits == null)
                {
                    return Failed(read.IsAuthenticated ? ERROR.ProtocolConstraint : read.Error, sectors, written, ClassicLayout.TrailerBlock(sector));
                }

                var keyA = KeyOf(ClassicKeyType.KeyA, request);
                var keyB = KeyOf(ClassicKeyType.KeyB, request);

                if (keyA == null || keyB == null)
                {
                    return Refused(ClassicMadStatus.KeysMissing);
                }

                var trailer = bits.BuildTrailer(keyA, read.Blocks[^1].Data![9], keyB);
                var result = await operations.WriteBlockAsync(ClassicLayout.TrailerBlock(sector), trailer, request.ApplicationKeys!, WithTrailer);

                if (!result.Succeeded)
                {
                    return result.Violation == ClassicWriteViolation.None
                        ? Failed(result.Error, sectors, written, ClassicLayout.TrailerBlock(sector))
                        : Refused(ClassicMadStatus.WrongRequest);
                }

                written += result.BlocksWritten;
            }

            return Complete(sectors, written);
        }

        // The key that goes into a trailer: the new one when there is one, otherwise the one that the sector has now (when it was given).
        private static byte[]? KeyOf(ClassicKeyType type, ClassicApplicationWrite request)
        {
            if (request.NewApplicationKeys != null && request.NewApplicationKeys.Has(type))
            {
                return request.NewApplicationKeys.Get(type);
            }

            return request.ApplicationKeys!.Has(type) ? request.ApplicationKeys.Get(type) : null;
        }

        // ── helpers ──

        // The blocks of a sector that was read, joined; null when one of them could not be read.
        private static byte[]? BlockBytes(ClassicSectorRead read, params int[] positions)
        {
            var bytes = new List<byte>();

            foreach (var position in positions)
            {
                var data = read.Blocks[position].Data;

                if (data == null)
                {
                    return null;
                }

                bytes.AddRange(data);
            }

            return bytes.ToArray();
        }

        // All data blocks of a sector that was read (not the trailer), joined; null when one of them could not be read.
        private static byte[]? DataBlocksOf(ClassicSectorRead read) =>
            BlockBytes(read, Enumerable.Range(0, read.Blocks.Count - 1).ToArray());
    }
}
