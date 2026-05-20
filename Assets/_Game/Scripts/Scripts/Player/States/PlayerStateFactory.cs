public class PlayerStateFactory
{
    IPlayerController _context;

    public PlayerBaseState Grounded { get; private set; }
    public PlayerBaseState Air { get; private set; }

    // --- YENÝ EKLENEN DURUM ---
    public PlayerBaseState Climb { get; private set; }
    // --------------------------

    public PlayerStateFactory(IPlayerController currentContext)
    {
        _context = currentContext;

        // State'leri önceden oluþturup hafýzada tutuyoruz (Memory Allocation'ý engellemek için)
        Grounded = new PlayerGroundedState(_context, this);
        Air = new PlayerAirState(_context, this);

        // --- YENÝ EKLENEN DURUMUN ÜRETÝMÝ ---
        Climb = new PlayerClimbState(_context, this); // Hata verirse merak etme, þimdi bu sýnýfý yazacaðýz.
        // ------------------------------------
    }
}