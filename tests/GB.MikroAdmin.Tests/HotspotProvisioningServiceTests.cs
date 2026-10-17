using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;
using Xunit;

namespace GB.MikroAdmin.Tests;

public sealed class HotspotProvisioningServiceTests
{
    [Fact]
    public async Task CreateBatch_creates_the_requested_quantity()
    {
        var router = new RecordingWriter();
        var service = new HotspotProvisioningService(router, new SequentialCodes(1_000_000));

        var created = await service.CreateBatchAsync(3, "limitado", TimeSpan.FromHours(1));

        Assert.Equal(3, created.Count);
        Assert.Equal(3, router.Writes.Count);
        Assert.All(router.Writes, w => Assert.Equal("limitado", w.Profile));
    }

    [Fact]
    public async Task CreateBatch_uses_the_code_as_both_user_and_password()
    {
        var router = new RecordingWriter();
        var service = new HotspotProvisioningService(router, new SequentialCodes(555_555));

        var created = await service.CreateBatchAsync(1, "limitado", TimeSpan.FromHours(1));

        var ticket = Assert.Single(created);
        Assert.Equal(ticket.Name, ticket.Password);
        Assert.Equal("555555", ticket.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task CreateBatch_rejects_quantities_outside_one_to_one_hundred(int quantity)
    {
        var service = new HotspotProvisioningService(new RecordingWriter(), new SequentialCodes(1_000_000));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.CreateBatchAsync(quantity, "limitado", TimeSpan.FromHours(1)));
    }

    [Fact]
    public async Task CreateBatch_requires_a_profile()
    {
        var service = new HotspotProvisioningService(new RecordingWriter(), new SequentialCodes(1_000_000));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateBatchAsync(1, "  ", TimeSpan.FromHours(1)));
    }

