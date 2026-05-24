using Unity.Entities;

public struct GameResultSingleton : IComponentData
{
    public int WinnerNetworkId;
    public bool GameEnded;
    public bool IsDraw;
}
