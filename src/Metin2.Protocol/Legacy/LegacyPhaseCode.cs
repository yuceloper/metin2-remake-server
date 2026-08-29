namespace Metin2.Protocol.Legacy;

public enum LegacyPhaseCode : byte
{
    Handshake = 1,
    Login = 2,
    Select = 3,
    Loading = 4,
    Game = 5,
    Auth = 10
}
