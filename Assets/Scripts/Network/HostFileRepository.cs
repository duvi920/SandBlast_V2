using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace SandBlast.Network
{
    // 호스트 머신의 로컬 파일에 JSON으로 플레이어 데이터를 저장한다.
    // 저장 경로: Application.persistentDataPath/SaveData/<playerId>.json
    public class HostFileRepository : IPlayerDataRepository
    {
        readonly string _saveDir;

        public HostFileRepository()
        {
            _saveDir = Path.Combine(Application.persistentDataPath, "SaveData");
            Directory.CreateDirectory(_saveDir);
        }

        public async Task<PlayerSaveData> LoadAsync(string playerId)
        {
            var path = GetPath(playerId);
            if (!File.Exists(path))
                return new PlayerSaveData { PlayerId = playerId };
            var json = await File.ReadAllTextAsync(path);
            return JsonUtility.FromJson<PlayerSaveData>(json) ?? new PlayerSaveData { PlayerId = playerId };
        }

        public async Task SaveAsync(string playerId, PlayerSaveData data)
        {
            var json = JsonUtility.ToJson(data, prettyPrint: true);
            await File.WriteAllTextAsync(GetPath(playerId), json);
        }

        public Task<bool> ExistsAsync(string playerId)
            => Task.FromResult(File.Exists(GetPath(playerId)));

        string GetPath(string playerId)
            => Path.Combine(_saveDir, $"{playerId}.json");
    }
}
