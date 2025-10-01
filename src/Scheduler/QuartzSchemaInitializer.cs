// Copyright (c) 2025 Duplicati Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
using System.Reflection;
using Npgsql;

namespace Scheduler.Quartz;

public static class QuartzSchemaInitializer
{
    public static void CreateSchemaIfMissing(string adminConnectionString)
    {
        using var conn = new NpgsqlConnection(adminConnectionString);
        conn.Open();

        // Check one canonical table.
        using (var check = new NpgsqlCommand($"SELECT * FROM QRTZ_JOB_DETAILS LIMIT 1", conn))
            try
            {
                check.ExecuteScalar();
                return; // already created
            }
            catch (PostgresException pgex)
                when (pgex.SqlState == "42P01")
            {
                // No such table
            }

        // Load embedded DDL
        var sql = ReadEmbeddedText("quartz_tables_postgres.sql");

        using var tx = conn.BeginTransaction();
        using (var cmd = new NpgsqlCommand(sql, conn, tx))
        {
            cmd.ExecuteNonQuery();
            // If the script commits, we don't need to do it again
            if (!sql.Contains("COMMIT;", StringComparison.OrdinalIgnoreCase))
                tx.Commit();
        }
    }

    private static string ReadEmbeddedText(string resourcePath)
    {
        var asm = Assembly.GetExecutingAssembly();
        var fullName = asm.GetManifestResourceNames()
            .First(n => n.EndsWith(resourcePath.Replace('/', '.'), StringComparison.Ordinal));

        using var s = asm.GetManifestResourceStream(fullName)!;
        using var sr = new StreamReader(s);
        return sr.ReadToEnd();
    }
}