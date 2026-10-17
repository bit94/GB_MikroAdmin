using System.Net.Sockets;
using System.Security.Authentication;
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
        var setup = new TikConnectionSetup(
            TikRouterAddress.FromHost(settings.Host),
            settings.UserName,
            settings.Password)
        {
            Port = settings.Port,
            AllowInvalidCertificate = false,
            // tik4net autentica con checkCertificateRevocation desactivado. El callback toma el
            // control del mismo socket que envía las credenciales y exige nombre, cadena y
            // revocación en línea; reemplaza al handshake de comprobación separado.
            CertificateValidationCallback = (_, certificate, _, errors) =>
                RouterCertificatePolicy.Validate(settings.Host, certificate, errors)
        };

        ITikConnection connection;
        try
        {
            connection = await setup.CreateAsync(TikConnectionType.ApiSsl, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is AuthenticationException or TikConnectionSSLErrorException)
        {
            // tik4net reduce el rechazo a «API-SSL error»; el detalle de la política se pierde.
            throw new AuthenticationException(
                $"No se pudo validar la conexión TLS con «{settings.Host}:{settings.Port}». " +
                "Comprueba que api-ssl use un certificado de confianza, vigente y con el nombre del host en su SAN.",
                exception);
        }

        return new Tik4NetSession(connection);
    }

    private sealed class Tik4NetSession(ITikConnection connection) : ITikSession
    {
        // La conexión admite un comando en vuelo; el refresco automático y un lote en curso
        // pueden solaparse ahora que nada corre en el hilo de UI.
        private readonly SemaphoreSlim _gate = new(1, 1);

        public async Task<IReadOnlyList<ITikRow>> ListAsync(string command, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                // tik4net expone una API mayormente síncrona; se aparta del hilo de UI para no bloquearlo.
                return await Task.Run<IReadOnlyList<ITikRow>>(() =>
                {
                    var rows = new List<ITikRow>();
                    foreach (var item in connection.CreateCommandAndParameters(command).ExecuteList())
                        rows.Add(new Tik4NetRow(item));
                    return rows;
                }, cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task ExecuteAsync(string command, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                await Task.Run(() =>
                {
                    var arguments = parameters.SelectMany(pair => new[] { pair.Key, pair.Value }).ToArray();
                    try
                    {
                        connection.CreateCommandAndParameters(command, arguments).ExecuteNonQuery();
                    }
                    catch (TikAlreadyHaveSuchItemException exception)
                    {
                        throw new RouterWriteException(
                            RouterWriteFailureKind.Duplicate, "El código ya existe en el router.", exception);
                    }
                    catch (TikCommandTrapException exception) when (IsPermissionTrap(exception))
                    {
                        throw new RouterWriteException(
                            RouterWriteFailureKind.Permission,
                            "El usuario del router no tiene permisos para crear tickets.", exception);
                    }
                    catch (TikConnectionLoginException exception)
                    {
                        throw new RouterWriteException(
                            RouterWriteFailureKind.Authentication, "El router rechazó las credenciales.", exception);
                    }
                    catch (TikCommandTrapException exception)
                    {
                        var detail = string.IsNullOrWhiteSpace(exception.CodeDescription)
                            ? exception.Message
                            : exception.CodeDescription;
                        throw new RouterWriteException(
                            RouterWriteFailureKind.Router,
                            string.IsNullOrWhiteSpace(detail)
                                ? "El router rechazó la operación."
                                : $"El router rechazó la operación: {detail}",
                            exception);
                    }
                    catch (Exception exception) when (IsConnectionFailure(exception))
                    {
                        throw new RouterWriteException(
                            RouterWriteFailureKind.Connection, "Se perdió la conexión con el router.", exception);
                    }
                }, cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }

        public ValueTask DisposeAsync()
        {
            connection.Close();
            connection.Dispose();
            return ValueTask.CompletedTask;
        }

        private static bool IsPermissionTrap(TikCommandTrapException exception)
        {
            var text = $"{exception.CodeDescription} {exception.Message}";
            return text.Contains("not permitted", StringComparison.OrdinalIgnoreCase)
                || text.Contains("missing policy", StringComparison.OrdinalIgnoreCase)
                || text.Contains("permission", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsConnectionFailure(Exception exception)
            => exception is TikConnectionException
                or TikConnectionNotOpenException
                or TikConnectionReceiveTimeoutException
                or TikConnectionResponseIncompleteException
                or TikCommandFatalException
                or IOException
                or SocketException
                or ObjectDisposedException;
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
