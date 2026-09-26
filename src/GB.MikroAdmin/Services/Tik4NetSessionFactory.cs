using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;
using tik4net;

namespace GB.MikroAdmin.Services;

/// <summary>
/// Abre la sesión real de tik4net. Es el único punto del programa que referencia tipos de
/// la librería; antes se usaba <c>Type.GetType</c> + <c>dynamic</c>, lo que eliminaba el
/// tipado y el IntelliSense sin aportar ninguna ventaja, ya que la dependencia sigue existiendo.
/// </summary>
public sealed class Tik4NetSessionFactory : ITikSessionFactory
{
    public async Task<ITikSession> OpenAsync(RouterSettings settings, CancellationToken cancellationToken)
    {
        // tik4net expone una API mayormente síncrona; se aparta del hilo de UI para no bloquearlo.
        return await Task.Run(() =>
        {
            var connection = ConnectionFactory.CreateConnection(TikConnectionType.ApiSsl);
            try
            {
                connection.Open(settings.Host, settings.Port, settings.UserName, settings.Password);
            }
            catch
            {
                connection.Close();
                throw;
            }

            return (ITikSession)new Tik4NetSession(connection);
        }, cancellationToken);
    }

    private sealed class Tik4NetSession(ITikConnection connection) : ITikSession
    {
        public IReadOnlyList<ITikRow> List(string command)
        {
            var rows = new List<ITikRow>();
            foreach (var item in connection.CreateCommandAndParameters(command).ExecuteList())
                rows.Add(new Tik4NetRow(item));
            return rows;
        }

        public Task ExecuteAsync(string command, IReadOnlyDictionary<string, string> parameters)
        {
            var arguments = parameters.SelectMany(pair => new[] { pair.Key, pair.Value }).ToArray();
            connection.CreateCommandAndParameters(command, arguments).ExecuteNonQuery();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            connection.Close();
            connection.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Tik4NetRow(ITikReSentence sentence) : ITikRow
    {
        public string GetString(string field) => GetStringOrNull(field) ?? string.Empty;

        public string? GetStringOrNull(string field)
            => sentence.GetResponseFieldOrDefault(field, null) is { Length: > 0 } value ? value : null;

        // RouterOS devuelve los booleanos como "true"/"false" y los ausentes como "".
        public bool GetBool(string field)
            => string.Equals(GetStringOrNull(field), "true", StringComparison.OrdinalIgnoreCase);

        public int GetInt(string field, int fallback)
            => int.TryParse(GetStringOrNull(field), out var value) ? value : fallback;
    }
}
