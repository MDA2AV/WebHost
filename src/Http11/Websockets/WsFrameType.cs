namespace WebHost.Http11.Websockets;

public enum WsFrameType
{
    Continue,
    Utf8,
    Binary,
    Close,
    Ping,
    Pong
}