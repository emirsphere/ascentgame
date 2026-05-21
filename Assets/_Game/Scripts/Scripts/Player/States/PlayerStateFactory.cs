public class PlayerStateFactory
{
    IPlayerController _context;

    public PlayerBaseState Grounded { get; private set; }
    public PlayerBaseState Air { get; private set; }
    public PlayerBaseState Hang { get; private set; } // YENİ: Tek El Sarkma
    public PlayerBaseState Climb { get; private set; } // YENİ: İki El Tırmanma

    public PlayerStateFactory(IPlayerController currentContext)
    {
        _context = currentContext;
        Grounded = new PlayerGroundedState(_context, this);
        Air = new PlayerAirState(_context, this);
        Hang = new PlayerHangState(_context, this);
        Climb = new PlayerClimbState(_context, this);
    }
}