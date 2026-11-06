#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.Tests.ApiSurface
{
    // ── sample surface: one of everything the guard has to notice ─────────────

    public enum SampleLevel : byte
    {
        Off = 0,
        Low = 1,
        High = 5,
    }

    [Flags]
    public enum SampleFlags
    {
        A = 1,
        B = 2,
    }

    // A ref parameter is one of the details the writer must be able to render, so the sample needs one.
    [SuppressMessage("Major Code Smell", "S3874:\"out\" and \"ref\" parameters should not be used", Justification = "The writer must render ref parameters.")]
    public interface ISampleHost
    {
        string Name { get; }

        string? Optional { get; set; }

        int? Count { get; }

        int this[int index] { get; }

        event EventHandler? Changed;

        Task<string?> LoadAsync(string path, CancellationToken ct = default);

        [Obsolete("Use LoadAsync.")]
        void Load(string path);

        bool TryGet(out int value, ref string text);

        void Run(params string[] args);

        T Convert<T>(object value) where T : class, new();

        IReadOnlyDictionary<string, IReadOnlyList<string?>> Lookup();
    }

    // Public constants are one of the details the writer must be able to render, so the sample needs some.
    [SuppressMessage("Major Code Smell", "S2339:Public constant members should not be used", Justification = "The writer must render constants.")]
    public abstract class SampleBase
    {
        public const int Limit = 7;
        public const string Label = "a\"b";
        public static readonly string Shared = string.Empty;

        protected SampleBase()
        {
        }

        protected internal string Hidden { get; private set; } = string.Empty;

        public string Created { get; init; } = string.Empty;

        public virtual int Weight => 1;

        public abstract void Step();

        internal int NotPartOfTheSurface() => 0;
    }

    public sealed class SampleDerived : SampleBase
    {
        public override int Weight => 2;

        public override void Step()
        {
        }
    }

    public sealed record SampleRecord(string Id, int? Count = null);

    public delegate void SampleHandler(object? sender, int value);

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class SampleMarkerAttribute : Attribute
    {
    }

    public static class SampleStatics
    {
        public static int Add(int a, int b) => a + b;
    }

    /// <summary>
    /// Tests of the writer itself. The snapshot guard is only as good as what the writer can see, so each kind of
    /// change that would break an extension must show up in its output.
    /// </summary>
    public class ApiSurfaceWriterTests
    {
        private const string Ns = "RFiDGear.Tests.ApiSurface.";

        private static IReadOnlyList<string> Describe(Type type) =>
            ApiSurfaceWriter.Describe(type).Select(l => l.Trim()).ToList();

        [Fact]
        public void Enums_ShowTheirUnderlyingTypeAndEveryValueInValueOrder()
        {
            Assert.Equal(
                new[]
                {
                    $"public enum {Ns}SampleLevel : byte",
                    "Off = 0",
                    "Low = 1",
                    "High = 5",
                },
                Describe(typeof(SampleLevel)));
        }

        [Fact]
        public void FlagsAttribute_IsPartOfTheDescription()
        {
            Assert.Equal($"[Flags] public enum {Ns}SampleFlags : int", Describe(typeof(SampleFlags))[0]);
        }

        [Fact]
        public void InterfaceMembers_ShowSignatureNullabilityDefaultsAndRefKinds()
        {
            var lines = Describe(typeof(ISampleHost));

            Assert.Equal($"public interface {Ns}ISampleHost", lines[0]);
            Assert.Contains("public string Name { get; }", lines);
            Assert.Contains("public string? Optional { get; set; }", lines);
            Assert.Contains("public int? Count { get; }", lines);
            Assert.Contains("public int this[int index] { get; }", lines);
            Assert.Contains("public event System.EventHandler? Changed", lines);
            Assert.Contains("public System.Threading.Tasks.Task<string?> LoadAsync(string path, System.Threading.CancellationToken ct = default)", lines);
            Assert.Contains("public bool TryGet(out int value, ref string text)", lines);
            Assert.Contains("public void Run(params string[] args)", lines);
            Assert.Contains("public System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string?>> Lookup()", lines);
        }

        [Fact]
        public void GenericMethods_ShowTheirConstraints()
        {
            Assert.Contains("public T Convert<T>(object value) where T : class, new()", Describe(typeof(ISampleHost)));
        }

        [Fact]
        public void ObsoleteAttribute_ShowsItsMessage()
        {
            Assert.Contains("[Obsolete(\"Use LoadAsync.\")] public void Load(string path)", Describe(typeof(ISampleHost)));
        }

        [Fact]
        public void ClassMembers_ShowModifiersConstantsInitAccessorsAndProtectedAccess()
        {
            var lines = Describe(typeof(SampleBase));

            Assert.Equal($"public abstract class {Ns}SampleBase", lines[0]);
            Assert.Contains("protected ctor()", lines);
            Assert.Contains("public const int Limit = 7", lines);
            Assert.Contains("public const string Label = \"a\\\"b\"", lines);
            Assert.Contains("public static readonly string Shared", lines);
            // The private setter is not surface; only the protected-internal getter is.
            Assert.Contains("protected internal string Hidden { get; }", lines);
            Assert.Contains("public string Created { get; init; }", lines);
            Assert.Contains("public virtual int Weight { get; }", lines);
            Assert.Contains("public abstract void Step()", lines);
        }

        [Fact]
        public void InternalMembers_AreNotPartOfTheSurface()
        {
            Assert.DoesNotContain(Describe(typeof(SampleBase)), line => line.Contains("NotPartOfTheSurface", StringComparison.Ordinal));
        }

        [Fact]
        public void DerivedClass_ShowsItsBaseAndItsOverrides()
        {
            var lines = Describe(typeof(SampleDerived));

            Assert.Equal($"public sealed class {Ns}SampleDerived : {Ns}SampleBase", lines[0]);
            Assert.Contains("public override int Weight { get; }", lines);
            Assert.Contains("public override void Step()", lines);
        }

        [Fact]
        public void Records_ShowTheirPrimaryConstructorAndInitProperties_WithoutCompilerBoilerplate()
        {
            var lines = Describe(typeof(SampleRecord));

            Assert.StartsWith("public sealed record ", lines[0], StringComparison.Ordinal);
            Assert.Contains("public ctor(string Id, int? Count = null)", lines);
            Assert.Contains("public string Id { get; init; }", lines);
            Assert.Contains("public int? Count { get; init; }", lines);
            Assert.DoesNotContain(lines, l => l.Contains("EqualityContract", StringComparison.Ordinal));
            Assert.DoesNotContain(lines, l => l.Contains("<Clone>", StringComparison.Ordinal));
        }

        [Fact]
        public void StaticClasses_AndDelegates_AreDescribed()
        {
            Assert.Equal($"public static class {Ns}SampleStatics", Describe(typeof(SampleStatics))[0]);
            Assert.Contains("public static int Add(int a, int b)", Describe(typeof(SampleStatics)));
            Assert.Equal($"public delegate void {Ns}SampleHandler(object? sender, int value)", Describe(typeof(SampleHandler))[0]);
        }

        [Fact]
        public void ElidedNamespaces_AreLeftOutOfSignaturesOnly()
        {
            var options = new ApiSurfaceOptions(
                new System.Collections.Generic.HashSet<string> { "RFiDGear.Tests.ApiSurface" },
                new System.Collections.Generic.HashSet<Type>());

            var elided = ApiSurfaceWriter.Describe(options, typeof(SampleDerived)).Select(l => l.Trim()).ToList();

            // The declaration keeps its full name; the base type, which is only mentioned, loses the namespace.
            Assert.Equal($"public sealed class {Ns}SampleDerived : SampleBase", elided[0]);
        }

        [Fact]
        public void ExtraSurfaceAttributes_AreShownOnlyWhenAskedFor_WithEnumArgumentsByName()
        {
            var plain = ApiSurfaceWriter.Describe(typeof(SampleMarkerAttribute)).Select(l => l.Trim()).First();
            var options = new ApiSurfaceOptions(
                new System.Collections.Generic.HashSet<string>(),
                new System.Collections.Generic.HashSet<Type> { typeof(AttributeUsageAttribute) });
            var extended = ApiSurfaceWriter.Describe(options, typeof(SampleMarkerAttribute)).Select(l => l.Trim()).First();

            Assert.DoesNotContain("AttributeUsage", plain, StringComparison.Ordinal);
            Assert.StartsWith("[AttributeUsage(AttributeTargets.Class, Property, AllowMultiple = false)] ", extended, StringComparison.Ordinal);
        }

        [Fact]
        public void Output_IsIdenticalOnEveryRun_AndIndependentOfTheOrderTypesAreGiven()
        {
            var first = ApiSurfaceWriter.Describe(typeof(SampleLevel), typeof(SampleRecord), typeof(ISampleHost));
            var second = ApiSurfaceWriter.Describe(typeof(ISampleHost), typeof(SampleLevel), typeof(SampleRecord));

            Assert.Equal(first, second);
        }
    }
}
