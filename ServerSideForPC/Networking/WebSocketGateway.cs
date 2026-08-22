using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ServerSideForPC.Configuration;
using ServerSideForPC.Models;
using ServerSideForPC.Services;

namespace ServerSideForPC.Networking;

public sealed class WebSocketGateway : IDisposable
{
    private const int MaxMessageBytes = 64 * 1024;
    private readonly ConcurrentDictionary<Guid, ClientSession> _sessions = new();
    private readonly PcControlOptions _options;
    private readonly AudioService _audio;
    private readonly ProfileService _profiles;
    private readonly PowerService _power;
    private readonly MediaService _media;
    private readonly PcStateService _state;
    private int _broadcastScheduled;

    public WebSocketGateway(
        PcControlOptions options,
        AudioService audio,
        ProfileService profiles,
        PowerService power,
        MediaService media,
        PcStateService state)
    {
        _options = options;
        _audio = audio;
        _profiles = profiles;
        _power = power;
        _media = media;
        _state = state;
        _state.StateChanged += ScheduleStateBroadcast;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!IsAuthorized(context))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        var session = new ClientSession(socket);
        _sessions[session.Id] = session;
        var sendLoop = session.SendLoopAsync();

        try
        {
            QueueState(session);
            await ReceiveLoopAsync(session, context.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            _sessions.TryRemove(session.Id, out _);
            await session.DisposeAsync().ConfigureAwait(false);
            await sendLoop.ConfigureAwait(false);
        }
    }

    private bool IsAuthorized(HttpContext context)
    {
        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            return true;
        }

        var supplied = context.Request.Headers["X-Pc-Control-Key"].ToString();
        return string.Equals(supplied, _options.ApiKey, StringComparison.Ordinal);
    }

    private async Task ReceiveLoopAsync(ClientSession session, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        while (!cancellationToken.IsCancellationRequested && session.Socket.State == WebSocketState.Open)
        {
            using var messageBuffer = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await session.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    await QueueErrorAsync(session, null, "UNSUPPORTED_MESSAGE", "Only text JSON messages are supported.")
                        .ConfigureAwait(false);
                    return;
                }

                messageBuffer.Write(buffer, 0, result.Count);
                if (messageBuffer.Length > MaxMessageBytes)
                {
                    await QueueErrorAsync(session, null, "MESSAGE_TOO_LARGE", "Message exceeds 64 KiB.")
                        .ConfigureAwait(false);
                    return;
                }
            }
            while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(messageBuffer.ToArray());
            await HandleMessageAsync(session, json, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleMessageAsync(ClientSession session, string json, CancellationToken cancellationToken)
    {
        ProtocolRequest? request = null;
        try
        {
            request = JsonSerializer.Deserialize<ProtocolRequest>(json, JsonDefaults.Options);
            if (request is null || string.IsNullOrWhiteSpace(request.Type))
            {
                throw new ArgumentException("Request type is required.");
            }
            if (request.Version != 1)
            {
                throw new ArgumentException($"Unsupported protocol version '{request.Version}'.");
            }

            switch (request.Type)
            {
                case "state.get":
                    QueueState(session);
                    break;

                case "audio.setVolume":
                    _audio.SetVolume(GetRequired<float>(request.Payload, "value"));
                    break;

                case "audio.setMute":
                    _audio.SetMute(GetRequired<bool>(request.Payload, "muted"));
                    break;

                case "audio.setDevice":
                    _audio.SetDefaultDevice(GetRequired<string>(request.Payload, "deviceId"));
                    break;

                case "profile.extend":
                    await _profiles.ActivateExtendAsync(cancellationToken).ConfigureAwait(false);
                    break;

                case "profile.tv":
                    await _profiles.ActivateTvAsync(cancellationToken).ConfigureAwait(false);
                    break;

                case "media.playPause":
                    _media.PlayPause();
                    break;

                case "media.next":
                    _media.Next();
                    break;

                case "media.previous":
                    _media.Previous();
                    break;

                case "media.stop":
                    _media.Stop();
                    break;

                case "power.shutdown":
                    _power.Shutdown();
                    break;

                case "ping":
                    break;

                default:
                    throw new ArgumentException($"Unknown command '{request.Type}'.");
            }

            QueueResponse(session, request.Id, true, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await QueueErrorAsync(session, request?.Id, "COMMAND_FAILED", ex.Message).ConfigureAwait(false);
        }
    }

    private static T GetRequired<T>(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(propertyName, out var value))
        {
            throw new ArgumentException($"Payload property '{propertyName}' is required.");
        }

        var parsed = value.Deserialize<T>(JsonDefaults.Options);
        if (parsed is null)
        {
            throw new ArgumentException($"Payload property '{propertyName}' is invalid.");
        }
        return parsed;
    }

    private void QueueState(ClientSession session)
    {
        var payload = JsonSerializer.Serialize(new
        {
            v = 1,
            type = "event.state",
            data = _state.GetSnapshot()
        }, JsonDefaults.Options);
        session.TryQueue(payload);
    }

    private void ScheduleStateBroadcast()
    {
        if (Interlocked.Exchange(ref _broadcastScheduled, 1) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(35).ConfigureAwait(false);
                // Re-open the scheduling gate before taking the snapshot. A state change that
                // happens during serialization/sending will schedule the next broadcast.
                Interlocked.Exchange(ref _broadcastScheduled, 0);

                var payload = JsonSerializer.Serialize(new
                {
                    v = 1,
                    type = "event.state",
                    data = _state.GetSnapshot()
                }, JsonDefaults.Options);

                foreach (var session in _sessions.Values)
                {
                    session.TryQueue(payload);
                }
            }
            catch
            {
                Interlocked.Exchange(ref _broadcastScheduled, 0);
            }
        });
    }

    private static void QueueResponse(ClientSession session, string? id, bool ok, string? errorCode, string? errorMessage)
    {
        var payload = JsonSerializer.Serialize(new
        {
            v = 1,
            id,
            type = "response",
            ok,
            error = errorCode is null ? null : new { code = errorCode, message = errorMessage }
        }, JsonDefaults.Options);
        session.TryQueue(payload);
    }

    private static Task QueueErrorAsync(ClientSession session, string? id, string code, string message)
    {
        QueueResponse(session, id, false, code, message);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _state.StateChanged -= ScheduleStateBroadcast;
    }
}