    [Fact]
    public async Task CreateBatch_requires_a_positive_duration()
    {
        var service = new HotspotProvisioningService(new RecordingWriter(), new SequentialCodes(1_000_000));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.CreateBatchAsync(1, "limitado", TimeSpan.Zero));
    }

    /// <summary>
    /// El generador repite el mismo código: el servicio debe acuñar otros hasta alcanzar la
    /// cantidad pedida en lugar de crear usuarios duplicados o abortar el lote.
    /// </summary>
    [Fact]
    public async Task CreateBatch_retries_when_a_code_collides()
    {
        var router = new RecordingWriter();
        var codes = new ScriptedCodes("100001", "100001", "100002", "100003");
        var service = new HotspotProvisioningService(router, codes);

        var created = await service.CreateBatchAsync(3, "limitado", TimeSpan.FromHours(1));

        Assert.Equal(3, created.Count);
        Assert.Equal(3, created.Select(c => c.Name).Distinct().Count());
    }

    [Fact]
    public async Task CreateBatch_fails_when_unique_codes_cannot_be_coined()
    {
        var service = new HotspotProvisioningService(new RecordingWriter(), new FixedCodes("111111"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateBatchAsync(2, "limitado", TimeSpan.FromHours(1)));
    }

    /// <summary>
    /// Si el router rechaza un usuario por un fallo sistémico, los códigos ya creados no deben
    /// perderse y el lote debe detenerse en lugar de repetir el mismo error cien veces.
    /// </summary>
    [Fact]
    public async Task CreateBatch_aborts_on_a_systemic_failure_without_losing_created_tickets()
    {
        var router = new RecordingWriter { RejectFrom = 2 };
        var service = new HotspotProvisioningService(router, new SequentialCodes(200_000));

        var exception = await Assert.ThrowsAsync<TicketBatchException>(
            () => service.CreateBatchAsync(3, "limitado", TimeSpan.FromHours(1)));

        // El primero entró; el segundo falló y el tercero ya no se intentó.
        Assert.Single(exception.Created);
        Assert.Equal("200000", exception.Created[0].Name);
        Assert.Single(exception.Failed);
        Assert.Equal(1, exception.NotAttempted);
        Assert.Equal("El router rechazó el usuario.", exception.Reason);
    }

    [Fact]
    public async Task CreateBatch_aborts_on_a_connection_failure()
    {
        var router = new ConnectionFailureWriter("100001");
        var service = new HotspotProvisioningService(router, new SequentialCodes(100_000));

        var exception = await Assert.ThrowsAsync<TicketBatchException>(
            () => service.CreateBatchAsync(10, "limitado", TimeSpan.FromHours(1)));

        Assert.Single(exception.Created);
        Assert.Single(exception.Failed);
        Assert.Equal(8, exception.NotAttempted);
        Assert.Equal("Se perdió la conexión con el router.", exception.Reason);
        Assert.Equal(2, router.Attempts);
    }

    [Fact]
    public async Task CreateBatch_retries_a_code_that_already_exists_in_the_router()
    {
        var router = new DuplicateOnceWriter("200000");
        var service = new HotspotProvisioningService(router, new SequentialCodes(200_000));

        var created = await service.CreateBatchAsync(2, "limitado", TimeSpan.FromHours(1));

        Assert.Equal(2, created.Count);
        Assert.DoesNotContain(created, ticket => ticket.Name == "200000");
        Assert.Equal(3, router.Attempts);
    }

    [Fact]
    public async Task CreateBatch_gives_up_on_a_code_after_the_retry_limit()
    {
        var router = new DuplicateWriter();
        var service = new HotspotProvisioningService(router, new SequentialCodes(300_000));

        var exception = await Assert.ThrowsAsync<TicketBatchException>(
            () => service.CreateBatchAsync(1, "limitado", TimeSpan.FromHours(1)));

        Assert.Empty(exception.Created);
        Assert.Single(exception.Failed);
        Assert.Equal(0, exception.NotAttempted);
        Assert.Contains("ya existía", exception.Reason);
        Assert.Equal(HotspotProvisioningService.CodeAttempts + 1, router.Attempts);
    }

    [Fact]
    public async Task CreateBatch_propagates_cancellation()
    {
        var router = new RecordingWriter();
        var service = new HotspotProvisioningService(router, new SequentialCodes(300_000));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CreateBatchAsync(5, "limitado", TimeSpan.FromHours(1), null, cts.Token));
    }

    [Fact]
    public async Task CreateBatch_reports_progress_from_zero_to_total()
    {
        var service = new HotspotProvisioningService(new RecordingWriter(), new SequentialCodes(400_000));
        var reports = new List<BatchProgress>();

        await service.CreateBatchAsync(
            4, "limitado", TimeSpan.FromHours(1), new CollectingProgress(reports));

        Assert.Equal(0, reports[0].Completed);
        Assert.Equal(4, reports[^1].Completed);
        Assert.All(reports, r => Assert.Equal(4, r.Total));
    }

    /// <summary>El avance llega hasta el total aunque el lote se detenga por un fallo.</summary>
    [Fact]
    public async Task CreateBatch_still_reports_full_progress_when_it_stops_early()
    {
        var router = new RecordingWriter { RejectFrom = 2 };
        var service = new HotspotProvisioningService(router, new SequentialCodes(500_000));
        var reports = new List<BatchProgress>();

        await Assert.ThrowsAsync<TicketBatchException>(
            () => service.CreateBatchAsync(4, "limitado", TimeSpan.FromHours(1), new CollectingProgress(reports)));

        Assert.Equal(4, reports[^1].Completed);
    }

    [Fact]
    public void BatchProgress_percent_and_remaining_are_consistent()
    {
        var progress = new BatchProgress(25, 100);

        Assert.Equal(75, progress.Remaining);
        Assert.Equal(25, progress.Percent);
    }

    [Fact]
    public void BatchProgress_handles_an_empty_total_without_dividing_by_zero()
        => Assert.Equal(0, new BatchProgress(0, 0).Percent);

    /// <summary>Se cancela a mitad del lote y lo ya creado debe seguir siendo válido.</summary>
    [Fact]
    public async Task CreateBatch_cancelling_midway_keeps_what_was_already_created()
    {
        var router = new RecordingWriter();
        var service = new HotspotProvisioningService(router, new SequentialCodes(600_000));
        using var cts = new CancellationTokenSource();
        var progress = new CancellingProgress(cts, cancelAt: 2);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CreateBatchAsync(10, "limitado", TimeSpan.FromHours(1), progress, cts.Token));

        // Los dos primeros entraron en el router antes de que se pidiera la cancelación.
        Assert.Equal(2, router.Writes.Count);
    }

    private sealed class CollectingProgress(List<BatchProgress> reports) : IProgress<BatchProgress>
    {
        public void Report(BatchProgress value) => reports.Add(value);
    }

    private sealed class CancellingProgress(CancellationTokenSource source, int cancelAt) : IProgress<BatchProgress>
    {
        public void Report(BatchProgress value)
        {
            if (value.Completed >= cancelAt)
                source.Cancel();
        }
    }

    private sealed class RecordingWriter : IRouterUserWriter
    {
        public List<(string Name, string Password, string Profile, TimeSpan Limit)> Writes { get; } = [];

        /// <summary>A partir de este número de intento, el router rechaza las altas.</summary>
        public int? RejectFrom { get; init; }

        public Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken cancellationToken = default)
        {
            if (RejectFrom is { } threshold && Writes.Count + 1 >= threshold)
                throw new InvalidOperationException("El router rechazó el usuario.");

            Writes.Add((name, password, profile, limit));
            return Task.CompletedTask;
        }
    }

    private sealed class ConnectionFailureWriter(string failCode) : IRouterUserWriter
    {
        public int Attempts { get; private set; }

        public Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (name == failCode)
                throw new RouterWriteException(
                    RouterWriteFailureKind.Connection, "Se perdió la conexión con el router.");

            return Task.CompletedTask;
        }
    }

    private sealed class DuplicateOnceWriter(string duplicate) : IRouterUserWriter
    {
        private bool _rejected;

        public int Attempts { get; private set; }

        public Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (!_rejected && name == duplicate)
            {
                _rejected = true;
                throw new RouterWriteException(
                    RouterWriteFailureKind.Duplicate, "El código ya existe en el router.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class DuplicateWriter : IRouterUserWriter
    {
        public int Attempts { get; private set; }

        public Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken cancellationToken = default)
        {
            Attempts++;
            throw new RouterWriteException(
                RouterWriteFailureKind.Duplicate, "El código ya existe en el router.");
        }
    }

    /// <summary>Genera códigos consecutivos para poder predecir el resultado en las pruebas.</summary>
    private sealed class SequentialCodes(int start) : ITicketCodeGenerator
    {
        private int _current = start - 1;

        public string Next() => (++_current).ToString();
    }

    /// <summary>Devuelve una secuencia fija de códigos, para provocar colisiones a voluntad.</summary>
    private sealed class ScriptedCodes(params string[] values) : ITicketCodeGenerator
    {
        private int _index;

        public string Next() => values[Math.Min(_index++, values.Length - 1)];
    }

    /// <summary>Devuelve siempre el mismo código, para agotar los reintentos.</summary>
    private sealed class FixedCodes(string value) : ITicketCodeGenerator
    {
        public string Next() => value;
    }
}
