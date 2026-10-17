using System.Text.Json;
using GB.MikroAdmin.Core.Services;
using Xunit;

namespace GB.MikroAdmin.Tests;

public sealed class TicketHistoryStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "gb-mikroadmin-tests", Guid.NewGuid().ToString("N"));

    public TicketHistoryStoreTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private TicketHistoryStore Store() => new(_directory);

    private static TicketBatchExport Batch(DateTimeOffset createdAt, params string[] codes) => new(
        createdAt, "limitado", TimeSpan.FromHours(1),
        codes.Select(code => new CreatedHotspotUser(code, code)).ToList());

    [Fact]
    public async Task Save_and_get_round_trips_the_batch()
    {
        var batch = Batch(DateTimeOffset.Now, "111", "222");

        await Store().SaveAsync(batch);
        var all = await Store().GetAllAsync();

        var saved = Assert.Single(all);
        Assert.Equal(batch.CreatedAt, saved.CreatedAt);
        Assert.Equal(["111", "222"], saved.Tickets.Select(t => t.Name));
    }

    [Fact]
    public async Task Get_returns_empty_when_there_is_no_history()
    {
        Assert.Empty(await Store().GetAllAsync());
    }

    [Fact]
    public async Task Mark_printed_merges_and_persists()
    {
        var batch = Batch(DateTimeOffset.Now, "111", "222");
        var store = Store();
        await store.SaveAsync(batch);

        await store.MarkPrintedAsync(batch, ["111"]);
        await store.MarkPrintedAsync(batch, ["111", "222"]);

        var saved = Assert.Single(await Store().GetAllAsync());
        Assert.Equal(["111", "222"], saved.Printed);
    }

    [Fact]
    public async Task Remove_deletes_only_the_given_batch()
    {
        var first = Batch(DateTimeOffset.Now.AddMinutes(-1), "111");
        var second = Batch(DateTimeOffset.Now, "222");
        var store = Store();
        await store.SaveAsync(first);
        await store.SaveAsync(second);

        await store.RemoveAsync(first);

        var saved = Assert.Single(await Store().GetAllAsync());
        Assert.Equal(second.CreatedAt, saved.CreatedAt);
    }

    [Fact]
    public async Task Save_keeps_the_100_most_recent_batches()
    {
        var store = Store();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 105; i++)
            await store.SaveAsync(Batch(start.AddMinutes(i), i.ToString()));

        var all = await store.GetAllAsync();

        Assert.Equal(100, all.Count);
        Assert.Equal(start.AddMinutes(104), all[0].CreatedAt);
        Assert.Equal(start.AddMinutes(5), all[^1].CreatedAt);
    }

    [Fact]
    public async Task Corrupt_history_is_not_silently_overwritten()
    {
        var path = Path.Combine(_directory, TicketHistoryStore.FileName);
        const string corrupt = "{ esto no es json";
        await File.WriteAllTextAsync(path, corrupt);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => Store().SaveAsync(Batch(DateTimeOffset.Now, "111")));

        Assert.Equal(corrupt, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Corrupt_history_recovers_from_the_previous_backup()
    {
        var path = Path.Combine(_directory, TicketHistoryStore.FileName);
        var batch = Batch(DateTimeOffset.Now, "111");
        await File.WriteAllTextAsync(
            path + TicketHistoryStore.BackupSuffix,
            JsonSerializer.Serialize(new[] { batch }));
        await File.WriteAllTextAsync(path, "no es json");

        var recovered = Assert.Single(await Store().GetAllAsync());

        Assert.Equal(batch.CreatedAt, recovered.CreatedAt);
    }

    [Fact]
    public async Task Saving_after_a_recovery_keeps_the_backup_readable()
    {
        var store = Store();
        var first = Batch(DateTimeOffset.Now.AddMinutes(-2), "111");
        await store.SaveAsync(first);
        await store.SaveAsync(Batch(DateTimeOffset.Now.AddMinutes(-1), "222"));

        var path = Path.Combine(_directory, TicketHistoryStore.FileName);
        var backup = path + TicketHistoryStore.BackupSuffix;
        await File.WriteAllTextAsync(path, "no es json");

        await store.SaveAsync(Batch(DateTimeOffset.Now, "333"));

        var all = await Store().GetAllAsync();
        Assert.Equal(["333", "111"], all.SelectMany(b => b.Tickets).Select(t => t.Name));

        // El respaldo bueno no debe quedar sustituido por el principal dañado.
        var recovered = JsonSerializer.Deserialize<List<TicketBatchExport>>(await File.ReadAllTextAsync(backup));
        Assert.NotNull(recovered);
        Assert.Equal(["111"], Assert.Single(recovered).Tickets.Select(t => t.Name));
    }

    [Fact]
    public async Task Second_save_creates_a_backup_and_leaves_no_temp_file()
    {
        var store = Store();
        await store.SaveAsync(Batch(DateTimeOffset.Now.AddMinutes(-1), "111"));
        await store.SaveAsync(Batch(DateTimeOffset.Now, "222"));

        Assert.True(File.Exists(Path.Combine(_directory, TicketHistoryStore.FileName)));
        Assert.True(File.Exists(Path.Combine(
            _directory, TicketHistoryStore.FileName + TicketHistoryStore.BackupSuffix)));
        Assert.False(File.Exists(Path.Combine(_directory, TicketHistoryStore.FileName + ".tmp")));
    }

    [Fact]
    public async Task Legacy_history_is_read_and_migrated_on_the_next_save()
    {
        var old = Batch(DateTimeOffset.Now.AddMinutes(-1), "111");
        await File.WriteAllTextAsync(
            Path.Combine(_directory, TicketHistoryStore.LegacyFileName),
            JsonSerializer.Serialize(new[] { old }));

        var store = Store();
        Assert.Single(await store.GetAllAsync());

        await store.SaveAsync(Batch(DateTimeOffset.Now, "222"));

        Assert.True(File.Exists(Path.Combine(_directory, TicketHistoryStore.FileName)));
        Assert.Equal(2, (await Store().GetAllAsync()).Count);
    }
}
