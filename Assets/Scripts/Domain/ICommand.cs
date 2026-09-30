namespace ZipTrip.Domain
{
    public interface ICommand
    {
        CommandResult Execute(GameState state);
    }
}
