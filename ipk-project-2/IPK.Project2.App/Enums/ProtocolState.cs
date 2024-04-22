namespace App.Enums;

public enum ProtocolState
{
    Accept,
    Auth,
    Open,
    End
}

public class ProtocolStateBox
{
    public ProtocolState State { get; private set; }

    public ProtocolStateBox(ProtocolState state)
    {
        State = state;
    }

    public void SetState(ProtocolState state)
    {
        State = state;
    }
}