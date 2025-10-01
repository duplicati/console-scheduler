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
using MassTransit;
using MassTransit.SqlTransport.PostgreSql;

namespace Scheduler.MassTransit;

public static class MessagingSchemaInitializer
{
    public static async Task RunPostgresMigrationsAsync(string connectionString, CancellationToken ct)
    {
        var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddConsole());
        var migrator = new PostgresDatabaseMigrator(factory.CreateLogger<PostgresDatabaseMigrator>());
        var opts = new SqlTransportOptions() { ConnectionString = connectionString };

        await migrator.CreateSchemaIfNotExist(opts, ct).ConfigureAwait(false);
        await migrator.CreateInfrastructure(opts, ct).ConfigureAwait(false);
    }
}
