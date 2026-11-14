#nullable enable
using Microsoft.Data.Sqlite;

using System;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    /// <summary>
    /// The engine the tests of the order store run on. It is only a stand-in for SQL Server and is not shipped, but NuGet checks it
    /// all the same: the SQLite that SQLitePCLRaw.lib.e_sqlite3 2.1.11 and older carry is older than 3.50.2, which has a flaw that
    /// can corrupt memory (CVE-2025-6965, GHSA-2m69-gcr7-jv3q), and every restore of the test project warns about it (NU1903).
    /// </summary>
    public class TestDatabaseEngineTests
    {
        [Fact]
        public void The_engine_the_order_store_tests_run_on_is_not_older_than_the_one_that_has_the_fix()
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT sqlite_version()";
            var version = Version.Parse((string)command.ExecuteScalar()!);

            Assert.True(version >= new Version(3, 50, 2), "the tests run on SQLite " + version);
        }
    }
}
