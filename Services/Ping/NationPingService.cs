using ISRORBilling.Models.Ping;

using Microsoft.Extensions.Options;

using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace ISRORBilling.Services.Ping
{
    public class NationPingService : BackgroundService
    {
        private readonly ILogger<NationPingService> _logger;
        readonly NationPingServiceOptions _options;

        private readonly TcpListener _tcpListener;

        private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

        public NationPingService(ILogger<NationPingService> logger, IOptions<NationPingServiceOptions> options)
        {
            _logger = logger;
            _options = options.Value;

            if (!IPAddress.TryParse(_options.ListenAddress, out var address))
                address = Dns.GetHostEntry(_options.ListenAddress).AddressList.FirstOrDefault() ?? IPAddress.Loopback;

            _tcpListener = new TcpListener(address, _options.ListenPort);
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _tcpListener.Start();
            _logger.LogInformation(
                "Ping Service listening on [{ServiceOptionsListenAddress}:{ServiceOptionsListenPort}]",
                _options.ListenAddress, _options.ListenPort);

            return ProcessAccept(cancellationToken);
        }

        private async Task ProcessAccept(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var socket = await _tcpListener.AcceptSocketAsync(cancellationToken).ConfigureAwait(false);

                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("[{clientEndPoint}]: connected.", socket.RemoteEndPoint);

                _ = ProcessSocketSafe(socket, cancellationToken);
            }
        }

        private async Task ProcessSocketSafe(Socket socket, CancellationToken cancellationToken)
        {
            try
            {
                // Clients that connect and never send a full request must not keep the socket open forever.
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(ReadTimeout);
                await ProcessSocket(socket, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException or IOException or SocketException or EndOfStreamException)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Ping connection closed: {Reason}", e.Message);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected error while processing a ping connection");
            }
        }

        private static async Task ProcessSocket(Socket socket, CancellationToken cancellationToken)
        {
            await using var stream = new NetworkStream(socket, true);

            var buffer = ArrayPool<byte>.Shared.Rent(14);
            var memory = new Memory<byte>(buffer)[..14];
            try
            {
                await stream.ReadExactlyAsync(memory, cancellationToken).ConfigureAwait(false);
                if (buffer[2] != (byte)'R' || buffer[3] != (byte)'E' || buffer[4] != (byte)'Q' || buffer[5] != (byte)'\0')
                    return;

                buffer[2] = (byte)'A';
                buffer[3] = (byte)'C';
                buffer[4] = (byte)'K';
                buffer[5] = (byte)'\0';

                await stream.WriteAsync(memory, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}