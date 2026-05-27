using System.Threading.Tasks;

namespace SandBlast.Network
{
    // 플레이어 데이터 저장·불러오기 추상화.
    // LocalJsonRepository(싱글) / HostFileRepository(호스트) / RemoteApiRepository(MMO)가 구현한다.
    public interface IPlayerDataRepository
    {
        Task<PlayerSaveData> LoadAsync(string playerId);
        Task SaveAsync(string playerId, PlayerSaveData data);
        Task<bool> ExistsAsync(string playerId);
    }

    [System.Serializable]
    public class PlayerSaveData
    {
        public string   PlayerId       = "local";
        public int      PlayerLevel    = 1;
        public int      XP             = 0;
        public string[] UnlockedSkills = System.Array.Empty<string>();
        public int      CurrentWorld   = 1;
        public int      CurrentLevel   = 1;
    }
}
