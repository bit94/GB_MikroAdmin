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

        var voucher = Assert.Single(created);
        Assert.Equal(voucher.Name, voucher.Password);
        Assert.Equal("555555", voucher.Name);
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
    /// Si el router rechaza un usuario a mitad del lote, los códigos ya creados no deben
    /// perderse: se devuelven junto al fallo para que el operador pueda entregarlos.
    /// </summary>
    [Fact]
    public async Task CreateBatch_reports_partial_failure_without_losing_created_vouchers()
    {
        var router = new RecordingWriter { RejectFrom = 2 };
        var service = new HotspotProvisioningService(router, new SequentialCodes(200_000));

        var exception = await Assert.ThrowsAsync<VoucherBatchException>(
            () => service.CreateBatchAsync(3, "limitado", TimeSpan.FromHours(1)));

        // El primero entró, el segundo y el tercero fueron rechazados por el router.
        Assert.Single(exception.Created);
        Assert.Equal("200000", exception.Created[0].Name);
        Assert.Equal(2, exception.Failed.Count);
    }

    [Fact]
    public async Task CreateBatch_propagates_cancellation()
    {
        var router = new RecordingWriter();
        var service = new HotspotProvisioningService(router, new SequentialCodes(300_000));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CreateBatchAsync(5, "limitado", TimeSpan.FromHours(1), cts.Token));
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

    /// <summary>Genera códigos consecutivos para poder predecir el resultado en las pruebas.</summary>
    private sealed class SequentialCodes(int start) : IVoucherCodeGenerator
    {
        private int _current = start - 1;

        public string Next() => (++_current).ToString();
    }

    /// <summary>Devuelve una secuencia fija de códigos, para provocar colisiones a voluntad.</summary>
    private sealed class ScriptedCodes(params string[] values) : IVoucherCodeGenerator
    {
        private int _index;

        public string Next() => values[Math.Min(_index++, values.Length - 1)];
    }

    /// <summary>Devuelve siempre el mismo código, para agotar los reintentos.</summary>
    private sealed class FixedCodes(string value) : IVoucherCodeGenerator
    {
        public string Next() => value;
    }
}
